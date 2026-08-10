#!/bin/bash
# Runs the AI agent with the given prompt.
# Usage: ./agent.sh "<prompt>"

set -euo pipefail

PROMPT="${1:-}"
if [[ -z "$PROMPT" ]]; then
  echo "ERROR: No prompt provided"
  exit 1
fi

# Default: Claude Code
claude --dangerously-skip-permissions -p "$PROMPT"

# --- To use a local Ollama model instead, comment out the line above
#     and uncomment the block below. Run `ollama serve` first.
#
# MODEL="${OLLAMA_MODEL:-qwen3.5:9b}"
# node "$(dirname "$0")/ollama-agent.js" "$MODEL"
