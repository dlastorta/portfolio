# CLAUDE.md — Working agreement for AI-assisted code work

> Drop this file at the **root of any repo**. The agent reads it automatically every time it
> works in this folder, so the rules below apply to every task without being re-explained.
> This solves the "no shared context between tasks" problem: write the standard once, here.

This file exists because confident, well-documented code can still be architecturally wrong.
The rules below force the agent to **verify instead of trust**, and to run the specific checks
that surface-level reviews miss.

---

## 0. Non-negotiable rules (read first)

1. **Verify every claim against the source.** Never repeat what a README, comment, doc, or commit
   message *says* about the code without confirming it in the actual files (especially `.csproj` /
   package manifests, project references, and folder layout). If a doc says "Core has no UI
   dependency," open the project file and prove it before agreeing.
2. **Cite evidence for every finding.** Each strength or problem must name the file (and line/section)
   that proves it. No claim without a citation.
3. **Separate "it works / it's testable" from "it's well-structured."** Code can be fully tested and
   still violate architectural boundaries. Judge correctness, testing, AND structure as separate axes.
4. **Always run a red-team pass.** Before concluding, explicitly answer: *"What would a strict senior
   reviewer reject this for?"* Never end on "looks good" without this step.
5. **State what you could NOT verify.** If the build/tests weren't run, or a folder wasn't accessible,
   say so plainly and tell me how to confirm it myself.

---

## 1. Architecture review protocol (run this on every review)

This is the check that is most often skipped. Run it explicitly and show the output.

### Step 1 — Map every file to a layer

For each source file, produce a table: **file → project → layer it actually belongs to.**
Use these layers (Clean Architecture):

| Layer | Holds | Must NOT contain |
|-------|-------|------------------|
| **Domain / Core** | Entities, value objects, domain logic, **interfaces (ports)** | UI types, framework SDKs, concrete I/O, ViewModels |
| **Application** | Use cases, command/query handlers, ViewModels, orchestration | Direct DB/SQL, HTTP, file I/O, UI framework |
| **Infrastructure** | Concrete adapters: DB, SMTP, clock, file system, external APIs | Business rules, UI |
| **Presentation** | UI / API surface: views, controllers, resolvers, composition root | Business rules |

### Step 2 — Flag layer/project mismatches

Call out **any file whose real layer doesn't match the project it lives in.** Common smells:

- ❌ ViewModels, presentation, or orchestration sitting in a **Domain/Core** project.
- ❌ Concrete infrastructure (clock, email/SMTP, repositories, file I/O) sitting in **Domain/Core**
  instead of an Infrastructure project — Core should hold only the *interface*.
- ❌ A "Core" / "Common" project that contains domain **and** infrastructure **and** presentation —
  i.e. a catch-all "place for everything." This is a boundary failure even if every class is clean.

### Step 3 — Verify dependency direction

Dependencies must point **inward**: `Presentation → Application → Domain`, and
`Infrastructure → Domain`. Domain/Core depends on **nothing outward**.

- Open each project file. List its package + project references.
- ❌ Flag any **UI or infrastructure framework referenced by a Domain/Core project** (e.g. an MVVM
  toolkit, a web framework, an ORM, an SMTP client pulled into "Core").
- A package being "platform-agnostic" does **not** make it a domain concern. An MVVM library in Core
  still means a *presentation* concern has leaked into the domain.

### Step 4 — Verdict on structure

State clearly whether boundaries are **clean, muddled, or violated**, with the specific files that
drove the call. Do not let good documentation or good test coverage soften this verdict.

---

## 2. Right-sizing (avoid over- and under-engineering)

- Match effort to the task. For a small brief, note when extra layers/abstractions are justified vs.
  gold-plating — but **never** sacrifice correct boundaries for brevity. Clean separation is cheap;
  put each thing in the right place even in a small project.
- If the project root defines an architecture (see §4), follow it exactly. Consistency beats novelty.

---

## 3. Delivery hygiene (check before declaring done)

- **Git is clean.** No uncommitted changes in the working tree when a deliverable is "final." The
  shipped state must be captured in a commit. Run `git status` and report it.
- Build instructions and test instructions actually match the project layout.
- Documentation's claims match the code (re-checked per §0.1), not just internally consistent.

---

## 4. Project-specific architecture (FILL THIS IN per repo)

> Replace the placeholders below with this repo's real structure so the agent enforces YOUR layers,
> not generic ones. Example baseline drawn from a Clean Architecture / CQRS .NET layout:

- **Layers / projects:** `<Domain>`, `<Application>`, `<Infrastructure>`, `<Presentation/WebApi>`
- **Allowed dependencies:** Presentation → Application → Domain; Infrastructure → Domain. Domain → none.
- **Patterns in use:** `<e.g. CQRS + MediatR, Repository + Unit of Work, Result<T> for errors, AutoMapper for mapping, FluentValidation>`
- **Where things go:** interfaces in Domain; handlers/use-cases + ViewModels in Application; DB/SMTP/clock adapters in Infrastructure; controllers/resolvers/views + DI composition root in Presentation.
- **Forbidden:** raw SQL or I/O in handlers; business logic in UI; concrete adapters or UI frameworks referenced by Domain.

---

## 5. How I'll ask you to use this

When I say **"review this code,"** treat it as: run §1 (architecture protocol) in full, then correctness,
testing, security, and §3 hygiene — each with cited evidence — and finish with the §0.4 red-team pass.
If anything in §0 can't be satisfied, tell me before giving a verdict.
