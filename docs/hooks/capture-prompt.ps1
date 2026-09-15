# Architecture.Analyzer — capture du prompt courant.
#
# Depose le texte du prompt dans .architecture/current-prompt.txt a la racine du depot,
# pour que le build attribue chaque changement au prompt qui l'a cause (historique d'impact, 1A).
#
# Branche sur le hook UserPromptSubmit de Claude Code (voir settings.snippet.json).
# Le contrat, cote analyseur, est uniquement : ce fichier contient le prompt. N'importe quel
# outil ou script qui l'ecrit convient.
#
# Ne bloque jamais le prompt : sort toujours en code 0.

$ErrorActionPreference = 'SilentlyContinue'
try {
    $raw  = [Console]::In.ReadToEnd()
    $data = $raw | ConvertFrom-Json

    $root = $env:CLAUDE_PROJECT_DIR
    if ([string]::IsNullOrEmpty($root)) { $root = $data.cwd }
    if ([string]::IsNullOrEmpty($root)) { exit 0 }

    $dir = Join-Path $root '.architecture'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Set-Content -Path (Join-Path $dir 'current-prompt.txt') -Value $data.user_prompt -NoNewline -Encoding UTF8
}
catch {
    # jamais d'erreur remontee : la capture est un confort, pas une dependance dure.
}

exit 0
