# Prompt templates

These files are the **source of truth** for the prompts the orchestrator ([`../scripts/run_agent.py`](../scripts/run_agent.py)) sends to the AI agent at each stage. The script loads them at runtime and substitutes `{{PLACEHOLDER}}` tokens with actual values.

## Stage templates

The workflow has four stages — Plan → Review → Implement → Validate. Three are agent stages with a template here; **Review is a human approval gate between Plan and Implement, so it has no prompt** (it lives in [`../scripts/run_agent.py`](../scripts/run_agent.py) as a send-back-to-plan loop).

| Template | Stage | Purpose |
|---|---|---|
| `plan-prompt.txt` | 1 — Plan | Research the codebase, produce an implementation plan and test strategy. No code changes. |
| _(none)_ | 2 — Review | Human reads `plan.md` and approves it, or sends it back with notes (re-runs Plan via `{{REPLAN_CONTEXT}}`). |
| `implement-prompt.txt` | 3 — Implement | Execute the approved plan, write code + tests, verify the build. |
| `validate-prompt.txt` | 4 — Validate | Adversarial QA review against acceptance criteria, plan adherence, and conventions. |

## Role files (`roles/`)

Role definitions provide a specialized **persona and mindset** per project type. The orchestrator auto-detects the project type and picks the matching file.

| Role file | Stage | Project type | Persona |
|---|---|---|---|
| `roles/plan-be.txt` | Plan | Backend (.NET) | Senior Backend Architect |
| `roles/implement-be.txt` | Implement | Backend (.NET) | Senior Backend Engineer |
| `roles/plan-fe.txt` | Plan | Frontend (React/TS) | Senior Frontend Architect |
| `roles/implement-fe.txt` | Implement | Frontend (React/TS) | Senior Frontend Engineer |
| `roles/generic.txt` | Any | Fallback | Senior Software Engineer (explores the stack first) |

**Auto-detection:** a `.sln`/`.csproj` at the repo root → backend; a `package.json` → frontend; neither → generic.

**Role files are deliberately not prescriptive about tech choices** — they set a persona and a right-sizing mindset, not a house-standard architecture. This keeps the workflow reusable across projects with different scale and different stacks. Tech-stack conventions live per repo (see next section).

## Tech-stack per repo (`tech-stack.md` or `.cursor/rules/tech-stack.md`)

Because different projects use different stacks (and even the same team uses different stacks across products), tech-stack conventions live in the **target repo**, not in this workflow. The role files instruct the agent to look for stack context in this order:

1. **Existing code in the target repo** — if the repo already uses EF Core + xUnit + Serilog, the agent follows suit.
2. **A `tech-stack.md` at the repo root** (or `.cursor/rules/tech-stack.md`) — an explicit declaration of the project's chosen libraries, patterns, and boundaries.
3. **The ticket's Technical notes section** — for greenfield tickets, or when the ticket overrides project defaults.

For a mature codebase, the existing code is usually enough. For greenfield or when you want to pin conventions explicitly (e.g., "we chose EF Core Dapper, not both"), add a short `tech-stack.md` at the repo root. Example:

```markdown
# tech-stack.md

- Runtime: .NET 9, C# 12
- Data: EF Core 9 (SQL Server), migrations checked in
- API: Minimal APIs (not controllers)
- Testing: xUnit + FluentAssertions + Testcontainers
- Logging: Serilog with structured JSON
- No MediatR, no Repository/UoW (thin service uses DbContext directly)
```

## Placeholders

| Placeholder | Filled with |
|---|---|
| `{{ROLE}}` | The role definition loaded from `roles/` for this project type and stage |
| `{{TICKET_CONTEXT}}` | Instructions to read the saved ticket data (title, description, acceptance criteria) |
| `{{TICKET_KEY}}` | The ticket identifier (e.g. `ABC-123`) |
| `{{PLAN_PATH}}` | Absolute path to `output/<ticket>/plan.md` |
| `{{SUMMARY_PATH}}` | Absolute path to `output/<ticket>/implementation-summary.md` |
| `{{VALIDATION_PATH}}` | Absolute path to the current `output/<ticket>/validation-N.md` |
| `{{FEEDBACK}}` | Previous validation feedback (empty on the first implement iteration) |
| `{{ITERATION}}` | Current implement/validate iteration number |
| `{{PRIOR_VALIDATION}}` | Full text of the previous iteration's validation report — empty on iteration 1; populated on iteration 2+ so the validator can compare findings and declare `STAGNATED` when the implement stage is not converging (see validate-prompt.txt step 7c). |
| `{{CURSOR_RULES}}` | The repo's auto-detected `.cursor/rules/` block (empty if none) |
| `{{PROJECT_CONTEXT_HINT}}` | Greenfield hint (only when the repo has fewer than 3 real source files — see [greenfield mode](#greenfield-mode) below). Empty on repos with existing code. |
| `{{EXTRA_INSTRUCTIONS}}` | One-off instructions passed on the command line (empty if none) |
| `{{REPLAN_CONTEXT}}` | Re-plan notes when a human sends the plan back for revision (empty otherwise) |

Unreplaced placeholders are stripped at runtime.

## Greenfield mode

The orchestrator auto-detects greenfield repos (fewer than 3 real source files, excluding tool caches and build output) and injects an explicit hint into the Plan prompt via `{{PROJECT_CONTEXT_HINT}}`. The hint tells the agent:

- Do NOT try to "research the codebase" — there is no code to read.
- Context sources, in priority order: (1) `tech-stack.md` at repo root, (2) `.cursor/rules/*` files, (3) ticket Technical notes.
- If none of the above declare a library for a category the plan needs, propose it in the plan and mark it as "needs reviewer decision" — do NOT pick silently.

On repos with existing code, the placeholder is empty and the default "research the codebase" instruction applies as normal.

You can see which mode the run is in from the orchestrator's first line of output:

```
Ticket ABC-123 | project type: be | context: greenfield | repo: /path/to/repo
Ticket ABC-123 | project type: be | context: existing codebase | repo: /path/to/repo
```

The detection heuristic is defensive — three source files is a low bar; the goal is to distinguish "actual new project" from "repo with some real code". If you disagree with the automatic classification for a specific run, `--extra` gives you a channel to override the assumption in the prompt.

## Customizing

Edit a **template** to change behavior for all runs (e.g. add a coverage requirement to `implement-prompt.txt`, or tighten `validate-prompt.txt`).

Edit a **role file** only to adjust persona or mindset — not to bake in a specific tech stack. Stack-specific conventions belong in the target repo (`tech-stack.md` or `.cursor/rules/`), so the role stays reusable across projects.

Use the orchestrator's `--extra` flag for a one-off addition to any single run without editing files.
