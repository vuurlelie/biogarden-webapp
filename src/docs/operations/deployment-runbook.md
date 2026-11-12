# Deployment, Migration, And Rollback Runbook

This runbook owns the release sequence for the Biogarden API and frontend. UAT is hosted on SmarterASP.NET; the steps remain provider-neutral where exact production controls have not been verified.

Use this together with:

- [Configuration](configuration.md)
- [Monitoring](monitoring.md)
- [Backup and recovery](backup-recovery.md)

## Release Inputs

For the browser-session release, apply `20261007065011_AddBrowserSessions` before starting the new API, and deploy the matching frontend in the same release. Configure the same-site HTTPS API domain, CORS and durable Data Protection key directory as described in [configuration](configuration.md#browser-authentication-sessions). Existing browser JWT logins are intentionally cleared; users sign in again. The new Data Protection application identity can also invalidate previously issued confirmation/reset links; request fresh emails when needed. Verify reload, cross-tab logout, idle expiry, active renewal, admin access, guest-cart merge and API restart persistence. Roll back frontend/API together; the added session table can remain during an application rollback.

Admin pagination changes the list responses for `/api/admin/orders`, `/api/admin/users`, `/api/admin/inventory`, and `/api/admin/orders/withdrawal-requests` from arrays to `{ items, page, pageSize, totalCount, totalPages }`. Deploy the matching frontend and API together; an older frontend cannot read the new response shape. Roll back both packages together. There is no schema migration for this change. Verify page navigation, search/filter reset, handling/deletion on the last page, and order/inventory conflict reload after deployment.

For the .NET 10 upgrade, confirm that the target SmarterASP.NET server has the ASP.NET Core 10 runtime and compatible IIS hosting support before uploading the API. Provider-wide support does not establish availability on the assigned server. Retain the previous API package and record its database compatibility for rollback.

Validate the upgrade using the [engineering checks](../verification.md), including the SQL Server tests. Apply the existing migration history to a disposable database and check for pending model changes; changing the target framework alone is not a reason to create or apply a migration. If EF reports model changes, review them and any resulting SQL separately before deployment.

After deployment, verify login and authorization, order creation, stock changes, payment verification, and queued email delivery after an API restart. Local test results do not establish that this hosting verification has happened.

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

### SmarterASP.NET environment evidence

Treat the UAT preview URL as a location to test, not proof of the deployed release. Before a refresh, complete the environment section of the private release record below using the actual hosting panel and browser network observations:

| Check | Evidence required |
| --- | --- |
| Frontend and API identity | Exact HTTPS origins, mapped hosting websites, deployment directories and current frontend/API package versions |
| Hosting runtime | Assigned server's ASP.NET Core 10/IIS support and the API's actual startup result |
| TLS and routing | Successful normal GET on each intended public origin, certificate validity for that hostname, expected redirects and API routing; a HEAD response alone is insufficient |
| Origin relationship | Whether requests are same-origin, same-site or cross-site; actual CORS origin/credentials response and guest cookie storage on supported browsers |
| Proxy scheme | Where TLS terminates, whether the application sees HTTPS, and any explicitly trusted forwarding configuration; do not trust arbitrary forwarded headers |
| Frontend headers | Effective response headers/CSP from the host plus the document policy; verify intended API/image/GLS origins, framing policy and SPA deep-link reloads |
| Availability controls | Actual API stop/start/recycle and frontend maintenance controls available on the assigned plan, plus worker availability during idle periods |
| Deployment artifacts | Release SHA, API/frontend package hashes and private package locations, frontend build-time API origin, retained rollback pair |
| Target data | Operator-verified database identity, applied migration list, upload directory, backups and restricted evidence references |

Do not record passwords, tokens, full connection strings, cookies or unredacted network exports. Build-time `VITE_API_BASE_URL` must match the intended API: changing host environment variables after a static frontend build does not rewrite the bundle. Record how package hashes/versions were associated with the deployed files; if that cannot be established, mark the deployed version unknown and resolve it before acceptance. Git `main`, a green CI run, and a working homepage do not identify the hosted binaries by themselves.

If a public URL returns 404, record the exact URL, request method, time, status and redacted response headers. Check the domain-to-site mapping, site status, target directory and frontend artifact before assigning a cause. Do not change DNS, SSL, bindings or the API origin as an incidental documentation task. Such a change needs its own operational review and relevant cookie/payment-link retests.

Do not start a production deployment if the target commit is not already reviewed and merged into `main`.

## Pre-Deployment Checks

For article and image validation changes, follow the [content deployment checks](content-validation.md#deployment-and-verification), including native image decoder verification and preservation of uploaded files. No schema migration is needed for this change.

For the payment recovery release, apply `20261001130531_AddPaymentAttempts` and follow the [payment deployment and verification notes](payment-recovery.md#deployment-and-verification). Deploy the matching API/frontend and preserve attempt history when considering rollback.

For admin concurrency and payment corrections, follow the [admin update deployment notes](admin-order-updates.md#deployment-and-verification). Apply its additive audit migration and deploy a matching frontend/API pair; older admin forms do not supply the required version.

For the pricing and GLS release, follow [checkout migration and deployment compatibility](checkout-pricing.md#migrations-and-deployment-compatibility). Deploy a matching API/frontend pair and complete the [pricing and real-widget verification](checkout-pricing.md#verification-and-release-evidence) before reopening checkout. That guide owns the required migration names, provisional rates and rollback limitations.

For the checkout-draft release, apply `20260929112302_AddCheckoutDrafts` before starting the new API and deploy the matching frontend. Follow the [draft-specific release checks](checkout-drafts.md#deployment-and-verification), including cleanup availability and guest session behaviour.

### Cart session changes

The cart consistency update requires no schema migration. Cart creation and mutations use a SQL Server application lock per owner, held on a dedicated connection through the operation. A merge locks its user destination and guest source. The database login must be able to execute `sys.sp_getapplock`; verify guest additions, signed-in additions, and login merging on the hosted database after deployment. Lock waits are bounded to five seconds and return a retryable 409 on timeout. This adds a database connection during cart operations; monitor connection usage under the expected UAT traffic.

Before deploying over an older version, check for existing duplicate active carts:

```sql
SELECT UserId, COUNT(*) AS ActiveCartCount
FROM Carts
WHERE UserId IS NOT NULL AND IsCheckedOut = 0 AND ExpiresAtUtc > SYSUTCDATETIME()
GROUP BY UserId HAVING COUNT(*) > 1;

SELECT SessionId, COUNT(*) AS ActiveCartCount
FROM Carts
WHERE UserId IS NULL AND IsCheckedOut = 0 AND ExpiresAtUtc > SYSUTCDATETIME()
GROUP BY SessionId HAVING COUNT(*) > 1;
```

If either query returns rows, pause cart traffic and let reservations expire, or review and reconcile the affected carts before resuming. Do not automatically sum or delete them: combined quantities may exceed stock. The application rejects duplicate active owners instead of silently choosing one cart. Its locks coordinate application instances; they do not constrain direct SQL edits or older API instances, so replace all API instances before reopening cart traffic.

User carts are resolved only by authenticated user ID. Guest lookup excludes user-owned carts, including legacy records with a session ID. After deployment, test adding the last available item as a guest, signing in to merge it once, and signing out without exposing the user's cart. Use a fresh browser context to verify that the first guest request establishes its cookie before additions; browser cookie policies remain a hosted verification requirement. The local HTTPS browser suite uses localhost and therefore does not establish cross-site cookie behaviour. On the deployed frontend/API origins, repeat guest add/refresh/checkout in Chrome, Firefox and the client’s supported mobile browsers, including private browsing or third-party-cookie blocking. Record failures explicitly; local green tests are not a substitute for this check.

### Migration checks

Run [engineering verification](../verification.md) on the selected commit and review its CI results. Resolve applicable UAT blockers before a production release.

Run the following migration commands from the repository's **src/backend** directory. Restore the local CLI with **dotnet tool restore** if needed. Confirm the design-time database configuration described in the [developer guide](../developer-guide.md#backend-setup).

Review pending EF Core migrations:

```bash
dotnet ef migrations list --project Biogarden.DataAccess --startup-project Biogarden.WebApi
```

Use an operator-approved connection to inspect the **target** database, not merely the local development configuration. A restricted read of `SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;` supplies the applied migration list. Compare it with the selected release's migration history, record the last applied and intended target migrations, and review every pending step. Keep database identity and query evidence private. Do not put a connection string in a shared command transcript or PR.

Generate and review the SQL migration script before applying it:

```bash
dotnet ef migrations script --idempotent --project Biogarden.DataAccess --startup-project Biogarden.WebApi --output ../artifacts/migrations/production.sql
```

Review the generated `src/artifacts/migrations/production.sql` from the exact release commit for destructive operations such as dropping columns/tables, changing non-null constraints, or rewriting data. Plan migration and recovery steps for any destructive change. Execute only against the verified target database after the backup gate.

## Backup Gate

Complete the [database backup checklist](backup-recovery.md#database-backup-checklist) and, where applicable, the [uploaded files checklist](backup-recovery.md#uploaded-files-backup-checklist). Record verified backups in the release record before proceeding. Confirm restore capability using the [restore drill](backup-recovery.md#restore-drill).

## Deployment Steps

1. Confirm the selected commit's CI, approved enabled-method scope, verified backups and compatible rollback pair. Record the host's actual controls before starting.
2. Stop checkout/write traffic using the available maintenance controls. Stop the API and all its workers; prevent automatic restart during migration/deployment. If the host cannot provide the required isolation, resolve that before changing the schema.
3. Preserve the existing API/frontend packages, host configuration and uploaded files. Prepare the new matching packages without deleting runtime uploads or replacing secrets with repository placeholders.
4. Apply the reviewed pending migrations to the verified database and read back `__EFMigrationsHistory`. Record the result, including when no migration was needed.
5. Deploy the API and frontend from the selected release while keeping public write traffic paused. Verify the frontend build's API origin and the actual upload/stop/start controls used.
6. Configure the target environment's secrets, enabled checkout methods, SMTP/payment modes and worker settings through private host controls.
7. Start the API and run health/startup checks. Verify that it uses the expected database and schema before checkout is reopened.
8. Run the applicable smoke checks with designated test data and approved integration modes. Confirm the deployed frontend/API pair and test a fresh browser as well as an old tab requiring refresh.
9. Reopen the approved UAT scope only when its checks pass; otherwise keep the affected workflow closed and follow the compatible rollback/forward-fix decision.

Record the actual SmarterASP.NET/IIS package upload, stop/start, and traffic controls used for the target environment before its release. Do not assume that deploying a package also applies database migrations.

## Post-Deployment Smoke Checks

For the current UAT origins and build/host values, use [current UAT configuration](configuration.md#current-uat-addresses-and-overrides). The public frontend is `https://raczkrisztina.dev`; the API is `https://api.raczkrisztina.dev`.

For order-reliability releases, apply `AddOrderConcurrencyToken` and `AddOrderEmailEventIdentity` before starting the new API. Conflicting order writes return HTTP 409: reload the current state before retrying. A retried verified payment for a cancelled order must remain cancelled and enter refund reconciliation, without reserving stock again. Monitor failed payment events and reprocess them with fresh state; `RefundPending` records work required and does not perform a provider refund.

For releases containing `AddNotificationEmails`, apply it before starting the matching API/frontend. Verify contact and account notification delivery using [notification recovery](notification-recovery.md), including the worker and SMTP settings.

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

Record the exact frontend/API origins and browser privacy settings for the [hosted cart checks](#cart-session-changes), not only a screenshot of a cart count. Confirm actual customer and business SMTP receipt, pending queue recovery after an API recycle, and account/contact notices using the owning [order delivery](order-email-delivery.md) and [notification recovery](notification-recovery.md) guides. Use designated recipients; introducing SMTP failure or replaying payment events belongs in an isolated, approved test window. A queued record, preview file or readiness response does not establish mailbox delivery.

Verify bounded [cart cleanup](cart-maintenance.md#release-verification) and [draft cleanup](checkout-drafts.md#deployment-and-verification) through worker evidence. Record the actual application availability/recycle settings; a worker cannot progress while IIS has stopped the API process. Use the [UAT scenario IDs](../uat-checklist.md#regression-traceability) as references in the private record instead of duplicating feature-specific procedures here.

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

1. Stop write traffic and the API/its workers using the recorded hosting controls. Preserve current data and diagnostics before taking recovery actions.
2. Decide whether the previous API/frontend pair supports the **current schema and newly written data**. Record this compatibility before restoring binaries. Additive migrations alone do not prove compatibility: older code may not understand new delivery values, payment attempts or queued notifications.
3. If compatible, redeploy both previous packages while preserving uploads and verified environment settings. Retain the additive schema unless a separately reviewed database action is necessary.
4. If incompatible, keep the affected scope closed and choose a reviewed forward fix or rehearsed database/file restore. Do not automatically run down migrations or replace the database with a backup that loses orders created after the backup point.
5. Reconcile order/payment events and both notification queues before resuming external delivery. A restored database can forget a provider charge or an already-sent email; rolling back binaries cannot reverse either external effect. Follow [backup recovery](backup-recovery.md#incident-recovery) and [payment recovery](payment-recovery.md).
6. Start the compatible application pair with approved integration modes, run smoke checks, then reopen only the verified scope.
7. Record the failed SHA, actual recovery action, restored versions, elapsed time, data reconciliation and follow-up issue.

Database rollback is the riskiest part. Prefer forward corrective migrations when data has already changed and customers may have created orders after the deployment.

## Release Record Template

```text
Release ID / date / timezone:
Environment / operator / verifier:
Selected full commit SHA / PR / CI run URL and result:
Approved UAT scope / client decision references:
Frontend origin / hosting site / deployed version evidence:
API origin / hosting site / deployed version evidence:
API package hash / private location:
Frontend package hash / private location / build-time API origin:
TLS / routing / CORS / cookie / header evidence:
Assigned runtime / actual stop-start-recycle controls:
Database identity (private; no connection string):
Applied migrations before / expected target / verified after:
Reviewed migration script hash / execution result:
Database backup reference / timestamp / restore validation:
Uploaded files backup reference / timestamp / validation:
Effective fulfillment-payment matrix / fees / payment mode:
Email mode / designated recipients (private) / worker settings:
Scenario ID / status / actual result / evidence / tester / time:
Outbox SMTP/restart evidence / cleanup evidence:
Restore drill reference / measured RPO-RTO / unresolved issues:
Rollback API-frontend pair / schema-data compatibility evidence:
Accepted exclusions / issue owners / retest dates:
Decision: Not ready / limited UAT scope / accepted UAT scope:
Decision owner / date / evidence:
```

This is a blank template. Complete it in a private tracker or ignored local `src/artifacts/` directory; keep a durable private copy outside the checkout. Use **Not run**, **Pass**, **Fail**, **Blocked** and **Not applicable** consistently with the UAT checklist. Unknown versions, unexecuted restore drills and untested SMTP/provider delivery remain explicitly unverified. Public documentation records the procedure, not client approvals, credentials, private hosting details or fabricated execution results.
