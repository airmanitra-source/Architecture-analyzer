#!/usr/bin/env bash
# Architecture.Analyzer — capture du prompt courant (variante Bash, requiert jq).
#
# Equivalent Bash de capture-prompt.ps1. Sur Windows, prefere la version PowerShell :
# pwsh parse le JSON nativement et n'exige aucun outil supplementaire, alors que cette
# version depend de jq.
#
# Ne bloque jamais le prompt : sort toujours en code 0.

set -u

INPUT="$(cat)"

ROOT="${CLAUDE_PROJECT_DIR:-}"
if [ -z "$ROOT" ]; then
  ROOT="$(printf '%s' "$INPUT" | jq -r '.cwd // empty' 2>/dev/null)"
fi
[ -z "$ROOT" ] && exit 0

PROMPT="$(printf '%s' "$INPUT" | jq -r '.user_prompt // empty' 2>/dev/null)"

mkdir -p "$ROOT/.architecture" 2>/dev/null
printf '%s' "$PROMPT" > "$ROOT/.architecture/current-prompt.txt" 2>/dev/null

exit 0
