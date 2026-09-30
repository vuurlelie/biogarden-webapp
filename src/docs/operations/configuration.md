# Configuration And Secrets

This project keeps secrets out of source control. Committed configuration may contain safe defaults and placeholders only. Real credentials belong in production environment variables or local ASP.NET user secrets.

Use this together with:

- [Deployment](deployment-runbook.md)
- [Backup and recovery](backup-recovery.md)
- [Monitoring](monitoring.md)

## Local Development

The [developer guide](../developer-guide.md#backend-setup) owns runtime secret setup and the separate migration configuration instructions. Optional local SMTP and SumUp settings can also use user secrets.

## Production Secrets

Configure these through the hosting provider's secret or environment variable system:

| Setting | Required when | Notes |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | Always | Production database connection string. |
| `Jwt__SecretKey` | Always | At least 32 random characters. Rotate if it was ever shared. |
| `Email__SmtpHost` | `Email__Enabled=true` | SMTP host. |
| `Email__Username` | SMTP requires auth | SMTP username. |
| `Email__Password` | SMTP requires auth | SMTP password or app password. |
| `ErrorAlerts__RecipientEmail` | `ErrorAlerts__Enabled=true` | Address for important error alerts. |
| `SumUp__ApiKey` | `SumUp__Enabled=true` | SumUp API credential. |
| `SumUp__MerchantCode` | `SumUp__Enabled=true` | SumUp merchant identifier. |

Non-secret operational settings should still be reviewed before release:

| Setting | Production expectation |
| --- | --- |
| `AuthEmail__ClientBaseUrl` | Public website URL. |
| `EmailBranding__LogoUrl` | Public HTTPS logo URL. |
| `SumUp__RedirectUrl` | Public success page URL containing `{CHECKOUT_REFERENCE}`. |
| `SumUp__ReturnUrl` | Public API callback/status URL. |
| `AbuseProtection__*` | Limits tuned after real traffic appears. |
| `OrderEmailOutbox__Enabled` | Defaults to true; false pauses queued order-confirmation delivery. See [order email delivery](order-email-delivery.md) for its interaction with SMTP preview mode. |

## Checkout pricing (backend contract)

`CheckoutPricing` in the API configuration owns delivery fees and enabled payment methods. These are provisional UAT values, not a GLS contract or live carrier quote. Product prices remain in the database. `DeliveryFees` uses named fulfillment keys; only configured methods with an allowed payment combination are offered. The initial backend configuration enables personal pickup (0 HUF) and home delivery (2740 HUF). Locker and CsomagPont activation follows GLS location validation in a later commit.

`EnabledPaymentMethods` uses `OnlineCard`, `PayAtPickup`, and `CashOnDelivery`. Pickup permits online card or payment at pickup; deliveries permit online card or COD. Actual online payment also needs the separate SumUp configuration. `CodLowerFee`, `CodUpperFee`, `CodLowerLimit`, and `CodMaximum` configure provisional COD tiers (600/1190 HUF, 30000/499999 HUF). The tier basis is the final collected amount, including products, delivery and COD fee: choose the lower fee if it fits the lower limit, otherwise the upper fee, and reject amounts above the maximum. The branch supports HUF only and rounds each product line to whole forints, midpoint away from zero, before summing; fees must be nonnegative whole forints. Invalid configuration fails startup.

`GET /api/orders/checkout-methods` returns configured method combinations. `POST /api/orders/quote` accepts numeric `fulfillmentMethod` and `paymentMethod` identifiers and derives the cart from the authenticated user or existing guest session. Its response includes line items, currency, subtotal, delivery fee, payment fee, total and a comparison `version`. New order requests must send the selected `paymentMethod` and `quoteVersion`. The server recalculates; a changed quote returns 409 without persisting stock, order, or email effects. Repeated submissions of an existing order retain the original snapshot. The version is an optimistic comparison token, not a signed payment authorization or a guaranteed price reservation.

API identifiers: fulfillment `PersonalPickup=0`, `HomeDelivery=1`, `ParcelLocker=2`, `ParcelShop=3`; payment `OnlineCard=0`, `PayAtPickup=1`, `CashOnDelivery=2`. Use named constants in clients. Historical order snapshot fields are nullable: never infer a historical payment method or fee from today's configuration.

This is the first backend commit of the pricing branch. **Do not deploy it with the existing checkout frontend**: quote consumption, GLS selection and display integration are subsequent steps. Apply `20260930063759_AddOrderPricingSnapshots` before the completed branch's API deployment. The additive migration preserves existing totals and leaves legacy fee/currency/payment fields null. Its downgrade removes the new snapshot fields and loses their data; retain the additive schema when rolling back application binaries where possible. No application/UAT database is migrated as part of generating or testing this migration.

## SumUp Release Setup

Do not commit real SumUp values. When the production account details are available, enter them only through the production host's secret/environment setting UI:

```text
SumUp__Enabled=true
SumUp__ApiKey=<real SumUp API key>
SumUp__MerchantCode=<real merchant code>
SumUp__Currency=HUF
SumUp__ApiBaseUrl=https://api.sumup.com
SumUp__RedirectUrl=https://<frontend-domain>/rendeles-sikeres?sumup_checkout_reference={CHECKOUT_REFERENCE}
SumUp__ReturnUrl=https://<api-domain>/api/sumup/checkout-status
```

Before enabling real customer payments:

- Confirm the API and frontend domains use HTTPS.
- Confirm `SumUp__RedirectUrl` contains `{CHECKOUT_REFERENCE}` exactly once.
- Confirm `SumUp__ReturnUrl` points to the public API endpoint reachable by SumUp.
- Place a low-value test order using the production SumUp account.
- Verify the customer return page changes from checking to successful payment.
- Verify the order appears as paid and confirmed in the admin order list.
- Verify a row is created in `PaymentProviderEvents` with `ProcessedAtUtc` set and no `ProcessingError`.
- Verify failed or cancelled payment can be retried from the checkout page.

## Frontend Environment

### Guest cart cookies

The guest cart uses the API's `biogarden_cart_id` HttpOnly cookie. HTTPS responses set `SameSite=None; Secure; Path=/` so a frontend on a different site can send it with credentialed API requests. Plain-HTTP local development uses `SameSite=Lax`. Ensure the API sees the original HTTPS scheme when hosted behind a trusted proxy.

Browsers that block third-party cookies can still prevent guest-cart persistence across unrelated domains. Prefer hosting the frontend and API under the same HTTPS site (or using a same-origin API reverse proxy) for reliable support across browser privacy settings. `SameSite=None` alone cannot override third-party-cookie blocking.

After deploying, check in a signed-out browser that adding two different products, opening the cart and reloading preserves both items. Inspect the API cookie's storage and any browser blocking reason if every operation creates a new cart. API tests verify cookie attributes and SQL-backed session continuity; they do not reproduce every browser's third-party-cookie policy.

Set the frontend API base URL outside source control:

```bash
VITE_API_BASE_URL=https://your-api-domain.example
```

The frontend has a browser Content Security Policy in `index.html`. When production domains are final, update the policy so `connect-src` and `img-src` allow only the real API/static asset origins and no unnecessary local origins remain.

If the frontend host supports response headers, also configure:

- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `X-Frame-Options: DENY` or a CSP `frame-ancestors 'none'` directive

## Authentication Sessions

Checkout draft cleanup is enabled by default through `CheckoutDrafts:CleanupEnabled`. Keep it enabled on hosted environments and verify worker availability; see [checkout draft retention](checkout-drafts.md#retention-and-cleanup) for limits and operational checks.

JWTs include the Identity security stamp. Every authenticated token is checked against the current account state, security stamp, and roles in SQL Server. Deleted, deactivated, anonymized, unconfirmed, or locked-out accounts cannot use an existing token. Password reset and account-security changes invalidate earlier sessions; reactivation requires a new login. A valid customer session accessing an admin endpoint receives 403; an invalid session accessing a protected endpoint receives 401.

Deploying the session-revocation change requires **no database migration**, but tokens issued before this change have no stamp and are invalidated. Tell UAT users to sign in again. Deploy the API and frontend from the same release. This design adds account/role reads per authenticated request and requires database availability; it does not cache revoked authorization.

Account lifecycle and admin account mutations use a SQL Server session application lock (`Biogarden.AccountSecurity`) and recheck authorization after acquiring it. This prevents overlapping API requests from removing the last active administrator across multiple API instances. A dedicated unpooled connection owns the lock and releases it on disposal; a five-second acquisition timeout produces a recoverable conflict. Verify `sys.sp_getapplock` is available to the deployed database user. Direct database edits and future background account-management jobs must preserve the same invariant; the HTTP filter does not guard arbitrary external SQL operations.

Revocation prevents subsequent protected requests. It does not cancel arbitrary requests that have already passed authorization. Account-security mutations have the additional serialized recheck described above. Normal logout clears the browser session and its other tabs; it is not a server-side revoke-all operation for a separately copied bearer token.

The frontend synchronizes login/logout across tabs, clears expired sessions on a timer or window focus, removes account-specific screens and checkout drafts after session changes, and ignores old-session responses. A 401 for an older token cannot log out a newer login. Unrelated guest 401 responses and authenticated 403 responses do not clear the current session.

After deployment, verify an active login, a customer denied admin access, admin demotion, deactivation/reactivation, password reset, two-tab logout, and session expiry. The SQL integration tests cover real JWT/Identity revocation and concurrent administrator changes; browser tests cover local session transitions with mocked API responses.

## Commit Safety

Before committing, check for accidental secrets or generated artifacts:

```bash
git status --short --ignored
git diff --cached
```

Do not commit:

- `appsettings.Development.json`
- `.env` or `.env.*`
- log files
- `bin/`, `obj/`, `dist/`, `node_modules/`, or `artifacts/`
- completed private UAT records, client feedback, and local-only work notes; public documentation templates may be tracked
