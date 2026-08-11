#!/bin/bash
# run-claude.sh — invoke Claude Code CLI in non-interactive mode with a prompt file
#
# Usage: run-claude.sh <prompt_file> <repo>
#
# The workflow orchestrator (run_agent.py) uses --agent-cmd with {prompt_file}
# and {repo} placeholders. Because subprocess.run(shlex.split(cmd)) does not
# invoke a shell, shell substitutions ($(cat …), pipes, cd &&) cannot go in the
# --agent-cmd template directly. This wrapper handles them.
#
# What it does:
#   1. cd's into the target repo (so Claude Code's cwd is the repo — its tools
#      operate against the cwd by default).
#   2. Reads the prompt file and passes it as a positional argument.
#   3. Runs Claude in --print (non-interactive) mode with permission checks
#      skipped, so it can edit files and run bash without prompting.
#
# Prereqs:
#   - `claude` CLI installed and authenticated (`claude` interactive once, or
#     `claude auth login`, or `ANTHROPIC_API_KEY` env var set).
#   - Run from a trusted directory (this bypasses permission prompts).

set -euo pipefail

if [ $# -lt 2 ]; then
    echo "usage: $0 <prompt_file> <repo>" >&2
    exit 2
fi

PROMPT_FILE="$1"
REPO="$2"

if [ ! -f "$PROMPT_FILE" ]; then
    echo "error: prompt file not found: $PROMPT_FILE" >&2
    exit 3
fi

if [ ! -d "$REPO" ]; then
    echo "error: repo directory not found: $REPO" >&2
    exit 3
fi

# Move into the target repo so Claude Code's file tools operate against it.
cd "$REPO"

# Read the full prompt as a positional argument. Using file contents via $(…) is
# fine here because this IS a shell — the workflow's shlex.split limitation is
# the parent's problem, not ours.
PROMPT_CONTENT="$(cat "$PROMPT_FILE")"

# --print               : non-interactive; exits after one response
# --dangerously-skip-permissions : allow file edits, bash, etc. without prompts
# --add-dir "$REPO"     : explicit belt-and-suspenders in case cwd is not enough
exec claude \
    --print \
    --dangerously-skip-permissions \
    --add-dir "$REPO" \
    "$PROMPT_CONTENT"
