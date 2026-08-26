# Copilot Instructions

## Project Directives
- Dans Rgpd.Analyzer, toutes les chaînes de caractères doivent être déplacées dans des constantes.
- Dans ce dépôt RGPD, les bases légales doivent être modélisées en enum `LegalBasis` avec les valeurs Consent, Contract, LegalObligation, VitalInterests, PublicTask et LegitimateInterest. Les bases légales doivent correspondre aux bases légales prévues par le RGPD, et une incohérence rôle/finalité/base légale doit générer une erreur de compilation.
- Dans ce dépôt RGPD, les buts de traitement et les rôles d'accès doivent être typés via des enum côté application plutôt que manipulés comme chaînes libres.
- Dans ce dépôt, un fichier doit contenir une seule classe, enum ou record, et le nom du fichier doit correspondre au nom de la classe, de l'enum ou du record qu'il contient.
- Dans ce dépôt, les méthodes privées dans les analyseurs doivent être extraites en contrats de services injectables pour éviter de polluer l’analyseur.
- Dans ce dépôt, les services et leurs contrats doivent être placés dans un dossier Services.
- Dans ce dépôt, l’analyseur LOC doit avoir des pourcentages séparés par projet et au global, ainsi que des limites distinctes par classe et par méthode.