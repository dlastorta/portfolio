# Engineering portfolio — Diego Lastorta

Senior .NET / C# backend engineer, 12+ years. Most of my production work lives in private codebases, so this repo collects a few **substantive, sanitized artifacts** that show how I think about backend architecture and AI-assisted engineering — no employer names, no internal code, no business-specific detail.

## What's here

- **[modular-monolith-patterns](modular-monolith-patterns/)** — Modular monolith with Clean Architecture, CQRS, and MediatR pipeline behaviors. A written breakdown of the patterns *plus a runnable .NET reference implementation*: Domain / Application / Infrastructure / WebApi, a sample feature with commands, queries, validators and domain events, and a NetArchTest suite that fails the build if the layer boundaries are violated.

- **[agentic-coding-workflow](agentic-coding-workflow/)** — Plan → Review → Implement → Validate: a production agentic coding workflow with Cursor. The writeup plus the real (sanitized) prompt templates, role files, and a dependency-free reference orchestrator that ties the stages together with a human review gate and an adversarial validation step.

## How to read this

Each folder is self-contained with its own README. The technical repos are meant to be cloned and inspected — build and run instructions are in their READMEs. The emphasis throughout is judgment: not "AI/tools can do this," but *where they help, where they mislead, and how to keep the boundaries honest.*

## Contact

- Email: dlastorta@gmail.com
- LinkedIn: [linkedin.com/in/diegolastorta](https://linkedin.com/in/diegolastorta)
- GitHub: [github.com/dlastorta](https://github.com/dlastorta)
