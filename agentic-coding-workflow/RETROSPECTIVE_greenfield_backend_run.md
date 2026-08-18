# Retrospective — Running the workflow against a real greenfield backend

**Date**: August 2026
**Duration**: multi-day session-based use
**Target**: greenfield .NET 9 service (webhook ingest + REST query API + Basic Auth + three test tiers)
**Framing**: two parallel runs of the same brief — one product-first (manual + AI assistance), one spec-only minimal (agent-driven via this workflow)

> This retrospective is portfolio evidence, not internal ops notes. Written after the parallel builds shipped and after multiple adversarial review cycles converged.

---

## Executive summary

**What worked**: the Plan → Review → Implement → Validate loop produced usable artifacts without hand-holding beyond the human Review gate. `.cursor/rules/` injection was the single most effective control — turned "generic AI agent" into "agent that respects this project's constraints" more reliably than any prompt tweak.

**What didn't**: the role files prescribed a specific enterprise stack (MediatR, GraphQL, JWT) as the .NET default, biasing every greenfield build toward one team's house architecture regardless of ticket scope. Windows orchestration had latent bugs (POSIX shlex on Windows paths; `bash` resolving to WSL instead of Git Bash) that surfaced on first cross-platform use.

**Net verdict**: the workflow scaffolding is sound and the leverage points (staged execution, adversarial validation, human Review gate) held. The specific role files needed refactoring to be portable; the orchestrator needed one Windows-compat fix.

**Top 3 improvements identified**: (1) shlex.split posix-mode conditional on Windows, (2) role files as persona+mindset, tech stack per-repo, (3) validate stage to check "spec item → code + test" coverage matrix.

---

## What worked (signals verified against artifacts)

### The prompt structure held under pressure

The four-stage loop actually produced the artifacts each stage names — `plan.md`, `implementation-summary.md`, `validation-N.md` — without human intervention beyond the Review gate. On a greenfield build with no existing codebase to reference, the agent still produced a coherent plan by reading `tech-stack.md` at repo root + the ticket's Technical notes + `.cursor/rules/`.

### `.cursor/rules/` was the leverage point

The single most effective control was a hand-written `.cursor/rules/no-speculation.md` in the target repo — 13 explicit constraints ("no dependency without a use", "no abstraction with < 3 concrete users", "ADRs are scarce — target 5-8"). When the agent's first plan drifted toward ceremony, these rules pulled it back on the next iteration.

**Learning**: per-repo explicit rules outperform per-workflow implicit persona prompts. Rules that a human would recognize as "code review checklist" work; rules that read as "engineering values" don't move the model as much.

### Iteration loop terminated

The implement↔validate loop actually converged — the validator produced structured FAIL reports; the implement stage addressed them. Did not hit the `--max-iterations` cap on typical runs. The validator's adversarial prompt ("your job is not to confirm the work but to find what's wrong with it") produced findings that were consistently substantive, not sycophantic.

### The human Review gate paid off

The human-in-the-loop gate between Plan and Implement was where the most valuable interventions happened. Approving a bloated plan meant paying the rewrite cost later; sending back with concrete notes ("your plan proposes X which .cursor/rules/no-speculation.md forbids — redesign without") was cheap and steered decisively.

**Learning**: full automation ambition would have hurt here. The gate is the workflow's core value, not a friction point.

---

## What didn't work (friction with root cause)

### Role files prescribed a specific stack by default

`prompts/roles/plan-be.txt` (pre-fix) baked in "CQRS via MediatR, GraphQL, JWT" as the .NET default. The agent proposed those regardless of ticket scope, biasing every greenfield build toward one team's house architecture. Even with a `tech-stack.md` at repo root that explicitly listed non-goals, the role's prescriptions competed for attention.

**Root cause**: role files conflated **persona + judgment** (portable across projects) with **tech-stack prescription** (project-specific).

**Fix applied**: role files refactored to persona + right-sizing mindset only. Tech context moved to per-repo `tech-stack.md`. Pattern documented in `prompts/README.md`.

### Orchestrator broke on Windows

`subprocess.run(shlex.split(cmd))` in `run_agent.py` used POSIX-mode shlex by default, which strips backslashes from Windows paths — `C:\Users\dare2\...` became `C:Usersdare2...`. Additionally, `bash` on the Windows PATH resolved to WSL, not Git Bash — `bash /path/to/wrapper.sh` failed with "/bin/bash: No such file or directory" from WSL's execvpe.

**Root cause**: orchestrator was developed on POSIX shells; `os.name` never checked; no cross-platform CI.

**Fix applied**: Python wrapper `scripts/run-claude.py` sidesteps the shell layer (invokes `claude` with `shell=True`, prompt via stdin, `cwd` explicit). Works around the two Windows bugs simultaneously.

**Fix pending**: `shlex.split(cmd, posix=(os.name != "nt"))` in `run_agent.py`. The wrapper works but the underlying bug is still latent for any future CLI adapter that isn't wrapped.

### Greenfield mode was not first-class

The workflow's mental model was "add feature to existing code" — Plan stage instructions said "research the codebase to understand X". For greenfield builds with an empty target repo, the agent had no codebase to read; it fell back to role-file defaults (which was the failure mode above).

**Root cause**: workflow assumed existing code as universal context source.

**Fix pending**: either a greenfield role variant (`plan-be-greenfield.txt`) OR greenfield detection in the orchestrator (empty `.sln` / no source files → orchestrator prompts agent for stack proposals in Plan stage rather than "read the codebase").

### Malformed-body handling was a real bug the workflow missed

The manually-authored parallel build had a `GlobalExceptionHandler` that caught all exceptions and returned 500 — including `BadHttpRequestException` from body deserialization, which should be 400. Neither the plan-review nor the validate stage flagged this. It was caught by an external adversarial code review, not the workflow.

**Root cause**: the validate stage checks acceptance criteria + plan adherence + test coverage but does not systematically look for exception-classification bugs.

**Fix pending**: add a "exception handling review" bullet to the validate template — walk each exception type in the code and verify it maps to an appropriate HTTP status.

---

## Gaps discovered (things the workflow should do but doesn't)

### 1. No "spec item → code + test" coverage matrix
Gap-hunt at the end of the run was manual — enumerated the 10 spec acceptance criteria, grepped for corresponding test coverage. Found 7 real gaps (empty batch, malformed body, mixed valid/invalid, cross-role auth, sticky-disable end-to-end, correlation propagation, orphan-unpublish HTTP+SQL). Should be a Validate-stage output: a matrix of `AC N → covered by code (file:line) → covered by test (file:name)`.

### 2. No "declared vs used" audit
A `Microsoft.EntityFrameworkCore.Sqlite` package sat in `csproj` and was claimed in ADRs, README, CI comments for the entire run despite zero call sites. Only caught by external adversarial review. Validate stage could grep every declared package against actual `using`/`import` statements and flag orphans.

### 3. No cost-estimation for architectural choices
Plan proposed MediatR + Strategy for 3 handlers. A stronger plan-review would ask: "for each proposed abstraction, how many concrete implementations does it have today? If < 3, mark as candidate-for-simplification with a switch or direct call." Right-sizing is currently a general instruction, not a checkable output.

### 4. No verification-cycle log baked in
The target repo evolved a `verification-report.md` that got rewritten as a "cycle log" — captured drifts found + fix locations + trace to commit. This pattern emerged organically. The workflow could bake it in as a Validate-stage output ("update `verification-cycle-N.md` with what changed since the previous audit").

### 5. Metrics are shallow
`metrics.json` captures wall-clock duration per stage. Could also capture: LOC generated, files touched, `.cursor/rules/` size injected, agent model used per stage, iteration count until PASS. Useful for calibrating future runs and for portfolio evidence.

### 6. No "review-done" signal
Adversarial review has diminishing returns — 5 rounds against the parallel build, each round found things, but round 5 explicitly said "core is the right shape; more polish will feed the complexity objection". The workflow has no built-in heuristic for "stop iterating" — could offer one (e.g., 2 consecutive rounds with no substantive new findings + no failing tests).

---

## Meta-observations

### The agent respected explicit rules better than implicit ones
When `.cursor/rules/no-speculation.md` said "no MediatR, no Strategy for < 3 handlers", the agent complied. When ADRs merely *implied* right-sizing, the agent still added ceremony. **Implication**: for high-signal constraints, encode them as explicit checkable rules rather than trust judgment prompts.

### Right-sizing is asymmetric
Adding ceremony is easy; removing it is expensive (sunk cost + reviewer defensiveness). The spec-only run stayed close to minimal because constraints forbade additions from the start. The parallel product-first run started heavy and defending each layer cost more effort than adding them did. **Implication**: it is cheaper to start minimal + document what you'd add than to start heavy + defend what you kept.

### Adversarial review has diminishing returns
Each new review round found things, but the marginal value dropped fast. By round 5 the findings were nits + doc-drift the previous fix rounds surfaced. **Implication**: workflow should offer a stopping heuristic. Reviews are not free — they cost author fatigue and add "hunted-by-critique" energy to the codebase.

### The retrospective is itself portfolio evidence
Running the workflow against a real greenfield build + documenting what worked and didn't is stronger evidence than describing the workflow in the abstract. The document exists whether the workflow gets refactored or not.

---

## Improvement items — ranked

Priority: **P0** = fix before next run · **P1** = strongly worth doing · **P2** = worth doing when time permits · **P3** = nice-to-have

| # | Item | Priority | Estimate | Category | Status |
|---|------|----------|----------|----------|--------|
| 1 | `shlex.split(cmd, posix=(os.name != "nt"))` in `run_agent.py` | P0 | 5 min | orchestrator | ✅ done |
| 2 | `PATTERNS.md` documenting the per-repo `tech-stack.md` + `.cursor/rules/no-speculation.md` patterns (+ 2 related doc patterns) | P1 | 30 min | docs | ✅ done |
| 3 | Greenfield mode: role variant or greenfield-detection logic | P1 | 1-2 h | prompts + orchestrator | ✅ done |
| 4 | Validate stage: "spec item → code + test" matrix output | P1 | 1-2 h | prompt | |
| 5 | Case-study section in workflow README linking this retrospective | P1 | 20 min | docs | ✅ done |
| 6 | Validate stage: "declared vs used" package/tool audit | P2 | 1-2 h | prompt | |
| 7 | Metrics ampliadas: LOC, files touched, rules-injected size | P2 | 30 min | orchestrator | |
| 8 | Verification-cycle log pattern baked into Validate stage | P3 | 1 h | prompt | |
| 9 | "Review-done" heuristic (2 consecutive no-substantive-findings) | P3 | design + 1-2 h | prompt + orchestrator | |
| 10 | Exception-handling review bullet in Validate template | P2 | 15 min | prompt | ✅ done |
| 11 | `templates/tech-stack.default.md` — opt-in greenfield bootstrap template with sensible package defaults + explicit non-goals | P1 | 30 min | templates | ✅ done |

