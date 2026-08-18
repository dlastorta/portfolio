#!/usr/bin/env python3
"""
Plan -> Implement -> Validate orchestrator (distilled reference implementation).

This is a readable, dependency-free distillation of a production orchestrator I ran
against a large .NET/React codebase. The real thing additionally integrated an issue
tracker, ran stages in cloud containers, and published branches automatically; that
plumbing is omitted here so the *workflow* is easy to read. What remains is the part
that matters:

    1. PLAN      - agent researches the repo and writes an implementation plan (no code)
    2. REVIEW    - a human approves the plan, or sends it back with notes to re-plan
    3. IMPLEMENT - agent executes the approved plan, writes code + tests, verifies the build
    4. VALIDATE  - a separate, adversarial agent reviews against acceptance criteria

    Loop 1<->2 until the plan is approved; then loop 3<->4 on FAIL, carrying feedback
    forward, until PASS or a cap is hit.

The AI agent itself is pluggable: pass any CLI via --agent-cmd. The orchestrator only
templates prompts, injects the repo's .cursor/rules, gates on human approval, loops on
the validator's verdict, and records timing metrics.

Stdlib only. Python 3.9+.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shlex
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
PROMPTS_DIR = SCRIPT_DIR.parent / "prompts"


# --------------------------------------------------------------------------- #
# Project-type detection -> role selection
# --------------------------------------------------------------------------- #
def detect_project_type(repo: Path) -> str:
    """A .sln/.csproj at the root means backend; a package.json means frontend."""
    if any(repo.glob("*.sln")) or any(repo.glob("*.csproj")):
        return "be"
    if (repo / "package.json").exists():
        return "fe"
    return "generic"


# --------------------------------------------------------------------------- #
# Greenfield detection
# --------------------------------------------------------------------------- #
# A "greenfield" repo has essentially no source code yet — the target of a
# from-scratch build rather than a feature-add to an existing codebase. The
# distinction matters because the Plan stage's default posture is "research
# the codebase to understand X"; on a greenfield repo there is no codebase,
# and the agent either fabricates context (bad) or falls back to role-file
# defaults (also bad when we've deliberately made roles minimal).
#
# Heuristic: count source files under the repo, excluding output artifacts,
# tool caches, and the workflow's own scratch dirs. Threshold 3 — a repo with
# fewer than three source files is functionally a bootstrap target.
_GREENFIELD_EXCLUDED_DIRS = {
    ".git", ".cursor", "output", "bin", "obj", "node_modules",
    ".vs", ".idea", "TestResults", "coveragereport", ".venv", "__pycache__",
}
_GREENFIELD_SOURCE_EXTENSIONS = {
    ".cs", ".vb", ".fs",         # .NET
    ".ts", ".tsx", ".js", ".jsx", ".mjs",  # JS/TS
    ".py",                        # Python
    ".go", ".java", ".kt", ".scala",
    ".rs", ".rb", ".php", ".swift",
}
_GREENFIELD_THRESHOLD = 3


def detect_greenfield(repo: Path) -> bool:
    """True if the repo has fewer than THRESHOLD real source files (excluding tool caches)."""
    count = 0
    for path in repo.rglob("*"):
        if not path.is_file():
            continue
        if path.suffix not in _GREENFIELD_SOURCE_EXTENSIONS:
            continue
        if any(part in _GREENFIELD_EXCLUDED_DIRS for part in path.parts):
            continue
        count += 1
        if count >= _GREENFIELD_THRESHOLD:
            return False
    return True


def project_context_hint(is_greenfield: bool) -> str:
    """Explicit instruction injected into the Plan prompt when the repo is greenfield.

    Empty string when the repo has existing code — the default plan-role instruction
    ("research the codebase to understand X") already covers that case.
    """
    if not is_greenfield:
        return ""
    return (
        "\n\n**Project context: GREENFIELD build.** The target repo has fewer than "
        f"{_GREENFIELD_THRESHOLD} source files — there is no existing codebase to "
        "research for conventions or patterns. Do NOT invent conventions or fall "
        "back to role-file defaults. Your context sources, in priority order, are: "
        "(1) `tech-stack.md` at repo root (or `.cursor/rules/tech-stack.md`), "
        "(2) `.cursor/rules/*` files, "
        "(3) the ticket's Technical notes section. "
        "If none of these declare a library for a category your plan needs, "
        "propose the library in the plan and mark it explicitly as 'needs "
        "reviewer decision' — do not pick silently. The Review gate exists so "
        "these decisions get human approval before implementation.\n"
    )


def load_role(stage: str, project_type: str) -> str:
    """stage is 'plan' or 'implement'; validate uses a fixed QA persona in its template."""
    candidate = PROMPTS_DIR / "roles" / f"{stage}-{project_type}.txt"
    if not candidate.exists():
        candidate = PROMPTS_DIR / "roles" / "generic.txt"
    return candidate.read_text(encoding="utf-8").strip()


# --------------------------------------------------------------------------- #
# .cursor/rules injection - the codebase conventions, fed into every stage
# --------------------------------------------------------------------------- #
def read_cursor_rules(repo: Path) -> str:
    rules_dir = repo / ".cursor" / "rules"
    if not rules_dir.is_dir():
        return ""
    blocks = []
    for rule_file in sorted(rules_dir.rglob("*")):
        if rule_file.is_file() and rule_file.suffix in {".md", ".mdc", ".txt"}:
            blocks.append(f"# --- {rule_file.name} ---\n{rule_file.read_text(encoding='utf-8')}")
    if not blocks:
        return ""
    joined = "\n\n".join(blocks)
    return (
        "\n\nThis project defines coding conventions in .cursor/rules/. "
        "Follow them exactly:\n\n" + joined
    )


# --------------------------------------------------------------------------- #
# Prompt templating
# --------------------------------------------------------------------------- #
def render(template: str, values: dict[str, str]) -> str:
    for key, value in values.items():
        template = template.replace("{{" + key + "}}", value)
    # Strip any placeholders that weren't provided this run.
    return re.sub(r"\{\{[A-Z_]+\}\}", "", template)


def load_template(name: str) -> str:
    return (PROMPTS_DIR / name).read_text(encoding="utf-8")


# --------------------------------------------------------------------------- #
# Running one stage through the pluggable agent CLI
# --------------------------------------------------------------------------- #
def run_stage(agent_cmd: str, prompt_text: str, repo: Path, prompt_path: Path) -> int:
    """
    Write the fully-rendered prompt to a file and invoke the agent CLI.

    --agent-cmd is a template; {prompt_file} and {repo} are substituted, e.g.:
        "claude -p \"$(cat {prompt_file})\""
        "cursor-agent --cwd {repo} --prompt-file {prompt_file}"
    The agent is expected to read the prompt, edit files in `repo`, and write the
    output artifact (plan.md / summary / validation report) the prompt names.

    Note on shlex.split posix mode: on Windows, POSIX-mode shlex (the default)
    treats backslashes as escape characters and strips them from paths — a path
    like C:\\Users\\dare2\\... becomes C:Usersdare2... and the target CLI fails
    to find the prompt file. Setting posix=False on Windows preserves backslash
    literals while still splitting on whitespace and respecting quoted arguments.
    On POSIX shells, keep default behavior.
    """
    prompt_path.write_text(prompt_text, encoding="utf-8")
    cmd = agent_cmd.format(prompt_file=str(prompt_path), repo=str(repo))
    print(f"  $ {cmd}")
    completed = subprocess.run(shlex.split(cmd, posix=(os.name != "nt")), cwd=str(repo))
    return completed.returncode


# --------------------------------------------------------------------------- #
# Metrics
# --------------------------------------------------------------------------- #
def record_metric(metrics_path: Path, entry: dict) -> None:
    data = []
    if metrics_path.exists():
        data = json.loads(metrics_path.read_text(encoding="utf-8"))
    data.append(entry)
    metrics_path.write_text(json.dumps(data, indent=2), encoding="utf-8")


def timed(metrics_path: Path, stage: str, iteration: int, fn) -> int:
    start = time.monotonic()
    code = fn()
    record_metric(
        metrics_path,
        {
            "stage": stage,
            "iteration": iteration,
            "seconds": round(time.monotonic() - start, 1),
            "ok": code == 0,
            "at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        },
    )
    return code


# --------------------------------------------------------------------------- #
# Main
# --------------------------------------------------------------------------- #
def main() -> int:
    parser = argparse.ArgumentParser(description="Plan -> Implement -> Validate orchestrator")
    parser.add_argument("ticket", help="Ticket key, e.g. ABC-123")
    parser.add_argument("--repo", type=Path, default=Path.cwd(), help="Path to the target repo")
    parser.add_argument(
        "--agent-cmd",
        required=True,
        help="Agent CLI template; use {prompt_file} and {repo}. "
        'E.g. "cursor-agent --cwd {repo} --prompt-file {prompt_file}"',
    )
    parser.add_argument("--project-type", choices=["auto", "be", "fe", "generic"], default="auto")
    parser.add_argument("--max-iterations", type=int, default=3)
    parser.add_argument("--extra", default="", help="One-off extra instructions appended to every stage")
    parser.add_argument("--yes", action="store_true", help="Skip the human plan-approval gate (not recommended)")
    args = parser.parse_args()

    repo = args.repo.resolve()
    out_dir = repo / "output" / args.ticket
    out_dir.mkdir(parents=True, exist_ok=True)

    project_type = detect_project_type(repo) if args.project_type == "auto" else args.project_type
    cursor_rules = read_cursor_rules(repo)
    is_greenfield = detect_greenfield(repo)
    greenfield_hint = project_context_hint(is_greenfield)
    greenfield_label = "greenfield" if is_greenfield else "existing codebase"
    print(f"Ticket {args.ticket} | project type: {project_type} | context: {greenfield_label} | repo: {repo}")

    plan_path = out_dir / "plan.md"
    summary_path = out_dir / "implementation-summary.md"
    metrics_path = out_dir / "metrics.json"

    # The ticket data (title, description, acceptance criteria) is expected here.
    ticket_file = out_dir / "ticket-data.md"
    if not ticket_file.exists():
        ticket_file.write_text(
            f"# {args.ticket}\n\n(Paste the ticket title, description, and acceptance criteria here.)\n",
            encoding="utf-8",
        )
        print(f"! Populate {ticket_file} with the ticket details, then re-run.")
        return 2
    ticket_context = f"Read the ticket details (title, description, acceptance criteria) from: {ticket_file}"

    base = {
        "TICKET_CONTEXT": ticket_context,
        "TICKET_KEY": args.ticket,
        "PLAN_PATH": str(plan_path),
        "SUMMARY_PATH": str(summary_path),
        "CURSOR_RULES": cursor_rules,
        "PROJECT_CONTEXT_HINT": greenfield_hint,
        "EXTRA_INSTRUCTIONS": args.extra,
    }

    prompt_scratch = out_dir / "_prompt.txt"

    # ---- Stages 1 & 2: Plan <-> Review --------------------------------------
    # Plan writes plan.md; Review is a human gate. A send-back re-runs Plan with the
    # reviewer's notes injected ({{REPLAN_CONTEXT}}), so nothing is implemented until
    # a plan is approved.
    replan_context = ""
    plan_round = 0
    while True:
        print("\n== PLAN ==" if plan_round == 0 else f"\n== PLAN (re-plan {plan_round}) ==")
        plan_prompt = render(
            load_template("plan-prompt.txt"),
            {**base, "ROLE": load_role("plan", project_type), "REPLAN_CONTEXT": replan_context},
        )
        if timed(
            metrics_path, "plan", plan_round,
            lambda: run_stage(args.agent_cmd, plan_prompt, repo, prompt_scratch),
        ) != 0:
            print("Plan stage failed.")
            return 1

        # Review: the human approval gate.
        if args.yes:
            break
        print(f"\n== REVIEW ==\nRead the plan at {plan_path}.")
        decision = input("[a]pprove / [r]evise (send back to plan) / [q]uit: ").strip().lower()
        if decision in {"a", "approve", "y", "yes"}:
            break
        if decision in {"q", "quit", "n", "no"}:
            print("Aborted at review.")
            return 1
        notes = input("Revision notes for the re-plan: ").strip()
        replan_context = (
            "\n\nA reviewer sent this plan back. Revise the plan to address the following, "
            "then stop again for review:\n" + notes
        )
        plan_round += 1

    # ---- Stages 3 & 4: Implement <-> Validate loop ---------------------------
    feedback = ""
    for iteration in range(1, args.max_iterations + 1):
        print(f"\n== IMPLEMENT (iteration {iteration}) ==")
        implement_prompt = render(
            load_template("implement-prompt.txt"),
            {
                **base,
                "ROLE": load_role("implement", project_type),
                "FEEDBACK": feedback,
            },
        )
        if timed(
            metrics_path, "implement", iteration,
            lambda: run_stage(args.agent_cmd, implement_prompt, repo, prompt_scratch),
        ) != 0:
            print("Implement stage failed.")
            return 1

        print(f"\n== VALIDATE (iteration {iteration}) ==")
        validation_path = out_dir / f"validation-{iteration}.md"
        validate_prompt = render(
            load_template("validate-prompt.txt"),
            {**base, "VALIDATION_PATH": str(validation_path), "ITERATION": str(iteration)},
        )
        if timed(
            metrics_path, "validate", iteration,
            lambda: run_stage(args.agent_cmd, validate_prompt, repo, prompt_scratch),
        ) != 0:
            print("Validate stage failed.")
            return 1

        verdict = validation_path.read_text(encoding="utf-8") if validation_path.exists() else ""
        if "OVERALL VERDICT: PASS" in verdict:
            print(f"\nPASS on iteration {iteration}. Hand off to a human for final review + merge.")
            return 0

        print(f"FAIL on iteration {iteration}; feeding the report back into implement.")
        feedback = (
            "The previous implementation did not pass validation. Address every item in this report:\n\n"
            + verdict
        )

    print(f"\nStill failing after {args.max_iterations} iterations. Needs a human.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
