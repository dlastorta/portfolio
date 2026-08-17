# tech-stack.default.md — sensible defaults for a fresh .NET Web API

> **Opt-in template.** Copy this file to your target repo's root as `tech-stack.md`
> and delete anything you don't need. It's a starting point, not a prescription.
>
> The workflow's role files explicitly instruct the agent to right-size to the
> ticket. This file just answers "IF a category is needed, here is a
> defensible default library". Do NOT add categories the ticket doesn't
> require, and do NOT keep packages you never call.
>
> The list below is derived from Mukesh Murugan's public "10 default packages"
> plus additions from real greenfield builds.

---

## Runtime

- **.NET 9** (or latest LTS matching your team's baseline). Pin the SDK version in
  a `global.json` at the repo root — reproducibility beats "latest".
- C# 12 language features assumed.

---

## Data — EF Core + a provider

```
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package <one provider — pick per project>
```

Provider choice depends on infrastructure:

- **SQL Server** → `Microsoft.EntityFrameworkCore.SqlServer`
- **PostgreSQL** → `Npgsql.EntityFrameworkCore.PostgreSQL`
- **SQLite** (local-only tools, embedded apps) → `Microsoft.EntityFrameworkCore.Sqlite`

Central package management: put versions in a `Directory.Packages.props` at the
repo root. Reduces version drift across multi-project solutions.

---

## Validation — rules live outside the model

Add only if the API accepts non-trivial input that needs conditional rules
(cross-field constraints, format validation, size caps). For simple `[Required]`-style
checks, data annotations on the DTO are enough — no package needed.

```
dotnet add package FluentValidation.AspNetCore
```

**Placement principle**: for command/query envelope validation where the whole
request must be valid, register in MediatR's `ValidationBehavior` pipeline. For
per-item validation in a batch where a bad item must not abort the batch, call
the validator explicitly at the top of the handler.

---

## Logging — structured events, not flat strings

```
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
```

Add additional sinks per deployment target — file, Application Insights, Loki,
etc. Structured logging is not optional in production; `Console.WriteLine`
loses correlation and query-ability.

---

## Auth — passwords need hashing

Add only if the API stores its own passwords (local Basic Auth, custom user
tables). Skip if using JWT + external identity provider (Azure AD, Auth0, etc.).

```
dotnet add package BCrypt.Net-Next
```

Work factor 11 is a defensible default for modern hardware (~200ms per verify).
BCrypt is preferred over PBKDF2 or plain SHA-family for password storage.

---

## API docs UI

Skip if the API has no external consumers or if README `curl` examples are
enough. Otherwise:

```
dotnet add package Scalar.AspNetCore
```

The OpenAPI document itself is built into ASP.NET Core (`Microsoft.AspNetCore.OpenApi`,
already in the framework) — Scalar just gives it a UI. Swashbuckle is the older
alternative; Scalar is lighter and more modern.

---

## Resilience — retries, timeouts, circuit breaker

Add when the API calls external HTTP dependencies that can fail transiently.
Skip for pure CRUD-on-own-DB services where the only external dep is your own
database (EF Core has its own retry story via `EnableRetryOnFailure`).

```
dotnet add package Microsoft.Extensions.Http.Resilience
```

Microsoft's first-party Polly-based resilience. Prefer this over adding raw
`Polly` unless you need Polly features the wrapper doesn't expose.

---

## Health checks — required for k8s / load balancer probes

```
dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks
```

Expose at `/health` (liveness) and `/ready` (readiness). Include DB connectivity
in `/ready`, keep `/health` cheap so a hanging DB doesn't kill the pod.

---

## Testing — three tiers, one framework

**Test framework**:

```
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package Microsoft.NET.Test.Sdk
```

**Assertions — pick ONE style and stay consistent**:

- **xunit built-in** (`Assert.Equal`, `Assert.True`, ...) — no package, no fluency
- **FluentAssertions** — more readable, adds a dep: `dotnet add package FluentAssertions`
- **Shouldly** — similar to FluentAssertions, less common in .NET ecosystem

Mixing styles across a codebase is worse than either choice alone. Pick one at
project start; enforce in code review.

**Mocking**:

- **Moq** — most common; battle-tested; occasional performance concerns
- **NSubstitute** — cleaner syntax, less setup ceremony; Mukesh's preference

**Integration testing against a real DB**:

```
dotnet add package Testcontainers.<provider>
```

E.g., `Testcontainers.MsSql`, `Testcontainers.PostgreSql`. Spins up a real DB in
Docker for each test class — catches SQL-dialect bugs that fake providers
(SQLite, EF InMemory) miss.

**Coverage collection**:

```
dotnet add package coverlet.collector
```

Usually included in test project templates but explicit is safer.

---

## What is BUILT INTO the framework (do NOT add a package)

Many features that historically required packages are now in the base framework.
Adding a package for these is friction without gain:

- **Configuration** — `IConfiguration` chain: `appsettings.json` → `appsettings.{Env}.json`
  → User Secrets (dev) → env vars → cloud secret provider (production). Framework built-in.
- **Options pattern** — `IOptions<T>` / `IOptionsSnapshot<T>` / `IOptionsMonitor<T>`. Built-in.
- **Dependency Injection** — `IServiceCollection` + `IServiceProvider`. Built-in and sufficient
  for 99% of services. Don't add Autofac unless you need decorators or property injection.
- **Background services** — `IHostedService` + `BackgroundService`. Built-in. Skip Hangfire
  unless you need scheduled jobs with persistence.
- **In-memory cache** — `IMemoryCache` via `Microsoft.Extensions.Caching.Memory`. Built-in via
  hosting namespace. Add Redis (`Microsoft.Extensions.Caching.StackExchangeRedis`) only when
  cache must survive process restart or be shared across instances.
- **Rate limiting** — `Microsoft.AspNetCore.RateLimiting` built-in since .NET 7. No need for
  `AspNetCoreRateLimit` (third-party) unless you need distributed state out of the box.
- **Request timeouts** — `AddRequestTimeouts` + `WithRequestTimeout(policyName)` built-in
  since .NET 8.
- **OpenAPI document** — `Microsoft.AspNetCore.OpenApi` built-in since .NET 9. Only need a
  package for a UI (Scalar, Swashbuckle).
- **Minimal APIs vs Controllers** — both built-in. Prefer Minimal APIs for new services;
  they are lower ceremony and faster to bootstrap.

---

## Explicitly NOT in these defaults

The following are common in enterprise .NET but require a ticket-level justification
to be added — they should NOT appear in a greenfield service by reflex:

- **MediatR** — command/query dispatch. Adds a ceremony layer over direct method calls.
  Worth adding when you have 3+ cross-cutting concerns (validation, logging, tx) you want
  to compose via pipeline behaviors, OR 15+ handlers where the discovery mechanism pays off.
- **Strategy / Chain-of-Responsibility patterns** — for < 3 concrete implementations, a
  switch expression is clearer and less ceremony.
- **Repository / Unit of Work over EF Core** — `DbContext` already is a Unit of Work and
  `DbSet<T>` already is a repository. Adding a wrapper is Repository-over-Repository unless
  you actually need to hide the ORM from Application (for a hard SQL→NoSQL migration path,
  or strict ORM-agnosticism enforced by tests).
- **AutoMapper** — hand-mapping is not slow enough to matter for typical CRUD DTOs, and
  AutoMapper's magic mapping errors are painful to debug. Add only if mapping surface is
  large AND stable.
- **OpenTelemetry with a custom `ActivitySource`** — Serilog structured logs cover most
  observability needs. Add OTel + custom activity sources only when you need distributed
  tracing across services (multi-service architecture) or when a specific vendor requires it.
- **Azure Key Vault / Managed Identity SDK** in the app — Configuration provider handles it
  via `Azure.Extensions.AspNetCore.Configuration.Secrets`. You rarely need `Azure.Security.KeyVault.Secrets`
  directly.
- **GraphQL, gRPC, SignalR** — three completely different network protocols. Add only when
  the ticket explicitly requires them; each carries significant infrastructure cost.
- **FluentValidation as a MediatR pipeline behavior** — see FluentValidation note above.
  Fine as fail-fast validator on commands; not fine as universal magic that hides validation
  from the handler.

If a ticket needs one of these, add a `Technical notes` section in the ticket
justifying it against the spec's actual scope, and revise this file.

---

## Scaffolding files worth having at repo root

Not packages, but standard bootstrap files that reduce friction:

- **`global.json`** — pins SDK version for reproducibility.
- **`Directory.Packages.props`** — central package version management across multi-project
  solutions; single place to bump versions.
- **`.editorconfig`** — style consistency across editors / IDEs.
- **`.gitignore`** — start from GitHub's `VisualStudio.gitignore` template.
- **`.gitattributes`** — usually just `* text=auto eol=lf` to normalize line endings
  across Windows / Mac / Linux contributors.
- **`docker-compose.yml`** — for local DB (and other external deps) via container. Pin
  `platform: linux/amd64` on the DB service if the image is x86-only, so Apple Silicon
  Macs use Rosetta 2 cleanly.
- **`README.md`** — setup steps, test commands, one-paragraph design summary.
- **`.cursor/rules/`** — per-repo constraints for AI-assisted work. See workflow docs
  for the `no-speculation.md` pattern.

---

## Attribution

Base list from Mukesh Murugan's public "My default stack for a new .NET API" post.
Additions from a real greenfield build:

- BCrypt for local password storage
- FluentAssertions / NSubstitute style guidance
- Coverlet for coverage collection
- Health checks for k8s / LB compatibility
- The "built into the framework" section — half the value of a defaults doc is knowing
  what you do NOT need to install
- The "explicitly NOT" section — same discipline that `.cursor/rules/no-speculation.md`
  enforces at the AI layer, applied at the operator layer
- Scaffolding files section — packages alone don't make a repo
