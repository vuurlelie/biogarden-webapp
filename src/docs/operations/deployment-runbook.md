# Deployment, Migration, And Rollback Runbook

This runbook describes the production deployment path for the Biogarden API and frontend. It is intentionally host-neutral until the final hosting provider is selected.

Use this together with:

- `docs/operations/configuration.md`
- `docs/operations/monitoring.md`
- `docs/operations/backup-recovery.md`

## Release Inputs

Before deploying, collect:

- target branch and commit SHA,
- target environment,
- expected database name and server,
- frontend public URL,
- API public URL,
- SumUp production settings when online payment is enabled,
- SMTP settings when real email sending is enabled,
- planned deployment window,
- rollback owner.

Do not start a production deployment if the target commit is not already reviewed and merged into `main`.

## Pre-Deployment Checks

Run from `src`:

```bash
git status --short --branch
dotnet restore backend/Biogarden.sln
dotnet build backend/Biogarden.sln --configuration Release --no-restore
dotnet test backend/Biogarden.sln --configuration Release --no-build --no-restore
npm.cmd --prefix frontend/biogarden-client ci
npm.cmd --prefix frontend/biogarden-client run lint
npm.cmd --prefix frontend/biogarden-client run build
```

Review pending EF Core migrations:

```bash
dotnet ef migrations list --project backend/Biogarden.DataAccess/Biogarden.DataAccess.csproj --startup-project backend/Biogarden.WebApi/Biogarden.WebApi.csproj
```

Generate and review the SQL migration script before applying it:

```bash
dotnet ef migrations script --idempotent --project backend/Biogarden.DataAccess/Biogarden.DataAccess.csproj --startup-project backend/Biogarden.WebApi/Biogarden.WebApi.csproj --output artifacts/migrations/production.sql
```

Check `artifacts/migrations/production.sql` for destructive operations such as dropping columns, dropping tables, changing non-null constraints, or data rewrites. If a destructive change is present, plan a manual data migration and rollback path before continuing.

## Backup Gate

Before applying migrations or replacing the application:

- create a database backup,
- confirm the backup completed successfully,
- confirm the backup is stored outside the production database server,
- record the backup location and timestamp,
- confirm uploaded files are already backed up or take a fresh copy.

Do not continue until the backup can be found and restored into a non-production environment.

## Deployment Steps

1. Put the application into maintenance mode if the host supports it.
2. Stop background jobs or scheduled tasks if any exist.
3. Deploy the API package for the target commit.
4. Deploy the frontend build for the same target commit.
5. Apply the reviewed database migration script.
6. Configure production secrets and environment variables.
7. Start the API.
8. Start or swap frontend traffic.
9. Remove maintenance mode.

The exact package upload, app restart, and traffic-swap commands depend on the final hosting provider. Add the provider-specific commands here before the first real production deployment.

## Post-Deployment Smoke Checks

For releases containing `AddOrderEmailOutbox`, apply that migration before starting the new API and verify the worker can deliver a queued test message. See [order email delivery](order-email-delivery.md) for configuration, retry behaviour, and IIS availability requirements.

Run these checks immediately after deployment:

```bash
curl -i https://<api-domain>/health/live
curl -i https://<api-domain>/health/ready
```

Then manually verify:

- homepage loads,
- product list loads,
- product detail page loads,
- cart can add/update/remove an item,
- checkout can reach order review,
- account login works,
- admin login works,
- admin orders page loads,
- contact form sends or creates a disabled-delivery preview in non-production,
- payment flow reaches the expected disabled or provider state.

Check operational signals:

- API logs show no unexpected startup errors,
- `X-Correlation-ID` appears in API responses,
- error-alert delivery is configured for production,
- rate limit responses still return JSON with `traceId`.

## Rollback Triggers

Rollback if any of these happen after deployment:

- API cannot start,
- `/health/ready` fails repeatedly,
- login or checkout is broken,
- admin cannot process orders,
- payment confirmation creates incorrect order state,
- database migration damaged required data,
- error rate stays elevated after an initial warm-up period.

## Rollback Steps

1. Put the application into maintenance mode if possible.
2. Stop the API.
3. Re-deploy the previous known-good API package.
4. Re-deploy the previous known-good frontend build.
5. If the database changed, decide whether to use a down migration, a corrective migration, or restore from backup.
6. Start the API.
7. Run the post-deployment smoke checks again.
8. Record the incident, failed commit SHA, rollback action, and follow-up fix.

Database rollback is the riskiest part. Prefer forward corrective migrations when data has already changed and customers may have created orders after the deployment.

## Migration Safety Rules

- Never apply a migration to production before reviewing the generated SQL.
- Never run EF migration commands against production without confirming the connection string.
- Never apply schema changes without a fresh database backup.
- Avoid destructive schema changes during high-traffic periods.
- Keep migration scripts generated from the exact commit being deployed.

## Release Record Template

```text
Date:
Deployed commit:
API package:
Frontend package:
Database backup:
Uploaded files backup:
Migration script:
Started by:
Verified by:
Smoke check result:
Rollback package:
Notes:
```

