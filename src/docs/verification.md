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

### SQL Server integration tests

The `SqlServer` category runs the API and real business/data-access services against a fresh database, applying the complete EF migration history. External email, GLS and payment provider calls are replaced where needed; no SMTP or payment requests are sent. Each test deletes its own randomly named `BiogardenIntegration_*` database afterwards.

These tests are skipped unless `BIOGARDEN_TEST_SQLSERVER` is set. Use a dedicated local/test SQL Server instance with permission to create and drop databases. The fixture replaces the connection's database name and does not read application connection settings. For Windows LocalDB, from `src`:

```powershell
$env:BIOGARDEN_TEST_SQLSERVER = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True;Encrypt=False'
try {
    dotnet test backend/tests/Biogarden.WebApi.Tests --configuration Release --filter Category=SqlServer
} finally {
    Remove-Item Env:BIOGARDEN_TEST_SQLSERVER
}
```

CI supplies an isolated SQL Server service and runs this category explicitly. Coverage includes guest checkout with an existing account email, idempotent retry and ownership, transactional rollback when queue persistence fails, durable email retry across API restart, competing queue claims and expired leases, and SQL Server rowversion conflicts. Worker scheduling and live SMTP delivery remain deployment checks.

`ConcurrentCheckoutTests` sends overlapping HTTP requests against real services and SQL Server. A test-only EF save interceptor waits until both requests have read their data and are ready to save, then releases both; bounded waits prevent a missing participant from hanging the suite. It covers:

- Two independent guest sessions competing for the last item: one reservation-and-checkout succeeds, the other receives 409, final stock is zero, and exactly one order and two confirmation emails are persisted. Reservation is part of this flow because adding a cart item reserves stock in this project.
- Two simultaneous submissions of the same checkout key: exactly one creates an order. The other may return the existing order or 409 for a stale rowversion; a subsequent retry must return the original order with 200. Stock is deducted once and emails are queued once.
- Concurrent quantity increases in separate carts: one succeeds and one receives 409, so total reservations cannot exceed available stock. Physical stock is unchanged until checkout.

Reservation writes also check and advance the product's existing rowversion in the same save transaction as the cart change. They do not decrement physical stock. This optimistic guard can require a refresh/retry when reservations or inventory change concurrently, even if a later retry would have sufficient stock. The tests verify the intended conflict response and rollback; they are not a load/performance test.

If the test process is forcibly terminated, its database may remain. Review the disposable instance's `BiogardenIntegration_*` databases before manually removing leftovers.

`SessionRevocationTests` uses real Identity users, issued JWTs, and SQL Server to verify account-state checks, password-reset revocation, role changes, and concurrent administrator/self-deactivation requests. The concurrency tests synchronize requests after initial authentication and exercise the actual database lock and authorization recheck. The ordinary HTTP contract-test factory bypasses that SQL filter deliberately; it is not evidence for session security.

`IdentityOwnershipTests` adds real Identity/JWT and SQL checks for concurrent case-insensitive registration, expired/tampered JWTs, expired confirmation/reset tokens, reset-token reuse and cross-account use, cart/order ownership, saved delivery-address ownership, and public withdrawal lookup. Concurrent registration uses a save barrier and must create exactly one account and confirmation request; the loser receives the normal duplicate-email response. The public payment-status route intentionally exposes only its limited status contract: the checkout reference is derived from the order ID and is not an additional secret or an ownership check.

`CartSessionTests` exercises real cart services and SQL Server with separate request scopes. It covers last-item and overlapping-cart merges, third-party reservations, rejected merges without partial quantities, repeated/concurrent merging, simultaneous first additions for guest and user owners, expired-cart replacement, and separation of user carts from guest cookies. These are database/service integration tests; hosted cookie delivery is checked separately in the [deployment runbook](operations/deployment-runbook.md#cart-session-changes).

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

`cart-session.spec.ts` delays API responses to verify initialization before guest additions, ordered mutations and refreshes, recovery after initialization/mutation/merge failures, and cancellation of queued actions after login changes. It includes the real product page and a test-only cart consumer for deliberately overlapping actions. The consumer uses the production provider and HTTP client, is served only by the test's Vite entry replacement, and is not part of the production bundle. API responses are mocked; these tests do not establish hosted cookie delivery or cross-tab request ordering.

Check the build's SEO output: a successful build with no fetched posts does not prove that production article previews were generated.

Finish with `git diff --check` and review `git status --short` from `src` for unintended tracked changes.

## What these checks establish

Payment-attempt SQL tests and browser recovery scenarios are described in [payment recovery](operations/payment-recovery.md#deployment-and-verification), including the remaining live-provider checks.

Checkout-draft SQL and browser coverage, along with hosted verification requirements, is described in [checkout drafts](operations/checkout-drafts.md#deployment-and-verification).

Backend unit tests cover business behaviour with mocked dependencies. Regular API tests exercise HTTP handling, middleware, and authorization, with services replaced where needed. The SQL Server category adds real API-to-database order workflows as described above. Playwright tests use mocked API responses and verify browser behaviour. These checks do not collectively prove a complete browser-to-API-to-SQL Server transaction or successful live SMTP/SumUp delivery.

Use the [UAT checklist](uat-checklist.md) to record deployed workflows, and the [deployment runbook](operations/deployment-runbook.md) for release smoke checks. Hosting behaviour and real integration delivery require environment-level verification in addition to these automated suites.
