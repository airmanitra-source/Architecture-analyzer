# Migrations RGPD

Ce dossier contient le schéma de base de données RGPD sous forme de **scripts SQL idempotents**
et un **runner** (`DatabaseMigrator`) qui les applique dans l'ordre.

## Contenu

| Fichier | Rôle |
|---------|------|
| `001_CreateSchema.sql` | Crée les tables `Purpose`, `Role`, `RolePurpose`, `Consent`, `RgpdField` (avec clés étrangères). |
| `002_SeedReferenceData.sql` | Insère les finalités, rôles et liaisons rôle/finalité de démonstration. |
| `003_CreateRlsPolicy.sql` | Crée la fonction de prédicat RLS `fn_rgpd_consent_predicate` et la policy `rgpd_consent_policy` sur `dbo.Consent`. |
| `DatabaseMigrator.cs` | Crée la base si absente puis exécute tous les scripts `.sql` embarqués, dans l'ordre alphabétique. |

## Modèle de données

Le pivot du modèle est la **finalité** (`Purpose`).

```
Purpose 1 ────< RolePurpose >──── 1 Role
   │
   ├──< Consent      (UserId, PurposeId)
   └──< RgpdField    (TableName, ColumnName, PurposeId)
```

- `Consent` lie un **utilisateur** à une **finalité** (consentement donné ou retiré).
- `RolePurpose` lie un **rôle** aux **finalités** qu'il couvre (liaison N:N).
- `RgpdField` rattache une **colonne** à une **finalité** pour le masquage.

## Conventions

- **Idempotence** : chaque script ne crée que ce qui n'existe pas (`IF OBJECT_ID ... IS NULL`, `MERGE`).
  Relancer le migrateur est sans effet de bord.
- **Ordre** : les scripts sont préfixés par un numéro (`001_`, `002_`, ...) et appliqués dans cet ordre.
- **Séparateur de lot** : utilisez une ligne `GO` seule pour découper un script en plusieurs lots.

## Ajouter une migration

1. Créez un nouveau fichier `NNN_Description.sql` (numéro suivant).
2. Écrivez du SQL idempotent.
3. Le fichier est automatiquement embarqué (`<EmbeddedResource Include="Migrations\*.sql" />`) et appliqué au prochain run.

## Exécution

Les applications web exécutent le migrateur au démarrage (en environnement de développement).
La chaîne de connexion utilisée est `ConnectionStrings:DefaultConnection`.

Le runner :
1. crée la base de données indiquée dans `Initial Catalog` si elle n'existe pas ;
2. applique les scripts `001` → `003` dans l'ordre.
