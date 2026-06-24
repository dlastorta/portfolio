# Code Review Checklist (Definition of Done)

A repeatable checklist for reviewing code — yours or a candidate's — with the **architecture and
boundary checks baked in up front**, because those are the ones surface-level reviews miss.

**How to use it:** paste the prompt in the last section, or just say *"review this against
CODE-REVIEW-CHECKLIST.md."* The agent must work top-to-bottom and **cite a file/line for every
finding**. Don't accept a verdict that skips section 1.

---

## 1. Architecture & boundaries  ⟵ run this FIRST, never skip

This section exists because clean-looking, well-tested code can still be structurally wrong. A
review that only checks "does it work and is it tested" will pass code that a strict reviewer fails.

- [ ] **Every file mapped to a layer.** Produced a `file → project → actual layer` table
      (Domain / Application / Infrastructure / Presentation).
- [ ] **No layer/project mismatch.** No file sits in the wrong project. Specifically:
  - [ ] No **ViewModels / presentation / orchestration** inside a **Domain/Core** project.
  - [ ] No **concrete infrastructure** (clock, email/SMTP, repositories, file/HTTP I/O) inside
        **Domain/Core** — Core holds only the *interface*; the implementation lives in Infrastructure.
  - [ ] No **catch-all project** that mixes domain + infrastructure + presentation ("a place for
        everything"). This is a failure even when each individual class is clean.
- [ ] **Dependency direction is inward.** Checked each `.csproj`/manifest:
      `Presentation → Application → Domain`, `Infrastructure → Domain`, Domain → nothing outward.
- [ ] **No UI/infra framework referenced by Domain/Core.** Opened the Core project file and
      confirmed it pulls no MVVM toolkit, web framework, ORM, or transport client. ("Platform-agnostic"
      does not excuse a presentation library in the domain.)
- [ ] **Documentation claims verified, not trusted.** Any "Core has no UI dependency"-type statement
      was checked against the actual project file, not accepted from the README.
- [ ] **Structure verdict given:** clean / muddled / violated — with the files that drove it.

> ❌ Anti-pattern (this is what failed the TwoSense challenge): a `Core` project containing
> `Models/` + `Services/` (concrete `SystemClock`, `InMemoryEmailService`) + `ViewModels/`, with a
> `PackageReference` to an MVVM toolkit. Domain, infrastructure, and presentation all in one project.
>
> ✅ Correct: `Domain` (entities + interfaces only) · `Application` (ViewModels/use-cases) ·
> `Infrastructure` (clock, email adapters) · `App/WebApi` (UI + composition root).

---

## 2. Correctness

- [ ] Meets every explicit requirement in the spec/brief (checked one by one).
- [ ] Core logic verified independently (hand-checked math, traced the key path), not assumed from tests.
- [ ] Edge cases handled: empty/null input, boundary values, divide-by-zero, overflow, cancellation.
- [ ] Error paths return/throw sensibly; no silent swallowing of exceptions.
- [ ] Concurrency/threading assumptions are stated and hold.

## 3. Testing

- [ ] Tests actually run and pass — **or** it's clearly stated they weren't run and how to run them.
- [ ] Coverage spans levels appropriately (unit / integration / e2e) for the risk.
- [ ] Boundary and failure cases tested, not just the happy path.
- [ ] Tests are deterministic (time/randomness/IO abstracted; no flaky sleeps or wall-clock races).
- [ ] Test names describe behavior; tests read as a spec of the system.
- [ ] No critical path left untested (e.g. happy-path e2e missing because of a test-seam gap).

## 4. Security & data handling

- [ ] No secrets, credentials, or sensitive field content in logs or error messages.
- [ ] Inputs validated; parameterized queries; least privilege.
- [ ] Nothing sensitive persisted or transmitted that shouldn't be.

## 5. Maintainability & style

- [ ] Single Responsibility — classes/methods do one thing; no god-objects/god-projects.
- [ ] No duplicated business logic (extract to helper for pure logic, service for logic with deps).
- [ ] Naming, formatting, and file organization match the project's conventions.
- [ ] Effort is right-sized: not gold-plated, not under-built — and never at the cost of §1.

## 6. Delivery hygiene

- [ ] `git status` is clean — the final intended state is committed (no stray uncommitted changes).
- [ ] Commit history is coherent (not contrived just to satisfy a "include .git" requirement).
- [ ] Build + test instructions match the actual project layout and work as written.
- [ ] README claims match the code (re-verified), not merely self-consistent.

## 7. Red-team pass (mandatory before verdict)

- [ ] Answered explicitly: **"What would a strict senior reviewer reject this for?"**
- [ ] Listed what could NOT be verified in this environment, and how to confirm it.
- [ ] Verdict separates the three axes: **correctness**, **testing**, **architecture** — so a strong
      score on two can't hide a weak score on the third.

---

## Prompt to paste when you want a review

```
Review this code against CODE-REVIEW-CHECKLIST.md.

Rules:
- Do section 1 (architecture & boundaries) FIRST and in full. Give me the
  file → project → layer table and flag every mismatch.
- Open every .csproj/manifest and list its references; flag any UI/infra
  framework pulled into a Domain/Core project.
- Verify the README's architecture claims against the actual files. Do not
  repeat what the docs say — prove or disprove it.
- Cite a file (and line/section) for every strength and every problem.
- Run `git status` and report it.
- Finish with the red-team pass: what would a strict senior reviewer reject
  this for, and what couldn't you verify here?
- Give separate verdicts for correctness, testing, and architecture.
```
