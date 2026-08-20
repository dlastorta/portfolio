# Patterns

Named patterns that emerged from running this workflow against real builds.

None of these are invented from theory — each surfaced organically during actual
use, was refined when it broke, and made the next run measurably better. Together
they form the operator-side toolkit that complements the prompt / orchestrator
side documented in the [main README](README.md).

**How to read this document**: each pattern has a problem statement, a structure
template you can copy, when-to-use / when-not-to-use guidance, and a concrete
example. Skim the headers first; drop into the ones that match a problem you
are hitting.

---

## Contents

1. [`tech-stack.md` per repo — declare your defaults](#1-tech-stackmd-per-repo--declare-your-defaults)
2. [`.cursor/rules/no-speculation.md` per repo — enforce your constraints](#2-cursorrulesno-speculationmd-per-repo--enforce-your-constraints)
3. [Verification-cycle log — keep audit docs honest over time](#3-verification-cycle-log--keep-audit-docs-honest-over-time)
4. ["Detected during review cycle" — turn drift into visible learning](#4-detected-during-review-cycle--turn-drift-into-visible-learning)
5. [`AGENTS.md` per repo — cross-tool agent conventions](#5-agentsmd-per-repo--cross-tool-agent-conventions)

---

## 1. `tech-stack.md` per repo — declare your defaults

### Problem

The workflow's role files describe a persona and a right-sizing mindset — they
deliberately do **not** prescribe a specific tech stack, because prescribing
"MediatR + GraphQL + JWT" as the .NET default biases every greenfield build
regardless of ticket scope. But somewhere the agent still needs to know: "if
your plan calls for validation, which library?"

The right place for that answer is per-repo, not per-workflow.

### Structure

A `tech-stack.md` at the target repo's root, or `.cursor/rules/tech-stack.md`
inside the rules folder (the workflow auto-injects both). Grouped by
**category**, each category naming the preferred library plus a short
"add only when the ticket actually requires this category" note.

Minimum viable template (adapt to your stack):

```markdown
# tech-stack.md — <project name>

Sensible defaults. Delete any category you don't need. IF a category is
needed, this file names the preferred library; the workflow's role file
already says "do NOT propose a category unless the ticket requires it."

## Runtime
- .NET 9, pinned via global.json

## Data
- Entity Framework Core 9 + Microsoft.EntityFrameworkCore.SqlServer
- Central package management via Directory.Packages.props

## Validation (only if inputs have non-trivial rules)
- FluentValidation

## Logging
- Serilog.AspNetCore with structured JSON output

## Testing
- xunit + FluentAssertions + Moq (unit)
- Testcontainers.MsSql (integration against real SQL)

## Explicitly NOT in this stack
- MediatR / CQRS unless the ticket has 3+ cross-cutting concerns
- Repository / Unit of Work over EF (DbContext is the abstraction)
- GraphQL, gRPC, SignalR
- OpenTelemetry with custom ActivitySource (Serilog structured logs suffice)
```

The full reference version — with reasoning for each choice, "what's built into
the framework so don't add a package", and scaffolding files — is at
[`templates/tech-stack.default.md`](templates/tech-stack.default.md).

### When to use

- **Greenfield builds** — critical. Without this file, the agent falls back to
  role-file defaults (which are intentionally minimal in this workflow) and
  the plan asks the reviewer for stack decisions that could have been declared
  once up front.
- **Established codebases where the stack is not obvious from the existing
  code** — mixed patterns, migration in progress, or partial adoption of a
  newer library.

### When NOT to use

- **Established codebases with a strong, consistent stack visible from the
  code** — the agent will infer it from the `.csproj` files and existing
  patterns. Duplicating it in a `tech-stack.md` is drift-prone.
- **One-off spikes and prototypes** where the stack is throwaway.

### Related patterns

- Pattern 2 (`.cursor/rules/no-speculation.md`) — this file says "if you need
  validation, use FluentValidation"; the rules file says "do NOT add
  validation unless the ticket requires it". They compose.
- Prompts / role files — see [`prompts/README.md`](prompts/README.md) for how
  the agent's role instructs it to look for `tech-stack.md` before falling
  back to defaults.

---

## 2. `.cursor/rules/no-speculation.md` per repo — enforce your constraints

### Problem

An AI agent given a small brief and a permissive persona will reach for extra
abstraction layers, patterns imported by reflex, and dependencies "for future
flexibility". Every prompt-level instruction like "please right-size the design"
gets partially obeyed and partially ignored. Explicit checkable rules move the
model more reliably than judgment prompts.

### Structure

A short, numbered rules file at `.cursor/rules/no-speculation.md` in the target
repo. The workflow's orchestrator auto-injects the entire `.cursor/rules/`
folder into every prompt stage.

Template — the exact ruleset used on a real greenfield build, generalizable
to most contexts:

```markdown
# no-speculation.md — right-sizing rules for this repo

These rules override the agent's default enterprise-<stack> reflexes.

## Core rules

1. **Build what the spec requires. Nothing more.** Nice-to-have goes into
   `deferred.md`, not into code.
2. **No dependency without a use.** Every package must have at least one
   non-trivial call site that could not be replaced by BCL types.
3. **No abstraction with fewer than three concrete users.** Do not
   introduce an interface, factory, strategy, dispatcher, or generic wrapper
   for a case that has one or two implementations today.
4. **No pattern imported by reflex.** <List patterns your context sees
   over-applied — MediatR, Repository/UoW, GraphQL, JWT, OpenTelemetry with
   custom ActivitySource, rate limiting, secret-rotation runbooks — none are
   the default here.>
5. **ADRs are scarce.** Target 5-8 short entries for the whole project. Each:
   Context (1-2 sentences), Decision (1-2 sentences), Alternatives, Trade-off.
6. **Layer only when boundaries exist.** Not every project needs
   Domain / Application / Infrastructure / Presentation as separate assemblies.

## Testing rules

7. **Test at the level that catches the bug.** Prefer integration tests for
   anything touching the DB or HTTP boundary; unit tests for pure domain logic.
   Do not write both for the same behavior.
8. **Assert on state, not on mock calls.** `Verify(SaveChangesAsync)` alone is
   not a passing test. Assert on what the DB or the response actually contains.
9. **No test written to raise coverage percentage.** If a class has no
   meaningful behavior to test, do not construct an instance just to make
   coverage green. Explain the omission in a comment.

## Documentation rules

10. **README is the entry point.** It must let a reviewer run the project
    locally in under 5 minutes.
11. **Docs describe reality.** No aspirational sentences. If code and docs
    disagree, fix one before shipping. A stale audit doc is worse than no
    audit.

## Agent behavior

12. **Prefer Grep + targeted Read over full-file Read.** Full-file reads on
    large files are token-expensive and rarely necessary. Grep for the
    symbol or pattern first; Read with offset+limit around the matches.
    Full-file reads only for small files or when you need the whole
    structure (class hierarchy, config end-to-end).
13. **Comments: WHY and WARN only.** Delete WHAT (describes the obvious),
    WHERE (references to stale docs, bare TODO/FIXME), commented-out code,
    and unjustified lint/type suppressions. See `AGENTS.md` § Comments
    policy for the full matrix. Public API docblocks are kept.
```

### Why numbered rules work better than persona prompts

Two observations from real use:

1. The agent complies with **"no MediatR"** as a rule far more reliably than
   with **"advocate for simple, elegant solutions"** as a mindset instruction.
   Rules give the model something checkable; mindset gives it something to
   partially interpret.
2. When the agent proposes something the rules forbid, the human Review step
   has a clean send-back: "Rule N forbids X. Redesign without." That's a
   sentence, not a debate.

### When to use

- **Any repo where the workflow will run**, unless the existing codebase's
  conventions are already so strong that the agent will infer right-sizing
  from the code alone. In practice: always add this file for greenfield;
  optional for well-established codebases.

### When NOT to use

- **When the rules would contradict the ticket** — if the ticket explicitly
  requires MediatR (integrating with an existing MediatR-based codebase),
  don't ship a rule that forbids MediatR. The rules describe the repo's
  posture, not universal law.

### Related patterns

- Pattern 1 (`tech-stack.md`) — see above.
- The [main README § 3](README.md#3-cursorrules-conventions-the-agent-must-obey)
  describes how `.cursor/rules/` gets injected into every prompt stage.

---

## 3. Verification-cycle log — keep audit docs honest over time

### Problem

A "verification report" or "ADR-vs-code audit" written once and never updated
becomes actively misleading. It describes drifts that were fixed weeks ago
and misses drifts that appeared after. A reviewer who cross-references the
audit against the code and finds them contradictory concludes — correctly —
that the team does not maintain what it writes.

The audit itself is the leverage; it needs to be a **cycle log** (what was
found, what changed, when) rather than a **snapshot** (what was found at
one point in time).

### Structure

A single `verification-report.md` in the target repo's `docs/` folder, with
two sections:

1. **Current state** — one section per ADR (or per major architectural
   claim), each stating the current implementation matches (CONFORMS) or does
   not (DRIFT — with the drift description).
2. **Cycle log** — a table at the bottom listing every drift ever found, when
   it was found, what commit resolved it, and where the fix lives in the
   codebase.

Template for the cycle log section:

```markdown
## Cycle log — drifts found and resolved

The initial audit surfaced N drifts. All were resolved in follow-up commits
before this report was rewritten. This section preserves the history; the
"Current state" section describes present reality.

| # | Original drift | Resolution | Fix location |
|---|----------------|------------|--------------|
| 1 | ADR-X claimed Y, code did Z | Aligned code with ADR | `src/.../Foo.cs` L45; commit abc123 |
| 2 | ADR-Y skipped feature Z | Implemented Z + test | `src/.../Bar.cs`; `tests/BarTests.cs` |
| ... | | | |

## How to keep this document honest

- When code changes ADR behavior, update the ADR AND rerun the audit row.
- When an ADR is superseded, mark it "Superseded" here (not deleted) and
  link the successor.
- **A stale audit is worse than no audit.** If this document is ever unclear,
  delete it rather than leave a lie in the repo.
```

### When to use

- **Any project that ships architectural documentation alongside code.** ADRs
  without an accompanying audit devolve into aspirational fiction. The audit
  is the accountability layer.

### When NOT to use

- **Projects with no ADRs / architectural docs to audit.** The audit is a
  reflection of a documented reality; without documented decisions, there is
  nothing to check code against.

### Related patterns

- Pattern 4 ("Detected during review cycle") composes with this — when the
  cycle log gets an entry, the corresponding ADR § Consequences also gets a
  "detected during review cycle" bullet so the ADR is self-explanatory.

---

## 4. "Detected during review cycle" — turn drift into visible learning

### Problem

When an adversarial review catches a real gap in the implementation, the
common response is to fix the code silently. That hides the learning. A
future reader (or future team member) has no way to know that the current
implementation is the result of a specific miss + correction — they see
only the final state.

The alternative: mention the miss + correction directly in the ADR that
governs the decision. This turns each drift into visible engineering
evidence and pre-empts the "why did you build it this way?" interview
question with a documented answer.

### Structure

A bullet in the relevant ADR's **§ Consequences** or **§ Trade-offs**
section, formatted:

```markdown
- **<Short description of the finding>**. <One or two sentences on what the
  original decision was, what the drift or gap was, how it was resolved,
  and — importantly — the escape hatch or trigger for revisiting.>
  Locked in by <test name> / documented in <commit reference>.
```

### Example (from a real ADR)

```markdown
- **Concurrent same-id writers are not serialized at the domain layer.**
  Two producers submitting `publish` for the same new id in the same instant
  may both read `FindByIdAsync -> null`, both attempt an insert, and the
  second one fails with a `persistence_error` when SQL Server surfaces the
  primary-key violation. This is the correct outcome under the current
  design — the batch continues, no data corruption, no HTTP 500 — but it
  produces a `persistence_error` for the loser instead of a cleaner
  `duplicate` skip. The alternative (adding a `RowVersion` column + a
  re-read retry loop) was considered and deferred: the spec does not
  describe concurrent producers, we have no evidence of the pattern under
  real load, and the producer already has to handle retry on any
  non-processed outcome. Documented as `future-improvements.md` #16 with
  the trigger conditions. The current behavior is captured end-to-end by
  `Post_ConcurrentPublishesForSameNewId_ResolveGracefully_...`.
```

Note what the bullet does:

- **Names the finding** (so a reviewer skimming the ADR sees it)
- **Explains the current behavior + why it's correct-given-tradeoffs**
- **Documents the alternative + why it was deferred**
- **Names the trigger for revisiting** (future-improvements entry with conditions)
- **Points at the test that locks the behavior** (so anyone can verify the current state)

### When to use

- Any time an adversarial review surfaces a real finding that you choose to
  document rather than immediately fix.
- Any time you make a design decision under a constraint (spec silence, scope
  limit, time pressure) where the decision could look wrong without context.

### When NOT to use

- Trivial nits or cosmetic issues — those go in commit messages, not ADR
  bodies.
- Drift that you fully fixed and no future revisit is expected — those go in
  the verification-cycle log (Pattern 3), not the ADR body.

### Related patterns

- Pattern 3 (verification-cycle log) — same accountability instinct at the
  audit-doc layer.
- The workflow's Validate stage prompt — see
  [`prompts/validate-prompt.txt`](prompts/validate-prompt.txt) for how the
  adversarial red-team pass is structured to surface these findings in the
  first place.

---

## 5. `AGENTS.md` per repo — cross-tool agent conventions

### Problem

Different agent tools read different config files: Cursor honors
`.cursor/rules/`, other tools may not; each ecosystem invents its own
folder. When the same conventions matter across tools ("keep comments
minimal", "read narrowly, not full-file", "prefer removing code to adding
layers"), duplicating them into each tool's native format drifts fast.

The [`AGENTS.md`](https://agents.md/) convention is the emerging
cross-tool answer — a single file at the repo root that any
AGENTS.md-aware client picks up automatically. It is the right place for
conventions that are about **agent behavior** (how the agent reads,
writes, and terminates work) rather than **code shape** (which is Pattern
2's job).

### Structure

A single `AGENTS.md` at the target repo's root. Sections cover the
categories where agent behavior is worth constraining. The reference
version — with the exact rules used on this workflow's own repo — is at
[`templates/AGENTS.md`](templates/AGENTS.md).

Minimum viable outline:

```markdown
# AGENTS.md — cross-tool agent conventions for this repo

## Fixes should make the system simpler, not more complex
<Prefer removing/consolidating over adding layers, flags, special cases.
Right-sizing is a token cost lever, not just aesthetic.>

## Comments policy — keep WHY and WARN, delete WHAT and WHERE
<Categorize each comment: WHAT/WHERE/DEAD/SUPPRESS → delete;
WHY/WARN/API-contract → keep. Rationale goes in commits and ADRs.>

## File reading discipline
<Grep first, then targeted Read with offset+limit. Full-file reads only
for small files or when the whole structure is needed.>

## Session hygiene
<Fresh session per new work stream. Long conversations accumulate
context that ships with every subsequent request.>
```

### Why this deserves its own file (not a section of the README)

READMEs are read by humans starting the project. `AGENTS.md` is read by
agent tools on every request. Keeping them separate means:

- The README can stay narrative and setup-focused; `AGENTS.md` stays
  short and directive.
- Agent tools that respect the convention pick up the rules without
  humans having to include them in every prompt.
- Rules can evolve (new comment categories, new tool support) without
  editing the human-facing README.

### When to use

- **Any repo where multiple people use different agent tools** — `AGENTS.md`
  gives them one source of truth instead of one file per tool.
- **Any repo where the same conventions are drifting across `.cursor/rules/`,
  `.github/copilot-instructions.md`, `CLAUDE.md`, etc.** — consolidate the
  cross-tool subset into `AGENTS.md`; leave only tool-specific bits in the
  per-tool files.

### When NOT to use

- **Solo project, single tool, small codebase** — `AGENTS.md` is one more
  file to maintain. If your `.cursor/rules/` file already covers everything
  and no other tool is in play, don't add ceremony.
- **Rules that are truly tool-specific** — keep those in the tool's native
  file, not in `AGENTS.md`.

### Related patterns

- Pattern 2 (`.cursor/rules/no-speculation.md`) — that pattern governs
  **code shape** (right-sizing, dependencies, test discipline);
  `AGENTS.md` governs **agent behavior** (reading, commenting, session
  management). They compose without overlapping.
- Pattern 1 (`tech-stack.md`) — declares libraries and framework choices;
  `AGENTS.md` says nothing about libraries. They live at the same level in
  the repo root, address different questions.

---

## Adding a new pattern to this document

If a future run surfaces a reusable pattern:

1. Verify it worked on at least one real build (theory does not count).
2. Add it as a new numbered section with the same structure: Problem →
   Structure → When to use → When NOT → Related.
3. Update the Contents list at the top.
4. Reference it from the retrospective that discovered it, closing the loop
   between "we learned this" and "we captured it".

Patterns are not free — each one adds a slot the operator has to know about.
Only add ones that pay their own weight.
