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

Set the frontend API base URL outside source control:

```bash
VITE_API_BASE_URL=https://your-api-domain.example
```

The frontend has a browser Content Security Policy in `index.html`. When production domains are final, update the policy so `connect-src` and `img-src` allow only the real API/static asset origins and no unnecessary local origins remain.

If the frontend host supports response headers, also configure:

- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `X-Frame-Options: DENY` or a CSP `frame-ancestors 'none'` directive

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
