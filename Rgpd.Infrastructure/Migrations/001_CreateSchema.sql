/*
	Schéma normalisé RGPD.
	Pivot central : Purpose (la finalité).
	- Purpose      : référentiel des finalités
	- Role         : référentiel des rôles applicatifs et leur rôle SQL
	- RolePurpose  : liaison N:N rôle <-> finalité
	- Consent      : consentement utilisateur <-> finalité
	- RgpdField    : métadonnées de masquage d'une colonne, rattachées à une finalité

	Script idempotent : ne crée que ce qui n'existe pas encore.
*/

IF OBJECT_ID('dbo.Purpose', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.Purpose
	(
		PurposeId INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Purpose PRIMARY KEY,
		Code      NVARCHAR(64)  NOT NULL CONSTRAINT UQ_Purpose_Code UNIQUE,
		Label     NVARCHAR(256) NOT NULL
	);
END;

IF OBJECT_ID('dbo.[Role]', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.[Role]
	(
		RoleId  INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Role PRIMARY KEY,
		Name    NVARCHAR(64)  NOT NULL CONSTRAINT UQ_Role_Name UNIQUE,
		SqlRole NVARCHAR(128) NOT NULL
	);
END;

IF OBJECT_ID('dbo.RolePurpose', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.RolePurpose
	(
		RoleId    INT NOT NULL,
		PurposeId INT NOT NULL,
		CONSTRAINT PK_RolePurpose PRIMARY KEY (RoleId, PurposeId),
		CONSTRAINT FK_RolePurpose_Role FOREIGN KEY (RoleId) REFERENCES dbo.[Role] (RoleId),
		CONSTRAINT FK_RolePurpose_Purpose FOREIGN KEY (PurposeId) REFERENCES dbo.Purpose (PurposeId)
	);
END;

IF OBJECT_ID('dbo.Consent', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.Consent
	(
		ConsentId   INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Consent PRIMARY KEY,
		UserId      NVARCHAR(128) NOT NULL,
		PurposeId   INT NOT NULL,
		ConsentedAt DATETIMEOFFSET NULL,
		WithdrawnAt DATETIMEOFFSET NULL,
		CONSTRAINT FK_Consent_Purpose FOREIGN KEY (PurposeId) REFERENCES dbo.Purpose (PurposeId),
		CONSTRAINT UQ_Consent_User_Purpose UNIQUE (UserId, PurposeId)
	);
END;

IF OBJECT_ID('dbo.RgpdField', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.RgpdField
	(
		RgpdFieldId  INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_RgpdField PRIMARY KEY,
		TableName    NVARCHAR(128) NOT NULL,
		ColumnName   NVARCHAR(128) NOT NULL,
		PurposeId    INT NOT NULL,
		AllowedRoles NVARCHAR(256) NOT NULL CONSTRAINT DF_RgpdField_AllowedRoles DEFAULT (N''),
		CONSTRAINT FK_RgpdField_Purpose FOREIGN KEY (PurposeId) REFERENCES dbo.Purpose (PurposeId),
		CONSTRAINT UQ_RgpdField_Table_Column UNIQUE (TableName, ColumnName)
	);
END;
