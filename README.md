# ERP-AI-Pro

Open-source ERP for a small Indian private-limited software company, built by [Dewiride](https://github.com/Dewiride-Open-Source). A modular monolith on .NET 10 with a Next.js 16 web application, signed in through Microsoft Entra ID, configured from Azure App Configuration and Key Vault, and deployed with Docker Compose on the company's own server.

The roadmap runs from user management through clients, vendors, GST-compliant finance, Indian statutory payroll, timesheets, HR and integrations, with AI capabilities woven into every module. See [docs/roadmap/ROADMAP.md](docs/roadmap/ROADMAP.md) for what is done, in progress and planned.

## Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 LTS, ASP.NET Core minimal APIs, EF Core, modular monolith (one schema per module) |
| Frontend | Next.js 16 App Router, React 19, TypeScript, Tailwind CSS 4, shadcn/ui, light + dark theme |
| Identity | Microsoft Entra ID (workforce tenant), backend-for-frontend cookie session |
| Data | SQL Server (owner's instance for development), Azure SQL in production |
| Configuration | Azure App Configuration (labels per environment) + Azure Key Vault |
| Observability | OpenTelemetry, Aspire dashboard locally |
| Testing | xUnit v3 on Microsoft.Testing.Platform, ArchUnitNET, Playwright (desktop + mobile, light + dark) |
| Delivery | GitHub Actions, Dependabot, CodeQL, Docker Compose on Ubuntu |

## Repository map

```
backend/     .NET solution — BuildingBlocks/, Hosts/{Api,HealthProbe}, Modules/<Domain>/<Module>/, Tests/
frontend/    pnpm workspace — apps/web (Next.js), packages/{config,ui}, e2e (Playwright)
infra/       Dockerfiles and Compose files
scripts/     roadmap CLI, verification runner, repository checks (Node 24, zero dependencies)
docs/        roadmap, architecture, guides, operations, ADRs, configuration and routing registers
.github/     workflows, composite actions, Dependabot, CodeQL configuration, templates
```

## Quick start

Prerequisites: .NET SDK 10, Node.js 24, pnpm 12 (`npm i -g pnpm@12`), Docker Desktop. Details in [docs/guides/local-development.md](docs/guides/local-development.md).

```bash
# API on http://localhost:5080 (health, /api/platform/system-info, /scalar)
cd backend && dotnet restore && dotnet build --no-restore && dotnet run --project Hosts/Api/Dewiride.Erp.Host.Api

# Web app on http://localhost:3000 (redirects to /login)
cd frontend && pnpm install --frozen-lockfile && pnpm dev
```

## Everyday commands

| Task | Command |
|---|---|
| Backend build + tests | `cd backend && dotnet build -warnaserror && dotnet test --solution Dewiride.Erp.slnx` |
| Frontend lint, typecheck, build | `cd frontend && pnpm lint && pnpm typecheck && pnpm build` |
| End-to-end tests | `cd frontend && pnpm e2e` (installs browsers with `pnpm e2e:install`) |
| Containers | `docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml up -d --build --wait` |
| Roadmap | `node scripts/roadmap/roadmap.ts next` · `start <id>` · `done <id>` · `build` · `check` |
| Full verification | `node scripts/verify/verify.ts [--e2e] [--docker]` |

## Documentation

Start at [docs/README.md](docs/README.md): architecture overview, module anatomy, dependency rules, naming, guides and the architecture decision records. Contributions follow [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md); security reports follow [SECURITY.md](SECURITY.md).

## Licence

[MIT](LICENSE) © 2026 Dewiride
