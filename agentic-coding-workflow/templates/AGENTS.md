# AGENTS.md — cross-tool agent conventions for this repo

This file is read by tools that respect the [AGENTS.md convention](https://agents.md/)
(Claude Code, Cursor, Copilot-compatible clients, and others). Rules here apply
to every agent-assisted change in this repo and are picked up automatically —
no per-prompt reminders required.

See also `.cursor/rules/no-speculation.md` (Cursor-specific right-sizing rules
this file composes with) and `tech-stack.md` (per-repo library and pattern
declarations).

## Fixes should make the system simpler, not more complex

Prefer removing or consolidating code over adding a new layer, flag, or
special case. If a fix grows the system's surface area, look for the
version that shrinks it.

- Every abstraction with fewer than three concrete users is a candidate
  for inlining.
- Every flag that toggles between two paths is a candidate for picking one
  and deleting the other.
- Every new package is a candidate for "does the framework already do this?"

The right-sizing discipline is not aesthetic — it is a token cost lever.
Less code = less context per prompt = cheaper reasoning and faster
convergence on every subsequent request.

## Comments policy — keep WHY and WARN, delete WHAT and WHERE

Comments that describe what the code does are noise. Comments that describe
non-obvious rationale or constraints are load-bearing. Categorize before
you write:

| Category | Example | Keep? |
|----------|---------|-------|
| WHAT — describes the obvious | `// increment counter` above `i++` | delete |
| WHERE — references stale docs | `// see the wiki`, bare `// TODO` without owner+ticket | delete |
| DEAD — commented-out code | `// old_impl();` | delete |
| SUPPRESS — lint / type suppressions without justification | bare `// eslint-disable-next-line` | delete (or add rationale on same line) |
| WHY — non-obvious rationale | `// intentional — see ADR-005 § timestamp rewind` | keep |
| WARN — non-obvious constraint | `// order matters — X must precede Y or Z breaks` | keep |
| API contract — docblocks on public surfaces | XML doc / JSDoc on public interfaces | keep (LLMs use them to infer intent) |

Rationale belongs in commit messages, PR descriptions, ADRs, or the code
structure itself. Comments should exist only when the surrounding artifacts
cannot carry the meaning.

Interpreter shebangs (`#!/usr/bin/env python3`) are executable directives,
not comments — they are always kept.

## File reading discipline

When you need to read a file to answer a question, prefer:

1. **Grep first, then targeted Read with offset+limit** — reads only the
   lines that match the pattern you care about.
2. **Full-file Read** only when the file is small (~200 lines or less) OR
   when you need the whole structure (e.g., understanding a class hierarchy
   or reading a config end-to-end).

Full-file reads on large files are token-expensive and rarely necessary.
The token cost of one full-file read on a 1000-line source file is often
larger than the entire prompt that requested it.

## Session hygiene

Long conversations accumulate context that ships with every subsequent
request. When starting a new work stream (different feature, different
file area, different question), open a fresh session instead of continuing
in the old one.

Long-lived sessions produce increasingly expensive prompts for decreasing
marginal value. Fresh sessions are cheap; churn on a bloated session is not.
