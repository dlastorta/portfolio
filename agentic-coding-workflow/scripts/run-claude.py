#!/usr/bin/env python3
"""
run-claude.py — wrapper to invoke Claude Code CLI from the workflow orchestrator.

The orchestrator (run_agent.py) uses --agent-cmd with {prompt_file} and {repo}
placeholders, then subprocess.run(shlex.split(cmd)). On Windows this makes it
awkward to invoke a bash wrapper because `bash` resolves to WSL (not Git Bash)
via CreateProcess, and shell substitutions cannot go through shlex.split.

This Python wrapper sidesteps that: Python is portable, `claude` is invoked with
shell=True so Windows resolves the `.cmd`/`.exe` shim correctly, the prompt is
read from the file and passed via stdin, and the working directory is set to
the target repo so Claude Code's file tools operate against it.

Usage: python run-claude.py <prompt_file> <repo>

Prereqs:
    - `claude` CLI installed and authenticated (`claude` interactive once, or
      `claude auth`, or `ANTHROPIC_API_KEY` env var set for API-key auth).
    - The target repo is a directory you trust (permission checks are skipped).
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) < 3:
        print("usage: run-claude.py <prompt_file> <repo>", file=sys.stderr)
        return 2

    prompt_file = Path(sys.argv[1])
    repo = Path(sys.argv[2])

    if not prompt_file.is_file():
        print(f"error: prompt file not found: {prompt_file}", file=sys.stderr)
        return 3
    if not repo.is_dir():
        print(f"error: repo directory not found: {repo}", file=sys.stderr)
        return 3

    prompt = prompt_file.read_text(encoding="utf-8")

    # shell=True on Windows lets us resolve `claude` when it's installed as a
    # .cmd shim (npm-based install). On POSIX shell=True uses /bin/sh, still fine.
    # The prompt goes via stdin — avoids Windows command-line length limits and
    # escaping headaches with quotes/newlines in the prompt body.
    cmd = (
        'claude --print --dangerously-skip-permissions --add-dir "{repo}"'
    ).format(repo=str(repo))

    completed = subprocess.run(
        cmd,
        input=prompt,
        text=True,
        shell=True,
        cwd=str(repo),
    )
    return completed.returncode


if __name__ == "__main__":
    sys.exit(main())
