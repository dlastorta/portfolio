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

Role definitions provide a specialized persona and tech-stack context per project type. The orchestrator auto-detects the project type and picks the matching file.

| Role file | Stage | Project type | Persona |
|---|---|---|---|
| `roles/plan-be.txt` | Plan | Backend (.NET) | Senior Backend Architect |
| `roles/implement-be.txt` | Implement | Backend (.NET) | Senior Backend Engineer |
| `roles/plan-fe.txt` | Plan | Frontend (React/TS) | Senior Frontend Architect |
| `roles/implement-fe.txt` | Implement | Frontend (React/TS) | Senior Frontend Engineer |
| `roles/generic.txt` | Any | Fallback | Senior Software Engineer (explores the stack first) |

**Auto-detection:** a `.sln`/`.csproj` at the repo root → backend; a `package.json` → frontend; neither → generic.

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
| `{{CURSOR_RULES}}` | The repo's auto-detected `.cursor/rules/` block (empty if none) |
| `{{EXTRA_INSTRUCTIONS}}` | One-off instructions passed on the command line (empty if none) |
| `{{REPLAN_CONTEXT}}` | Re-plan notes when a human sends the plan back for revision (empty otherwise) |

Unreplaced placeholders are stripped at runtime.

## Customizing

Edit a template to change behavior for **all** runs (e.g. add a coverage requirement to `implement-prompt.txt`, or tighten `validate-prompt.txt`). Edit a role file to adjust stack details or persona. Use the orchestrator's `--extra` flag for a one-off addition without editing files.
