using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Rgpd.Infrastructure.Migrations;

/// <summary>
/// Applique les scripts SQL embarqués du dossier <c>Migrations</c> dans l'ordre de leur nom.
/// Crée la base de données cible si elle n'existe pas encore.
/// Les scripts sont idempotents : exécuter le migrateur plusieurs fois est sans effet de bord.
/// </summary>
public sealed partial class DatabaseMigrator
{
    private readonly string _connectionString;

    public DatabaseMigrator(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Crée la base si nécessaire puis applique tous les scripts de migration embarqués.
    /// </summary>
    /// <returns>La liste ordonnée des scripts appliqués.</returns>
    public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("La chaîne de connexion 'DefaultConnection' est vide.");
        }

        await EnsureDatabaseExistsAsync(cancellationToken);

        var assembly = typeof(DatabaseMigrator).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Where(name => !name.Contains(".Migrations.Manual.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var applied = new List<string>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var resourceName in resourceNames)
        {
            var script = await ReadResourceAsync(assembly, resourceName);
            if (string.IsNullOrWhiteSpace(script))
            {
                continue;
            }

            foreach (var batch in SplitBatches(script))
            {
                // connection.CreateCommand() (et non 'new SqlCommand') pour rester conforme à l'analyseur RGPD001 :
                // les migrations sont du DDL d'administration et ne doivent pas passer par le filtrage de session.
                using var command = connection.CreateCommand();
#pragma warning disable CA2100
                command.CommandText = batch;
#pragma warning restore CA2100
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            applied.Add(ToFriendlyName(resourceName));
        }

        return applied;
    }

    /// <summary>
    /// Applique un script de migration "manuel" embarqué (dossier <c>Migrations/Manual</c>) par son nom de fichier,
    /// par exemple <c>RlsPolicy.sql</c>. Utilisé pour les opérations déclenchées explicitement par un administrateur.
    /// </summary>
    [SuppressMessage("Security", "CA2100", Justification = "Le script manuel est résolu depuis des ressources embarquées avant exécution.")]
    public async Task ApplyManualScriptAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("La chaîne de connexion 'DefaultConnection' est vide.");
        }

        var assembly = typeof(DatabaseMigrator).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name =>
                name.Contains(".Migrations.Manual.", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new FileNotFoundException($"Script de migration manuel introuvable : {fileName}");
        }

        var script = await ReadResourceAsync(assembly, resourceName);
        if (string.IsNullOrWhiteSpace(script))
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var batch in SplitBatches(script))
        {
            using var command = connection.CreateCommand();
#pragma warning disable CA2100
            command.CommandText = batch;
#pragma warning restore CA2100
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    [SuppressMessage("Security", "CA2100", Justification = "Le nom de base provient de la chaîne de connexion et est uniquement réinjecté après échappement des crochets.")]
    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return;
        }

        var masterBuilder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var safeName = databaseName.Replace("]", "]]");
        using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(@name) IS NULL EXEC('CREATE DATABASE [{safeName}]');";
        command.Parameters.Add(new SqlParameter("@name", databaseName));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> ReadResourceAsync(Assembly assembly, string resourceName)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static IEnumerable<string> SplitBatches(string script)
    {
        var batches = GoSeparatorRegex().Split(script);
        foreach (var batch in batches)
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                yield return batch;
            }
        }
    }

    private static string ToFriendlyName(string resourceName)
    {
        const string marker = ".Migrations.";
        var index = resourceName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? resourceName[(index + marker.Length)..] : resourceName;
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparatorRegex();
}
