/*
	Sécurité au niveau ligne (RLS) sur dbo.Consent.

	Ce script est appliqué à la demande (écran "Politiques RLS" de Rgpd.SqlSetup),
	et NON automatiquement au démarrage, car il modifie le comportement de lecture
	de la table Consent pour toutes les requêtes.

	fn_rgpd_consent_predicate : fonction de prédicat RLS.
	  Reçoit la finalité (PurposeId) de la ligne et autorise la ligne uniquement
	  si le rôle SQL courant (SESSION_CONTEXT('rgpd_role')) couvre cette finalité
	  via la table de liaison RolePurpose. Le rôle 'DataOfficer' voit tout.

	rgpd_consent_policy : politique de sécurité qui attache le prédicat à dbo.Consent.

	Script idempotent.
*/

IF OBJECT_ID('dbo.fn_rgpd_consent_predicate', 'IF') IS NULL
EXEC('
CREATE FUNCTION dbo.fn_rgpd_consent_predicate(@PurposeId INT)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
	SELECT 1 AS fn_securitypredicate_result
	WHERE
		CAST(SESSION_CONTEXT(N''rgpd_role'') AS NVARCHAR(128)) = N''DataOfficer''
		OR EXISTS
		(
			SELECT 1
			FROM dbo.[Role] r
			JOIN dbo.RolePurpose rp ON rp.RoleId = r.RoleId
			WHERE r.SqlRole = CAST(SESSION_CONTEXT(N''rgpd_role'') AS NVARCHAR(128))
			  AND rp.PurposeId = @PurposeId
		);
');

IF NOT EXISTS (SELECT 1 FROM sys.security_policies WHERE name = 'rgpd_consent_policy')
BEGIN
	CREATE SECURITY POLICY dbo.rgpd_consent_policy
		ADD FILTER PREDICATE dbo.fn_rgpd_consent_predicate(PurposeId) ON dbo.Consent
		WITH (STATE = ON);
END;
