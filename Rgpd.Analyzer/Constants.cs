namespace Rgpd.Analyzer
{
    internal static class Constants
    {
        internal const string AnalyticsPurpose = "Analytics";
        internal const string AuthorizedPurposeAttributeMetadataName = "Rgpd.Infrastructure.Security.AuthorizedPurposeAttribute";
        internal const string ConsentLegalBasisName = "Consent";
        internal const string SqlCommand = "SqlCommand";
        internal const string ContractLegalBasisName = "Contract";
        internal const string OrderTrackingPurpose = "OrderTracking";
        internal const string SecurityCategory = "Security";
        internal const string DataOfficerRole = "DataOfficer";
        internal const string LegalObligationLegalBasisName = "LegalObligation";
        internal const string LegitimateInterestLegalBasisName = "LegitimateInterest";
        internal const string MarketingPurpose = "Marketing";
        internal const string PersonalDataAttributeName = "PersonalData";
        internal const string PublicTaskLegalBasisName = "PublicTask";
        internal const string UseSqlCommandFactoryTitle = "Use SqlCommandFactory for RGPD filtering";
        internal const string UseSqlCommandFactoryMessage = "Error RGPD001 : For RGPD compliance, you must use 'SqlCommandFactory.CreateFilterCommand' instead of 'new SqlCommand()'.";
        internal const string VitalInterestsLegalBasisName = "VitalInterests";
        internal const string AvoidSqlConcatenationTitle = "Avoid SQL string concatenation";
        internal const string AvoidSqlConcatenationMessage = "Error RGPD002 : Do not build SQL with string concatenation or interpolation for '{0}'. Use parameters or a fixed SQL template.";
        internal const string PersonalDataPolicyDiagnosticId = "RGPD003";
        internal const string PersonalDataPolicyTitle = "Validate role, purpose and legal basis consistency";
        internal const string PersonalDataPolicyMessage = "Error RGPD003 : Incoherent personal data policy for '{0}'. {1}";
        internal const string LegalBasisPurposeMismatchMessage = "Purpose '{0}' is not allowed with legal basis '{1}'.";
        internal const string NoMatchingPersonalDataPolicyMessage = "Property '{0}' does not declare a PersonalData policy matching purpose '{1}', legal basis '{2}' and role '{3}'.";
        internal const string SqlTargetNameRegexPattern = @"(?:^|(?:_|(?<![A-Z])[A-Z]))(?:sql|ddl|query|commandtext)(?:$|(?:_|(?=[A-Z]))|(?<![a-z]))";
        internal const string SqlCommandFactoryName = "SqlCommandFactory";
        internal const string SqlCommandFactoryCreateFilterCommand = "CreateFilterCommand";
        internal const string MicrosoftDataSqlClientNamespace = "Microsoft.Data.SqlClient";
        internal const string PersonalDataAttributeMetadataName = "Rgpd.Infrastructure.Security.PersonalDataAttribute";
        internal const string LegalBasisMetadataName = "Rgpd.Infrastructure.Security.LegalBasis";
        internal const string RgpdSecurityConstantsMetadataName = "Rgpd.Infrastructure.Security.RgpdSecurityConstants";
        internal const string SystemDataSqlClientNamespace = "System.Data.SqlClient";
        internal const string SystemDataNamespace = "System.Data";
        internal const string DbCommandInterfaceName = "IDbCommand";
        internal const string StringEmpty = "";
        internal const string AllowedRolePurposesByLegalBasisName = "AllowedRolePurposesByLegalBasis";
        internal const string CodeFixTitle = "Use SqlCommandFactory.CreateFilterCommand";
        internal const string CodeFixEquivalenceKey = "UseSqlCommandFactory";
        internal const string SqlCommandFactoryFieldName = "_sqlCommandFactory";
        internal const string ConnectionIdentifier = "connection";
        internal const string CreateFilterCommandIdentifier = "CreateFilterCommand";
        internal const string RgpdRoleParameterName = "@rgpdRole";
        internal const string RgpdRoleKey = "rgpd_role";
    }
}
