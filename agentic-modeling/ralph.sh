#!/bin/bash
# Eventmodelers agent loop — processes tasks.json indefinitely
# Usage: ./ralph.sh [project_dir]

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="${1:-"$SCRIPT_DIR"}"
TASKS_FILE="$PROJECT_DIR/tasks.json"

if [[ ! -f "$PROJECT_DIR/.eventmodelers/config.json" ]]; then
  echo "ERROR: No .eventmodelers/config.json found. Run: eventmodelers install"
  exit 1
fi

echo "Eventmodelers agent — project: $PROJECT_DIR"

while true; do
  cd "$PROJECT_DIR"

  # Skip agent call entirely when no tasks (saves tokens / avoids idle Ollama calls)
  COUNT=$(node -e "
let t = [];
try { t = JSON.parse(require('fs').readFileSync('tasks.json', 'utf8')); } catch {}
console.log(t.length);
" 2>/dev/null || echo 0)

  if [[ "$COUNT" == "0" ]]; then
    sleep 5
    continue
  fi

  attempt=0
  while [[ $attempt -lt 3 ]]; do
    attempt=$((attempt + 1))
    if bash "$SCRIPT_DIR/agent.sh" "Process the next task from tasks.json." 2>&1; then
      break
    fi
    echo "[ralph] agent error (attempt $attempt/3)"
    if [[ $attempt -lt 3 ]]; then
      sleep 10
    else
      echo "[ralph] task failed 3 times — discarding and continuing"
      node -e "
let t = [];
try { t = JSON.parse(require('fs').readFileSync('tasks.json', 'utf8')); } catch {}
t.shift();
require('fs').writeFileSync('tasks.json', JSON.stringify(t, null, 2));
" 2>/dev/null || true
    fi
  done
done
