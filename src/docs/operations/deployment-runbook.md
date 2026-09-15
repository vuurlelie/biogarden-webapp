# Deployment, Migration, And Rollback Runbook

This runbook owns the release sequence for the Biogarden API and frontend. UAT is hosted on SmarterASP.NET; the steps remain provider-neutral where exact production controls have not been verified.

Use this together with:

- [Configuration](configuration.md)
- [Monitoring](monitoring.md)
- [Backup and recovery](backup-recovery.md)

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

Run [engineering verification](../verification.md) on the selected commit and review its CI results. Resolve applicable UAT blockers before a production release.

Run the following migration commands from the repository's **src/backend** directory. Restore the local CLI with **dotnet tool restore** if needed. Confirm the design-time database configuration described in the [developer guide](../developer-guide.md#backend-setup).

Review pending EF Core migrations:

```bash
dotnet ef migrations list --project Biogarden.DataAccess --startup-project Biogarden.WebApi
```

Generate and review the SQL migration script before applying it:

```bash
dotnet ef migrations script --idempotent --project Biogarden.DataAccess --startup-project Biogarden.WebApi --output ../artifacts/migrations/production.sql
```

Review the generated `src/artifacts/migrations/production.sql` from the exact release commit for destructive operations such as dropping columns/tables, changing non-null constraints, or rewriting data. Plan migration and recovery steps for any destructive change. Execute only against the verified target database after the backup gate.

## Backup Gate

Complete the [database backup checklist](backup-recovery.md#database-backup-checklist) and, where applicable, the [uploaded files checklist](backup-recovery.md#uploaded-files-backup-checklist). Record verified backups in the release record before proceeding. Confirm restore capability using the [restore drill](backup-recovery.md#restore-drill).

## Deployment Steps

1. Put the application into maintenance mode if the host supports it.
2. Stop the API and its background worker; prevent automatic restart during migration/deployment.
3. Deploy the API package for the target commit.
4. Deploy the frontend build for the same target commit.
5. Apply the reviewed database migration script.
6. Configure production secrets and environment variables.
7. Start the API.
8. Start or swap frontend traffic.
9. Remove maintenance mode.

Record the actual SmarterASP.NET/IIS package upload, stop/start, and traffic controls used for the target environment before its release. Do not assume that deploying a package also applies database migrations.

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
