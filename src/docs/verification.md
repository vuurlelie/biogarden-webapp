# Engineering verification

Use this checklist for code changes and the exact commit selected for deployment. The [CI workflow](../../.github/workflows/dotnet-ci.yml) is the executable reference; record its actual result for each PR rather than treating old test counts as current evidence.

## Backend

From the repository's `src` directory:

```bash
dotnet restore backend/Biogarden.sln --locked-mode
dotnet list backend/Biogarden.sln package --vulnerable --include-transitive
dotnet build backend/Biogarden.sln --configuration Release --no-restore
dotnet format backend/Biogarden.sln --verify-no-changes --no-restore
dotnet test backend/Biogarden.sln --configuration Release --no-build --no-restore
```

Review the dependency report as well as the exit status. If a running local API locks build outputs, stop that local process before building again.

## Frontend

From `src/frontend/biogarden-client`:

```bash
npm ci
npm audit --audit-level=moderate
npx playwright install chromium
npm run lint
npm run typecheck
npm run build
npm run verify:seo
npm run test:e2e
```

On Windows, use `npm.cmd` and `npx.cmd` if PowerShell blocks their script wrappers. Playwright starts its own frontend server on port 4173; an existing process on that port must be stopped first. Install Chromium once per environment and again when the Playwright version requires it.

Check the build's SEO output: a successful build with no fetched posts does not prove that production article previews were generated.

Finish with `git diff --check` and review `git status --short` from `src` for unintended tracked changes.

## What these checks establish

Backend unit tests cover business behaviour with mocked dependencies. API tests exercise HTTP handling, middleware, and authorization, with services replaced where needed. Playwright tests use mocked API responses and verify browser behaviour. These checks do not collectively prove a complete browser-to-API-to-SQL Server transaction or successful live SMTP/SumUp delivery.

Use the [UAT checklist](uat-checklist.md) to record deployed workflows, and the [deployment runbook](operations/deployment-runbook.md) for release smoke checks. Database concurrency, queue lease recovery, and real integration delivery require environment-level verification in addition to these automated suites.
