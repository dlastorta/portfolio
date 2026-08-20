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
# Filesystem snapshots for per-stage file / LOC delta metrics
# --------------------------------------------------------------------------- #
# `metrics.json` used to capture only wall-clock duration per stage. Snapshots
# taken before/after each stage let us also report which files changed and how
# many lines were added/removed — useful for calibrating future runs and for
# writing about the workflow with concrete numbers rather than vibes.
#
# We exclude `output/` from snapshots because that's where the workflow writes
# its own artifacts (plan.md, implementation-summary.md, validation-N.md). A
# stage that produces only workflow artifacts (Plan, Validate) should legitimately
# show 0 files touched — that's a useful signal, not noise.
_SNAPSHOT_EXCLUDED_DIRS = _GREENFIELD_EXCLUDED_DIRS | {"output"}
_LOC_EXTENSIONS = _GREENFIELD_SOURCE_EXTENSIONS | {
    ".md", ".yaml", ".yml", ".json", ".xml",
    ".sql", ".css", ".html", ".sh", ".toml", ".ini",
    ".csproj", ".sln", ".props", ".targets",
}


def _snapshot_repo(repo: Path) -> dict:
    """Map {relative_path: (size_bytes, mtime_ns, line_count_or_none)} for the repo.

    line_count is None for binary / non-source files — we only read text files we
    care about counting lines for. Excludes tool caches and the workflow's own
    output/ dir.
    """
    snapshot: dict = {}
    for path in repo.rglob("*"):
        if not path.is_file():
            continue
        rel_parts = path.relative_to(repo).parts
        if any(part in _SNAPSHOT_EXCLUDED_DIRS for part in rel_parts):
            continue
        try:
            stat = path.stat()
        except OSError:
            continue
        line_count = None
        if path.suffix.lower() in _LOC_EXTENSIONS:
            try:
                with path.open("r", encoding="utf-8", errors="ignore") as fh:
                    line_count = sum(1 for _ in fh)
            except OSError:
                line_count = None
        snapshot[str(path.relative_to(repo))] = (stat.st_size, stat.st_mtime_ns, line_count)
    return snapshot


def _diff_snapshots(before: dict, after: dict) -> dict:
    """Compute created / modified / deleted files and net LOC delta between two snapshots."""
    before_keys = set(before)
    after_keys = set(after)
    created = sorted(after_keys - before_keys)
    deleted = sorted(before_keys - after_keys)
    common = before_keys & after_keys
    # A file is "modified" if size or mtime changed. mtime alone catches
    # touched-without-size-change; size catches content changes even if mtime
    # is somehow preserved (some editors preserve mtime on save).
    modified = sorted(k for k in common if before[k][:2] != after[k][:2])

    loc_delta = 0
    for k in created:
        _, _, lc = after[k]
        if lc is not None:
            loc_delta += lc
    for k in deleted:
        _, _, lc = before[k]
        if lc is not None:
            loc_delta -= lc
    for k in modified:
        _, _, lc_after = after[k]
        _, _, lc_before = before[k]
        if lc_after is not None and lc_before is not None:
            loc_delta += (lc_after - lc_before)

    return {
        "files_touched": len(created) + len(modified) + len(deleted),
        "files_created": created,
        "files_modified": modified,
        "files_deleted": deleted,
        "loc_delta": loc_delta,
    }


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


def timed(metrics_path: Path, stage: str, iteration: int, repo: Path, fn) -> int:
    start = time.monotonic()
    before = _snapshot_repo(repo)
    code = fn()
    after = _snapshot_repo(repo)
    diff = _diff_snapshots(before, after)
    record_metric(
        metrics_path,
        {
            "stage": stage,
            "iteration": iteration,
            "seconds": round(time.monotonic() - start, 1),
            "ok": code == 0,
            "at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
            **diff,
        },
    )
    return code


def write_run_summary(
    out_dir: Path,
    ticket: str,
    project_type: str,
    context_label: str,
    cursor_rules_bytes: int,
    verdict: str,
    iterations_until_pass: int | None,
    started_at: str,
) -> None:
    """Emit run-summary.json aggregating per-stage metrics into portfolio-friendly totals.

    Called at every exit point (PASS, FAIL after max iterations, plan/implement/validate
    stage failure, human abort at Review). Idempotent — safe to overwrite.
    """
    metrics_path = out_dir / "metrics.json"
    summary_path = out_dir / "run-summary.json"
    entries: list = []
    if metrics_path.exists():
        try:
            entries = json.loads(metrics_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            entries = []

    per_stage_seconds: dict = {}
    for e in entries:
        stage = e.get("stage", "unknown")
        per_stage_seconds[stage] = round(per_stage_seconds.get(stage, 0.0) + e.get("seconds", 0.0), 1)

    summary = {
        "ticket": ticket,
        "started_at": started_at,
        "completed_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "project_type": project_type,
        "context": context_label,
        "cursor_rules_bytes": cursor_rules_bytes,
        "verdict": verdict,
        "iterations_until_pass": iterations_until_pass,
        "stages_run": len(entries),
        "total_seconds": round(sum(e.get("seconds", 0.0) for e in entries), 1),
        "per_stage_seconds": per_stage_seconds,
        "total_files_touched": sum(e.get("files_touched", 0) for e in entries),
        "total_loc_delta": sum(e.get("loc_delta", 0) for e in entries),
    }
    summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")


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
    started_at = datetime.now(timezone.utc).isoformat(timespec="seconds")
    print(f"Ticket {args.ticket} | project type: {project_type} | context: {greenfield_label} | repo: {repo}")

    def emit_summary(verdict: str, iterations_until_pass: int | None = None) -> None:
        """Write run-summary.json — called from every exit point."""
        write_run_summary(
            out_dir=out_dir,
            ticket=args.ticket,
            project_type=project_type,
            context_label=greenfield_label,
            cursor_rules_bytes=len(cursor_rules.encode("utf-8")),
            verdict=verdict,
            iterations_until_pass=iterations_until_pass,
            started_at=started_at,
        )

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
            metrics_path, "plan", plan_round, repo,
            lambda: run_stage(args.agent_cmd, plan_prompt, repo, prompt_scratch),
        ) != 0:
            print("Plan stage failed.")
            emit_summary(verdict="PLAN_FAILED")
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
            emit_summary(verdict="ABORTED_AT_REVIEW")
            return 1
        notes = input("Revision notes for the re-plan: ").strip()
        replan_context = (
            "\n\nA reviewer sent this plan back. Revise the plan to address the following, "
            "then stop again for review:\n" + notes
        )
        plan_round += 1

    # ---- Stages 3 & 4: Implement <-> Validate loop ---------------------------
    feedback = ""
    prior_validation_content = ""  # populated iteration 2+ to enable the STAGNATED progress check per validate-prompt.txt step 7c
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
            metrics_path, "implement", iteration, repo,
            lambda: run_stage(args.agent_cmd, implement_prompt, repo, prompt_scratch),
        ) != 0:
            print("Implement stage failed.")
            emit_summary(verdict="IMPLEMENT_FAILED")
            return 1

        print(f"\n== VALIDATE (iteration {iteration}) ==")
        validation_path = out_dir / f"validation-{iteration}.md"
        validate_prompt = render(
            load_template("validate-prompt.txt"),
            {
                **base,
                "VALIDATION_PATH": str(validation_path),
                "ITERATION": str(iteration),
                # Empty on iteration 1; populated 2+ so the validator can compare findings vs
                # the prior iteration and declare STAGNATED when nothing new is being found —
                # see validate-prompt.txt step 7c.
                "PRIOR_VALIDATION": prior_validation_content,
            },
        )
        if timed(
            metrics_path, "validate", iteration, repo,
            lambda: run_stage(args.agent_cmd, validate_prompt, repo, prompt_scratch),
        ) != 0:
            print("Validate stage failed.")
            emit_summary(verdict="VALIDATE_FAILED")
            return 1

        verdict = validation_path.read_text(encoding="utf-8") if validation_path.exists() else ""
        if "OVERALL VERDICT: PASS" in verdict:
            print(f"\nPASS on iteration {iteration}. Hand off to a human for final review + merge.")
            emit_summary(verdict="PASS", iterations_until_pass=iteration)
            return 0
        if "OVERALL VERDICT: STAGNATED" in verdict:
            # No new + no fixed substantive findings vs prior iteration. Continuing costs cycles
            # without producing new signal — hand off to a human for the accept-as-is / escalate /
            # intervene decision. Distinct exit code from PASS and from FAIL_AFTER_MAX_ITERATIONS.
            print(
                f"\nSTAGNATED on iteration {iteration}: no new substantive findings vs iteration "
                f"{iteration - 1}. Handing off to human — accept as-is, escalate, or intervene manually."
            )
            emit_summary(verdict=f"STAGNATED_AFTER_ITERATION_{iteration}")
            return 3

        print(f"FAIL on iteration {iteration}; feeding the report back into implement.")
        feedback = (
            "The previous implementation did not pass validation. Address every item in this report:\n\n"
            + verdict
        )
        # Retain this iteration's report as the "prior" for the next validate iteration's
        # STAGNATED comparison (step 7c).
        prior_validation_content = verdict

    print(f"\nStill failing after {args.max_iterations} iterations. Needs a human.")
    emit_summary(verdict="FAIL_AFTER_MAX_ITERATIONS")
    return 1


if __name__ == "__main__":
    sys.exit(main())
