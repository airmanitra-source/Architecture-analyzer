# Copilot Instructions

## Project Directives
- Dans Rgpd.Analyzer, toutes les chaînes de caractères doivent être déplacées dans des constantes.
- Dans ce dépôt RGPD, les bases légales doivent être modélisées en enum `LegalBasis` avec les valeurs Consent, Contract, LegalObligation, VitalInterests, PublicTask et LegitimateInterest. Les bases légales doivent correspondre aux bases légales prévues par le RGPD, et une incohérence rôle/finalité/base légale doit générer une erreur de compilation.
- Dans ce dépôt RGPD, les buts de traitement et les rôles d'accès doivent être typés via des enum côté application plutôt que manipulés comme chaînes libres.