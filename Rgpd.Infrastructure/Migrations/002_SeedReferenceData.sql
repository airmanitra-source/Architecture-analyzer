/*
	Données de référence (seed) idempotentes.
	Insère les finalités, rôles et liaisons rôle/finalité de démonstration
	seulement si elles n'existent pas déjà.
*/

-- Finalités de base
MERGE dbo.Purpose AS target
USING (VALUES
	(N'Marketing',  N'Communications et finalités marketing'),
	(N'Analytics',  N'Mesure d''audience et analytics')
) AS source (Code, Label)
ON target.Code = source.Code
WHEN NOT MATCHED THEN
	INSERT (Code, Label) VALUES (source.Code, source.Label);

-- Bases légales de base
IF OBJECT_ID('dbo.LegalBasis', 'U') IS NOT NULL
BEGIN
	MERGE dbo.LegalBasis AS target
	USING (VALUES
		(N'Consent', N'Consentement de la personne concernée'),
		(N'Contract', N'Exécution d''un contrat'),
		(N'LegalObligation', N'Respect d''une obligation légale'),
		(N'VitalInterests', N'Sauvegarde des intérêts vitaux'),
		(N'PublicTask', N'Exécution d''une mission d''intérêt public ou relevant de l''autorité publique'),
		(N'LegitimateInterests', N'Intérêts légitimes du responsable du traitement ou d''un tiers')
	) AS source (Code, Label)
	ON target.Code = source.Code
	WHEN MATCHED THEN
		UPDATE SET Label = source.Label
	WHEN NOT MATCHED THEN
		INSERT (Code, Label) VALUES (source.Code, source.Label);
END;

-- Rôles applicatifs et rôle SQL associé
MERGE dbo.[Role] AS target
USING (VALUES
	(N'Anonymous',   N'Anonymous'),
	(N'Marketing',   N'Marketing'),
	(N'Analytics',   N'Analytics'),
	(N'DataOfficer', N'DataOfficer')
) AS source (Name, SqlRole)
ON target.Name = source.Name
WHEN NOT MATCHED THEN
	INSERT (Name, SqlRole) VALUES (source.Name, source.SqlRole);

-- Liaison rôle <-> finalité
-- Marketing -> Marketing ; Analytics -> Analytics ; DataOfficer -> toutes les finalités
MERGE dbo.RolePurpose AS target
USING
(
	SELECT r.RoleId, p.PurposeId
	FROM dbo.[Role] r
	JOIN dbo.Purpose p ON
		(r.Name = N'Marketing'   AND p.Code = N'Marketing') OR
		(r.Name = N'Analytics'   AND p.Code = N'Analytics') OR
		(r.Name = N'DataOfficer')
) AS source (RoleId, PurposeId)
ON target.RoleId = source.RoleId AND target.PurposeId = source.PurposeId
WHEN NOT MATCHED THEN
	INSERT (RoleId, PurposeId) VALUES (source.RoleId, source.PurposeId);
