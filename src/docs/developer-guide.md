# Biogarden Developer Guide

This guide gives a new developer or reviewer the shortest useful path through the project: what it is, how it is structured, how to run it, and how to verify production readiness.

## Project Overview

Biogarden is a full-stack webshop and content site for Bakony Kincse Biokerteszet.

- Frontend: React, TypeScript, Vite, Tailwind-style utility classes, Playwright E2E tests.
- Backend: ASP.NET Core WebApi on .NET 9, Entity Framework Core, ASP.NET Identity, SQL Server.
- Payments: SumUp checkout integration, disabled until production credentials are configured.
- Email: SMTP-based customer/admin notifications, disabled unless configured.
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

Restore the repository-local EF Core CLI from `src`:

```bash
dotnet tool restore
dotnet ef --version
```

The manifest at `src/.config/dotnet-tools.json` pins `dotnet-ef` to 9.0.8, matching the EF Core packages in `backend/Directory.Packages.props`. Update the tool version alongside those packages when upgrading EF Core.

Initialize local secrets once:

```bash
dotnet user-secrets init --project backend/Biogarden.WebApi/Biogarden.WebApi.csproj
dotnet user-secrets set "Jwt:SecretKey" "<at-least-32-random-characters>" --project backend/Biogarden.WebApi/Biogarden.WebApi.csproj
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\\SQLEXPRESS;Database=BiogardenDb;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;" --project backend/Biogarden.WebApi/Biogarden.WebApi.csproj
```

Apply migrations from `src`:

```bash
dotnet ef database update --project backend/Biogarden.DataAccess/Biogarden.DataAccess.csproj --startup-project backend/Biogarden.WebApi/Biogarden.WebApi.csproj
```

Run the API:

```bash
dotnet run --project backend/Biogarden.WebApi/Biogarden.WebApi.csproj
```

Visual Studio can also run `Biogarden.WebApi` directly.

## Frontend Setup

Install dependencies and run the client from `src`:

```bash
npm.cmd --prefix frontend/biogarden-client install
npm.cmd --prefix frontend/biogarden-client run dev
```

Set `VITE_API_BASE_URL` outside source control so the frontend points to the API. On Linux/macOS or CI, use `npm` instead of `npm.cmd`.

## Verification Commands

Run these before treating a branch as release-ready:

```bash
dotnet restore backend/Biogarden.sln --locked-mode
dotnet build backend/Biogarden.sln --no-restore
dotnet test backend/tests/Biogarden.UnitTests/Biogarden.UnitTests.csproj --no-build --no-restore
dotnet test backend/tests/Biogarden.WebApi.Tests/Biogarden.WebApi.Tests.csproj --no-build --no-restore
dotnet format backend/Biogarden.sln --verify-no-changes --no-restore
dotnet list backend/Biogarden.sln package --vulnerable
npm.cmd --prefix frontend/biogarden-client run typecheck
npm.cmd --prefix frontend/biogarden-client run lint
npm.cmd --prefix frontend/biogarden-client run build
npm.cmd --prefix frontend/biogarden-client run test:e2e
npm.cmd --prefix frontend/biogarden-client audit --audit-level=moderate
git diff --check
```

Playwright starts its own frontend server through its config. If local ports are busy, stop the existing dev server and rerun.

## Configuration And Secrets

Never commit real credentials, local `.env` files, `appsettings.Development.json`, logs, or generated build output.

Production needs these values from the site owner or host before real launch:

- Production SQL Server connection string.
- Strong JWT secret.
- Public frontend and API domains with HTTPS.
- SMTP host, username, password/app password, sender, and admin recipients.
- SumUp API key and merchant code.
- Public logo URL for email branding.
- Backup destination and retention policy.
- Production legal/accounting-approved wording.

See `docs/operations/configuration.md` for the full release configuration checklist.

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

## Production Readiness Boundary

The codebase can be technically ready before the live business is ready. Final production approval still requires:

- Production SumUp and SMTP settings.
- Legal/accounting review of receipt, invoice, privacy, and withdrawal wording.
- A real low-value payment test with the production SumUp account.
- Production backup and monitoring configured on the hosting platform.
- Final smoke test on the real public domains.
