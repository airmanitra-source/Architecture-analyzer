using System;

namespace Rgpd.Infrastructure.Security;

public sealed class PurposeMisuseExample
{
    // SCÉNARIO CONFORME :
    // L'email est exploité pour la finalité Marketing,
    // avec la base légale Consent, par le rôle Marketing.
    [AuthorizedPurpose(Purpose.Marketing, LegalBasis.Consent, AccessRole.Marketing)]
    public void EnvoyerNewsletter(DemoUserProfile user)
    {
        // Dans l'application actuelle, cela correspond à :
        // - purpose = Marketing
        // - legal basis = Consent
        // - access role = Marketing
        Console.WriteLine(user.Email);
    }

    // SCÉNARIO CONFORME :
    // Le téléphone est consulté pour Analytics,
    // avec la base légale LegitimateInterest, par le rôle Analytics.
    [AuthorizedPurpose(Purpose.Analytics, LegalBasis.LegitimateInterest, AccessRole.Analytics)]
    public void AnalyserUsageTelephone(DemoUserProfile user)
    {
        Console.WriteLine(user.PhoneNumber);
    }

    // SCÉNARIO CONFORME :
    // L'e-mail peut aussi être utilisé pour le suivi de commande
    // avec la base légale Contract.
    [AuthorizedPurpose(Purpose.OrderTracking, LegalBasis.Contract, AccessRole.Marketing)]
    public void EnvoyerSuiviCommande(DemoUserProfile user)
    {
        Console.WriteLine(user.Email);
    }

    public sealed class DemoUserProfile
    {
        [PersonalData(Purpose.OrderTracking, LegalBasis.Contract, AccessRole.Marketing)]
        [PersonalData(Purpose.Marketing, LegalBasis.Consent, AccessRole.Marketing)]
        public string Email { get; init; } = string.Empty;

        [PersonalData(Purpose.Analytics, LegalBasis.LegitimateInterest, AccessRole.Analytics)]
        public string PhoneNumber { get; init; } = string.Empty;
    }

    /// <summary>
    /// Détournement de finalité : l'e-mail a été collecté initialement pour
    /// <see cref="Purpose.OrderTracking"/> avec <see cref="LegalBasis.Contract"/>,
    /// puis la méthode le réutilise sous la finalité déclarée
    /// <see cref="Purpose.Marketing"/>, qui représente ici la prospection commerciale.
    /// Article 5.1.b (limitation des finalités)
    /// Article 6.1 (absence de base légale valable pour la nouvelle finalité)
    /// </summary>
    /// <param name="user">Profil contenant l'e-mail personnel réutilisé hors de sa finalité initiale.</param>
#if false
    [AuthorizedPurpose(Purpose.Marketing, LegalBasis.Contract, AccessRole.Marketing)]
    public void RecyclerEmailCommandePourProspection(DemoUserProfile user)
    {
        Console.WriteLine(user.Email);
    }
#endif

    // SCÉNARIO ILLÉGAL :
    // La finalité Marketing est bien liée à Consent dans l'application,
    // mais le rôle exécutant n'est pas cohérent avec la donnée ciblée.
    /// <remarks>
    /// Articles RGPD principalement violés : article 5, paragraphe 1, point a
    /// (licéité, loyauté, transparence), et article 25, paragraphe 1 et 2
    /// (protection des données dès la conception et par défaut).
    /// </remarks>
#if false
    [AuthorizedPurpose(Purpose.Marketing, LegalBasis.Consent, AccessRole.Analytics)]
    public void LancerCampagneMarketingDepuisRoleAnalytics(DemoUserProfile user)
    {
        Console.WriteLine(user.Email);
    }
#endif

    // SCÉNARIO CONFORME / EXCEPTION APPLICATIVE :
    // Dans DataMaskingService, le rôle DataOfficer peut lire en clair.
    [AuthorizedPurpose(Purpose.Marketing, LegalBasis.Consent, AccessRole.DataOfficer)]
    public void AuditerParDataOfficer(DemoUserProfile user)
    {
        Console.WriteLine(user.Email);
    }
}

