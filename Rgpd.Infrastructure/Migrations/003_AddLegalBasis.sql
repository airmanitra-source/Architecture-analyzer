/*
	Ajoute la base légale comme référentiel RGPD et rattache chaque champ personnel
	à une finalité, une base légale et une liste de rôles autorisés.
	Script idempotent.
*/

IF OBJECT_ID('dbo.LegalBasis', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.LegalBasis
	(
		LegalBasisId INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_LegalBasis PRIMARY KEY,
		Code         NVARCHAR(64)  NOT NULL CONSTRAINT UQ_LegalBasis_Code UNIQUE,
		Label        NVARCHAR(256) NOT NULL
	);
END;
GO

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
GO

IF COL_LENGTH('dbo.RgpdField', 'LegalBasisId') IS NULL
BEGIN
	DECLARE @defaultLegalBasisId INT;

	SELECT @defaultLegalBasisId = LegalBasisId
	FROM dbo.LegalBasis
	WHERE Code = N'Consent';

	ALTER TABLE dbo.RgpdField
	ADD LegalBasisId INT NULL;

	UPDATE dbo.RgpdField
	SET LegalBasisId = @defaultLegalBasisId
	WHERE LegalBasisId IS NULL;

	ALTER TABLE dbo.RgpdField
	ALTER COLUMN LegalBasisId INT NOT NULL;

	ALTER TABLE dbo.RgpdField
	ADD CONSTRAINT FK_RgpdField_LegalBasis FOREIGN KEY (LegalBasisId) REFERENCES dbo.LegalBasis (LegalBasisId);
END;