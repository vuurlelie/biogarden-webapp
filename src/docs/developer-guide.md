# Biogarden Developer Guide

This guide owns local setup and architecture notes. See the [documentation index](README.md) for UAT, verification, and operational procedures.

## Project Overview

Biogarden is a full-stack webshop and content site for Bakony Kincse Biokerteszet.

- Frontend: React, TypeScript, Vite, Tailwind-style utility classes, Playwright E2E tests.
- Backend: ASP.NET Core WebApi on .NET 9, Entity Framework Core, ASP.NET Identity, SQL Server.
- Payments: SumUp checkout integration; availability depends on the environment configuration.
- Email: configurable SMTP/preview delivery, with a durable queue for order-creation confirmations.
- Operations: monitoring, backup, deployment, abuse protection, and configuration docs live under `docs/operations`.

## Repository Layout

From the repository root:

```text
src/backend                    .NET solution, WebApi, business logic, data access, tests
src/frontend/biogarden-client   React/Vite client and Playwright tests
src/docs                        developer, legal/accounting, and operations documentation
.github/workflows               CI checks
```

The working directory for most commands is `src`.

## Backend Setup

Prerequisites: .NET 9 SDK and an accessible SQL Server instance. The frontend uses Node.js 24 in CI and npm.

Restore the repository-local EF Core CLI from `src`:

```bash
dotnet tool restore
dotnet ef --version
```

The manifest at `src/.config/dotnet-tools.json` pins `dotnet-ef` to 9.0.8, matching the EF Core packages in `backend/Directory.Packages.props`. Update the tool version alongside those packages when upgrading EF Core.

Initialize local secrets once:

```bash
dotnet user-secrets set "Jwt:SecretKey" "<at-least-32-random-characters>" --project backend/Biogarden.WebApi/Biogarden.WebApi.csproj
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\SQLEXPRESS;Database=BiogardenDb;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;" --project backend/Biogarden.WebApi/Biogarden.WebApi.csproj
```

The WebApi project already declares its local user-secrets ID. Use a generated JWT secret, not the placeholder above. Runtime user secrets are loaded in Development.

**Migration configuration differs from runtime configuration.** The current `DesignTimeAppDbContextFactory` reads `ConnectionStrings:DefaultConnection` from `backend/Biogarden.WebApi/appsettings.Development.json` when present, otherwise `appsettings.json`; it does not load user secrets or environment variables. Configure the intended local database in that ignored Development file and confirm its server/database before applying migrations. Do not put credentials in the tracked settings file.

From `src`, enter `backend` for migration commands, then return:

```bash
cd backend
dotnet ef database update --project Biogarden.DataAccess --startup-project Biogarden.WebApi
cd ..
```

Run the API:

```bash
dotnet run --project backend/Biogarden.WebApi/Biogarden.WebApi.csproj
```

Visual Studio can also run `Biogarden.WebApi` directly.

## Frontend Setup

Install dependencies and run the client from `src`:

```bash
npm.cmd --prefix frontend/biogarden-client ci
npm.cmd --prefix frontend/biogarden-client run dev
```

Set `VITE_API_BASE_URL` outside source control so the frontend points to the API. On Linux/macOS or CI, use `npm` instead of `npm.cmd`.

## Verification and configuration

Use [engineering verification](verification.md) for build, test, formatting, and dependency checks. See [configuration and secrets](operations/configuration.md) for environment settings and commit safety.

## Architecture Notes

Backend layering:

- `Biogarden.WebApi`: controllers, middleware, health checks, options, authentication, app startup.
- `Biogarden.BusinessLogic`: services, payment/email/order/cart orchestration, business contracts.
- `Biogarden.DataAccess`: EF Core entities, DbContext, migrations, repositories.
- `backend/tests`: unit and WebApi integration-style tests.

Frontend ownership:

- Feature API clients use shared request/error behavior where possible.
- Checkout, account, admin, cart, product, post, analytics, and contact concerns are grouped under `src/features`.
- Route components in `src/pages` should orchestrate feature components rather than owning large reusable UI blocks.
- Shared components are reserved for cross-feature patterns such as account shells, admin list states, layout, SEO, and common utilities.

## Branch And Commit Style

Use focused branches for meaningful topics, with multiple commits when the topic naturally has steps. Good examples:

```text
test/characterization-coverage
refactor/backend-order-cart
refactor/frontend-code-quality
docs/code-quality-readiness
```

Commit messages use the conventional style already present in the history:

```text
refactor(frontend): centralize async error handling
test(checkout): cover private and company billing payloads
docs(readiness): document code quality status
```

## Acceptance and release

Use the [UAT checklist](uat-checklist.md) for client acceptance and the [deployment runbook](operations/deployment-runbook.md) for production release. A successful local build is not a deployment or client sign-off.
