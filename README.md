# Rgpd.NET

Exemple de mise en conformité RGPD en .NET 10 autour d'un scénario simple : 
afficher des données personnelles, enregistrer les consentements par finalité,
propager le contexte RGPD jusqu'à SQL Server et masquer les données lorsque l'utilisateur n'a pas les droits ou les consentements requis.

## Ce que fait l'application

La solution illustre une approche de conformité RGPD en plusieurs couches :

- **dans l'application web**, les consentements actifs de l'utilisateur sont relus à chaque requête ;
- **dans le pipeline HTTP**, ces consentements sont transformés en claims et en rôle SQL effectif ;
- **dans l'accès aux données**, chaque commande SQL injecte le rôle RGPD dans `SESSION_CONTEXT` ;
- **dans l'interface**, les champs marqués comme données personnelles sont masqués automatiquement si la finalité n'est pas autorisée ;
- **dans SQL Server**, l'application d'administration permet de préparer les métadonnées de masquage, les rôles et les politiques RLS.

En pratique, l'application principale affiche un profil utilisateur de démonstration avec deux données personnelles :

- l'email, associé à la finalité **Marketing** ;
- le téléphone, associé à la finalité **Analytics**.

L'utilisateur peut activer ou retirer ces consentements depuis l'interface. Selon les consentements présents et le rôle applicatif courant, les données restent visibles ou sont partiellement masquées.

## Fonctionnement détaillé

### 1. Authentification de démonstration

`Rgpd.Web` expose un endpoint de démonstration :

- `/account/login-demo?role=Marketing`
- `/account/login-demo?role=Analytics`
- `/account/login-demo?role=DataOfficer`

Ce point d'entrée crée une authentification par cookie et injecte notamment :

- un `NameIdentifier` utilisateur ;
- un rôle applicatif (`ClaimTypes.Role`).

Cela permet de simuler rapidement différents profils d'accès sans fournisseur d'identité externe.

### 2. Lecture des consentements

Sur la page d'accueil, `HomeController` détermine l'identifiant utilisateur courant puis lit les finalités actives via `IConsentRepository`.

Les consentements sont stockés dans la table `ConsentRecord` avec deux finalités gérées dans l'exemple :

- `Marketing`
- `Analytics`

L'action `SaveConsent` fait un `MERGE` SQL pour créer ou mettre à jour le consentement correspondant :

- si la case est cochée, `ConsentedAt` est renseigné et `WithdrawnAt` est remis à `NULL` ;
- si la case est décochée, `WithdrawnAt` est renseigné et le consentement est considéré retiré.

### 3. Enrichissement du principal utilisateur

Le middleware `ConsentClaimsMiddleware` relit les consentements actifs et enrichit `HttpContext.User` avec des claims du type :

- `rgpd:purpose:marketing=true`
- `rgpd:purpose:analytics=true`

Le même middleware résout ensuite un **rôle SQL** via `ISqlRoleMapper` :

- `DataOfficer` si l'utilisateur porte ce rôle ;
- sinon `Marketing` si cette finalité est active ;
- sinon `Analytics` si cette finalité est active ;
- sinon `Anonymous`.

Le middleware `SqlRoleContextMiddleware` copie ensuite ce rôle dans `HttpContext.Items["RgpdSqlRole"]` pour qu'il soit accessible à l'infrastructure d'accès aux données.

### 4. Propagation du contexte RGPD jusqu'à SQL Server

`Rgpd.Infrastructure` centralise la création des commandes SQL via `SqlCommandFactory.CreateFilterCommand(...)`.

Chaque commande générée préfixe le SQL métier par :

```sql
EXEC sp_set_session_context @key=N'rgpd_role', @value=@rgpdRole;
```

Le rôle transmis provient de `IRgpdRoleAccessor`, qui récupère la valeur depuis :

1. `HttpContext.Items["RgpdSqlRole"]` ;
2. à défaut, le claim `rgpd:sql-role` ;
3. sinon le rôle ASP.NET ;
4. sinon `Anonymous`.

L'objectif est de faire descendre le contexte de sécurité RGPD de la couche web jusqu'à la session SQL, afin de pouvoir appliquer ensuite du filtrage ou des politiques SQL dépendantes de ce rôle.

### 5. Masquage applicatif dans l'interface

Le modèle `UserProfile` annote les données personnelles avec `[PersonalData(Purpose = "...")]` :

- `Email` → `Marketing`
- `PhoneNumber` → `Analytics`

Dans la vue, les valeurs sensibles sont rendues via le tag helper :

```html
<rgpd-field asp-for="Profile.Email" />
```

Le `FieldTagHelper` :

- lit l'attribut `PersonalData` de la propriété ;
- récupère l'utilisateur courant ;
- délègue à `IDataMaskingService` la décision d'afficher la valeur complète ou masquée.

Le service `DataMaskingService` applique les règles suivantes :

- si l'utilisateur possède le claim de finalité requis, la valeur est affichée en clair ;
- si l'utilisateur est dans le rôle `DataOfficer`, la valeur est affichée en clair ;
- sinon la valeur est masquée partiellement.

Exemples :

- un email devient de la forme `d***@example.com` ;
- un numéro ou une chaîne devient `****` ou une version tronquée ne laissant visibles que les derniers caractères.

### 6. Préparation SQL et administration RGPD

`Rgpd.SqlSetup` est une seconde application web dédiée à l'administration technique de la base.

Elle permet :

#### Gestion des champs personnels

L'écran **Fields** permet :

- de déclarer une table et une colonne comme champ personnel ;
- d'associer une fonction de masquage SQL ;
- d'enregistrer la finalité RGPD (clé étrangère vers `Purpose`) et les rôles autorisés dans la table `RgpdField`.

Le service d'administration applique un `ALTER TABLE ... ADD MASKED` puis met à jour la métadonnée applicative `RgpdField` (en créant la finalité dans `Purpose` si elle n'existe pas).

#### Synchronisation des politiques RLS

L'écran **Policies** permet de créer/synchroniser une politique RLS SQL Server.

Le service `SyncRlsPoliciesAsync` :

- crée la fonction `dbo.fn_rgpd_consent_predicate` si elle n'existe pas ;
- crée la policy `dbo.rgpd_consent_policy` si elle n'existe pas ;
- applique un filtre sur `dbo.Consent` en fonction de `SESSION_CONTEXT('rgpd_role')` et de la liaison `RolePurpose`.

Cela montre comment brancher le rôle RGPD courant sur des mécanismes SQL natifs de filtrage.

##### Détail : `dbo.fn_rgpd_consent_predicate`

`fn_rgpd_consent_predicate` est une **fonction de prédicat RLS** (Row-Level Security) utilisée par SQL Server pour décider si une ligne de `dbo.Consent` peut être renvoyée par une requête.

La fonction reçoit l'identifiant de finalité de la ligne (`@PurposeId`) et ne laisse passer la ligne que dans deux cas :

1. le rôle SQL courant stocké dans `SESSION_CONTEXT(N'rgpd_role')` vaut `DataOfficer` ;
2. ou ce rôle SQL est associé à la finalité de la ligne via les tables `Role` et `RolePurpose`.

En pratique :

- si le rôle courant est `DataOfficer`, toutes les lignes de `dbo.Consent` sont visibles ;
- sinon, SQL Server compare le rôle courant à la finalité de la ligne ;
- si aucune correspondance n'existe, la ligne est automatiquement filtrée par SQL Server.

Le point important est que cette décision est prise **dans la base**, au moment de l'exécution de la requête, et non uniquement dans le code applicatif.

##### Détail : `dbo.rgpd_consent_policy`

`rgpd_consent_policy` est la **politique de sécurité RLS** qui attache `fn_rgpd_consent_predicate` à la table `dbo.Consent`.

La policy est créée avec un **FILTER PREDICATE** :

- elle appelle `dbo.fn_rgpd_consent_predicate(PurposeId)` pour chaque ligne de `dbo.Consent` ;
- elle est activée avec `STATE = ON` ;
- elle agit comme un filtre transparent, sans `WHERE` manuel dans les requêtes applicatives.

Effet pratique :

- toute requête sur `dbo.Consent` est automatiquement restreinte par SQL Server ;
- le développeur n'a pas besoin d'ajouter manuellement un `WHERE PurposeId = ...` dans chaque requête ;
- si le contexte de session RGPD a bien été initialisé par `SqlCommandFactory`, le filtrage suit automatiquement le rôle RGPD courant.

Autrement dit :

- `fn_rgpd_consent_predicate` contient la **règle de décision** ;
- `rgpd_consent_policy` applique cette règle à la table concernée de façon **transparente et systématique**.

#### Mapping rôles applicatifs ↔ rôles SQL

L'écran **RoleMappings** permet :

- d'associer un rôle applicatif à un rôle SQL ;
- de rattacher une finalité au rôle (liaison `RolePurpose`) ;
- de créer automatiquement le rôle SQL s'il n'existe pas.

Ces informations sont stockées dans les tables `Role` et `RolePurpose`.

## Structure de la solution

### `Rgpd.Web`

Application web principale de démonstration.

Responsabilités :

- authentification de démonstration par cookie ;
- lecture et mise à jour des consentements ;
- enrichissement du `ClaimsPrincipal` avec les finalités RGPD ;
- résolution du rôle SQL courant ;
- affichage masqué ou non des données personnelles dans l'UI.

### `Rgpd.Infrastructure`

Brique technique commune pour l'accès aux données, la sécurité RGPD et les migrations de base.

Contient notamment :

- `SqlCommandFactory` pour imposer l'injection du contexte SQL ;
- `RgpdRepository<T>` comme base pour les repositories ADO.NET ;
- `PersonalDataAttribute` pour annoter les propriétés sensibles ;
- `HttpContextRgpdRoleAccessor` pour récupérer le rôle RGPD effectif ;
- `Migrations/` : schéma SQL idempotent et `DatabaseMigrator` (voir [Migrations](Rgpd.Infrastructure/Migrations/README.md)).

### `Rgpd.SqlSetup`

Application d'administration SQL pour préparer la base et synchroniser les mécanismes de protection.

Responsabilités :

- gestion des métadonnées `RgpdField` (rattachées à `Purpose`) ;
- synchronisation de la policy RLS sur `Consent` ;
- gestion des rôles (`Role`) et de leurs finalités (`RolePurpose`).

### `Rgpd.Analyzer`

Analyseur Roslyn de garde-fou.

Il interdit l'usage direct de `new SqlCommand()` hors de la factory RGPD et remonte le diagnostic `RGPD001`.

But :

- empêcher les contournements accidentels de `SqlCommandFactory` ;
- imposer un point d'entrée unique pour l'injection de `SESSION_CONTEXT`.

Le projet contient également un code fix proposant le remplacement par `_sqlCommandFactory.CreateFilterCommand(...)`.

### `Rgpd.Analyzer.Tests`

Projet de tests de l'analyseur Roslyn.

## Parcours utilisateur type

1. L'utilisateur se connecte en mode démo avec un rôle.
2. La page d'accueil charge son profil et ses consentements actifs.
3. Les middlewares enrichissent le principal avec les finalités RGPD et le rôle SQL dérivé.
4. Les composants d'accès aux données exécutent les requêtes avec `SESSION_CONTEXT('rgpd_role')` initialisé.
5. L'interface affiche les données sensibles en clair ou masquées selon les finalités consenties.
6. L'utilisateur coche ou décoche les consentements, qui sont persistés dans la base.
7. L'administrateur peut préparer la base via `Rgpd.SqlSetup` pour aligner métadonnées, rôles et politiques SQL.

## Ce que démontre cet exemple

Cette solution ne cherche pas à couvrir tout le RGPD. Elle montre surtout une architecture cohérente pour relier :

- **consentement utilisateur** ;
- **rôles applicatifs** ;
- **claims métier RGPD** ;
- **masquage côté interface** ;
- **contexte de session SQL** ;
- **politiques SQL Server**.

Autrement dit, l'application illustre comment faire circuler une décision de conformité depuis l'expérience utilisateur jusqu'à la base de données, sans laisser l'accès SQL contourner les règles définies au niveau applicatif.

## Configuration

Les deux applications web utilisent par défaut la même connexion LocalDB :

```json
Server=(localdb)\\MSSQLLocalDB;Database=RgpdNet;Trusted_Connection=True;TrustServerCertificate=True
```

## Build et tests

Depuis la racine du dépôt :

```bash
dotnet build
dotnet test Rgpd.Analyzer.Tests/Rgpd.Analyzer.Tests.csproj
```
