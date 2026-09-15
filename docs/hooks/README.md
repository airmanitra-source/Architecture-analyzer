# Capturer le prompt pour l'historique d'impact

L'historique d'impact (`architecture_analyzer.impact_history_enabled = true`) écrit une ligne
dans `.architecture/impact-history.csv` à chaque changement, avec le **prompt** qui l'a causé.

La tâche MSBuild ne connaît pas le prompt : il vit dans ton outil d'IA. Le pont est un fichier.

## Le contrat

L'analyseur lit le prompt, dans cet ordre :

1. `.architecture/current-prompt.txt` à la racine du dépôt ;
2. sinon la variable d'environnement `ARCHITECTURE_ANALYZER_PROMPT`.

Tout ce qui écrit l'un des deux convient. Le hook ci-dessous est simplement l'automatisation
pour Claude Code.

## Claude Code

1. Copie `capture-prompt.ps1` (Windows, recommandé) ou `capture-prompt.sh` (Bash + `jq`) dans
   `.claude/hooks/` de ton dépôt.
2. Fusionne `settings.snippet.json` dans `.claude/settings.json` (partagé, versionné) ou
   `.claude/settings.local.json` (local, non versionné).
3. Ajoute `.architecture/` à ton `.gitignore` : l'historique est local par conception.

Le hook `UserPromptSubmit` reçoit le prompt (`user_prompt`), la session (`session_id`) et le
répertoire (`cwd`) en JSON sur l'entrée standard, et `${CLAUDE_PROJECT_DIR}` pointe la racine du
projet. Il écrit le prompt et sort en code 0 : il ne bloque jamais et ne modifie rien d'autre.

## Autre outil, ou build manuel

Écris le prompt toi-même avant de builder :

```powershell
$env:ARCHITECTURE_ANALYZER_PROMPT = "ajoute la rotation de session"
dotnet build
```

Sans prompt ni variable, la ligne est quand même écrite, avec un prompt vide.
