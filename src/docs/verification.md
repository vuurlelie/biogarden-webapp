# Engineering verification

Use this checklist for code changes and the exact commit selected for deployment. The [CI workflow](../../.github/workflows/dotnet-ci.yml) is the executable reference; record its actual result for each PR rather than treating old test counts as current evidence.

## Backend

### Reproducible restores

Run .NET commands from `src` or below so that `src/global.json` selects the .NET 10.0.4xx SDK feature band (minimum 10.0.401, servicing patches allowed). CI reads the same file. The EF tool version is separately pinned in `src/.config/dotnet-tools.json`; restore it with `dotnet tool restore` before migration commands. Frontend verification uses Node 24 and npm 11. The resolved application dependency versions come from the committed lockfiles, not the locally installed SDK or Node patch.

Use `dotnet restore backend/Biogarden.sln --locked-mode` and `npm ci` for an existing checkout. Keep all five NuGet `packages.lock.json` files and the frontend `package-lock.json` tracked. A locked restore must fail when project declarations and locks disagree; do not delete locks to get a green build. After switching branches, restore the new branch before building, and check `git diff --exit-code -- '**/packages.lock.json' '**/package-lock.json'` from the repository root.

For an intentional dependency change, update the project/central package declaration, run an ordinary restore once, review every affected lockfile (including transitive project dependencies), then run locked restore again. For npm, use an intentional `npm install` update and review both manifest and lock. Commit these changes together. Do not commit unexpected IDE-generated lock changes without comparing them to the branch's declarations.

Investigation of the earlier validation change (`966c89a`) shows a legitimate dependency-graph change: FluentValidation moved into BusinessLogic and FluentValidation.AspNetCore was removed. Dependent test-project locks consequently changed too. The historical uncommitted files are not available, so their exact cause cannot be established from history alone. On the current merged tree, SDK 10.0.401 and npm 11.6.0 restore the committed graph without lockfile changes; no package upgrade is needed for this work.

From the repository's `src` directory:

```bash
dotnet restore backend/Biogarden.sln --locked-mode
dotnet restore backend/Biogarden.sln --locked-mode --force -p:AuditPipeline=true
dotnet build backend/Biogarden.sln --configuration Release --no-restore
dotnet format backend/Biogarden.sln --verify-no-changes --no-restore
dotnet test backend/Biogarden.sln --configuration Release --no-build --no-restore
```

The audit restore checks direct and transitive dependencies and fails on moderate, high or critical advisories, or unavailable advisory data (NU1900/NU1905). Low-severity advisories do not block this policy, matching the npm moderate threshold. The opt-in `AuditPipeline` property keeps ordinary offline/local restores separate from release verification. CI must pass the online audit; an unavailable feed is not evidence of safety. See [NuGet audit configuration](https://learn.microsoft.com/nuget/concepts/auditing-packages). If a running local API locks build outputs, stop that local process before building again.

### CI execution and evidence gates

The ordinary backend job runs unit and HTTP contract tests, explicitly excluding `SqlServer` and `RealBrowser`. The dedicated integration job supplies its own SQL Server and runs each excluded category separately. `verification.runsettings` treats empty discovery as an error. After each run, `scripts/verification/check_results.py` also checks TRX summaries, individual outcomes and expected assemblies: missing reports, zero tests, incomplete execution, any skipped test or a missing assembly fail the job. Local opt-in skips remain useful, but cannot make the required CI jobs green.

The frontend emits a Playwright JSON report checked for nonzero execution, zero unexpected failures and zero skips. Existing two-retry CI behaviour remains; tests passing on retry are reported as flaky and should be investigated. Backend TRX files, frontend JSON/HTML reports and failure traces are retained as GitHub Actions artifacts for 14 days, including after failed runs. Runs use fresh result directories on clean runners so previous passing results cannot hide missing execution. Locally, use a new results directory for each verification run.

Gate scripts use Python 3.12's standard library and have negative-case tests (`python -m unittest discover -s scripts/verification -p 'test_*.py'`). Raw artifacts stay ignored. Use synthetic test data only: traces and exception output can contain request payloads. For durable release evidence, record the commit and CI run link in the private release record rather than committing generated reports.

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

## Real browser-to-database journey

The opt-in `RealBrowser` category launches the actual API through [WebApplicationFactory with Kestrel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1.usekestrel?view=aspnetcore-10.0), seeds a disposable SQL database, and starts Playwright/Vite. Install frontend dependencies and Playwright Chromium/Firefox first (`npx playwright install chromium firefox`). Node must be on PATH. The test owns HTTPS API port 5097 and HTTPS frontend port 4174; keep both free. Each Chromium desktop, Firefox desktop and Chromium mobile case gets a fresh database. A one-hour self-signed certificate is created for the run and removed afterwards; certificate trust is bypassed only inside the test, without changing the machine trust store.

From `src` in PowerShell:

```powershell
$env:BIOGARDEN_TEST_SQLSERVER = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True;Encrypt=False'
$env:BIOGARDEN_REAL_BROWSER = '1'
try {
    dotnet test backend/tests/Biogarden.WebApi.Tests --filter Category=RealBrowser
} finally {
    Remove-Item Env:BIOGARDEN_TEST_SQLSERVER, Env:BIOGARDEN_REAL_BROWSER
}
```

Run this through the .NET test, not by pointing Playwright at a hosted API. The test selects its own database name, injects captured email, disables real payment delivery, and cleans up the database after success or failure. Browser requests are limited to loopback hosts. The existing mocked browser suite remains separate in `e2e`; `e2e-real` is selected only by `playwright.real.config.ts`.

The checkout journey uses two isolated browser contexts, adds a guest item, signs in through the real login endpoint and merges it with an existing user cart. It completes a pickup order, intentionally drops the response **after the real API has committed**, recovers the saved order, and cancels through the account screen. SQL assertions check one order, the authoritative total, merged quantity, exactly one pair of queued confirmations, and restored stock. No checkout response is fabricated. The same run verifies actual HTTPS cookie flags, guest-session separation, checkout modal focus/Escape behaviour, and a mobile viewport. A second scenario starts with an expired SQL cart, verifies it is replaced and cannot be checked out, checks rejection of an unavailable product, and proves draft/deleted article URLs do not render their content while a published article does.

These are loopback, same-site browser tests. They do not reproduce SmarterASP's different-domain deployment, third-party-cookie blocking, proxy TLS termination, Safari/iOS, a real card charge or SMTP delivery. Those remain explicit hosted checks in the deployment runbook. Failure traces contain only synthetic test data and can be uploaded by CI; do not reuse real customer data in this harness.

## What these checks establish

Payment-attempt SQL tests and browser recovery scenarios are described in [payment recovery](operations/payment-recovery.md#deployment-and-verification), including the remaining live-provider checks.

Checkout-draft SQL and browser coverage, along with hosted verification requirements, is described in [checkout drafts](operations/checkout-drafts.md#deployment-and-verification).

Backend unit tests cover business behaviour with mocked dependencies. Regular API tests exercise HTTP handling, middleware, and authorization, with services replaced where needed. The SQL Server category adds real API-to-database order workflows as described above. The default Playwright suite uses mocked API responses. The separate `RealBrowser` category proves the browser-to-API-to-SQL journey described above when explicitly run; neither suite proves successful live SMTP/SumUp delivery.

Use the [UAT checklist](uat-checklist.md) to record deployed workflows, and the [deployment runbook](operations/deployment-runbook.md) for release smoke checks. Hosting behaviour and real integration delivery require environment-level verification in addition to these automated suites.
