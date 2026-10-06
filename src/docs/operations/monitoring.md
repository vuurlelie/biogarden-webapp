# Monitoring And Incident Triage

This project exposes lightweight operational checks and correlation metadata so production issues can be diagnosed without exposing sensitive data.

## Health Endpoints

`GET /health/live`

Use this endpoint for basic process liveness checks. It returns healthy when the API process can receive HTTP requests.

`GET /health/ready`

Use this endpoint for readiness checks. It verifies that the API can connect to the configured database before traffic is considered safe to serve.

This is a connectivity check; it does not verify migration state, queue processing, SMTP, or SumUp. Verify those through the release/UAT procedures.

Recommended behavior:

- Liveness failures should restart the API process.
- Readiness failures should remove the API instance from traffic until the database connection is healthy again.
- Monitoring should alert on repeated readiness failures, not on a single short failure.

## Correlation IDs

Every API response includes an `X-Correlation-ID` header.

Clients and operators can also send an `X-Correlation-ID` request header. If the incoming value is present and reasonably short, the API uses it as the request trace identifier and returns the same value in the response.

Use the correlation ID to connect:

- the browser/network request seen by a user,
- the API error response `traceId`,
- server request logs,
- unexpected-error alert emails.

## Error Alerts

For pending order confirmations, retries, and background-worker availability, see [order email delivery](order-email-delivery.md).

Unexpected server errors are sent through the configured error-alert email path when `ErrorAlerts:Enabled` is true. Validation, authentication, not-found, and normal operation conflicts are intentionally not alert emails.

Alert emails should contain enough context to identify the failing request without including sensitive user input:

- correlation/trace ID,
- HTTP method,
- request path,
- whether a query string was present.

## Cart Cleanup

Monitor scheduled completion counts, repeated failures and backlog using [expired cart maintenance](cart-maintenance.md#hosting-and-monitoring). API health does not establish worker progress.

## Production Checklist

- Configure external uptime monitoring for `/health/live`.
- Configure load balancer or hosting readiness checks for `/health/ready`.
- Keep API logs long enough to investigate customer reports using `X-Correlation-ID`.
- Configure alert email delivery only with production SMTP credentials stored outside source control.
- Test one controlled unexpected-error alert before launch.

## UAT operational monitoring

`GET /health/operations` is a read-only signal for an external monitor. It returns only `Healthy` (200) or `Unhealthy` (503); it does not expose customer data, provider payloads, counts, or exception details. It does not call SMTP or SumUp, retry payments, or modify records. Database/query failures also make this check unhealthy. Keep it separate from `/health/ready`: operational incidents must not automatically remove the shop from traffic or restart its workers.

The default signals are:

| Condition | Threshold |
| --- | --- |
| Unsent order email or unexpired, incomplete notification | At least 5 attempts, or its next scheduled attempt is at least 30 minutes overdue |
| Failed, unprocessed provider event | Received at least 15 minutes ago |
| Payment creation requested but checkout ID still unknown | Intent created at least 15 minutes ago |
| Order requiring a refund | `RefundPending`; manual follow-up is required |

Configure these via `OperationalMonitoring__EmailFailureAttempts`, `OperationalMonitoring__EmailOverdueMinutes`, and `OperationalMonitoring__PaymentRecoveryMinutes`. Ordinary pending payments, declined cards, short email retry delays, completed messages and expired notifications do not trigger this signal. The overdue measure is the scheduled attempt time, not message creation time. Recovery is reflected on the next poll. This is not proof that every provider state matches the local database, and it does not detect missing callbacks for an otherwise known pending checkout.

### External checks to configure before hosted UAT

These checks are not installed by deploying the repository. Record the monitor owner, destination and successful notification test in the private UAT record.

- Poll the API's `/health/live`, `/health/ready` and `/health/operations` every minute. Notify after three consecutive failures, send one incident notification and one recovery notification; avoid an email on every poll. Probe the final public frontend URL separately too.
- Use a notification channel independent of the application's SMTP account. An unavailable API or SMTP server cannot reliably report its own outage.
- Monitor the final hostname's TLS certificate expiry; initially warn at 14 days remaining and escalate at 7 days. Verify the actual hostname and HTTPS after publishing.
- Check hosting disk quota and available space. The shipped Serilog configuration rolls daily and at 10 MiB, retaining at most 30 log files (approximately 300 MiB plus the current write/retention timing). This is retention, not a disk-space alarm; uploads and email previews consume space separately. Hosting overrides must preserve suitable limits.
- During supervised UAT, review failed login/429 spikes, checkout 5xx/503 responses and slow request durations in the logs. Automated rate/latency aggregation remains a later improvement; normal 400/401/409 and card declines should not each notify the operator.

### Error email delivery

`ErrorAlerts__Enabled` remains false by default. Hosted email alerts need an operator recipient and enabled/configured SMTP; with `Email__Enabled=false`, the email service only writes previews. Test delivery to a controlled recipient before relying on it.

The first qualifying unexpected exception sends an alert; matching alerts are suppressed for `ErrorAlerts__ThrottleWindowMinutes` (default 30) after completion. In-flight sends are deduplicated. `ErrorAlerts__SendTimeoutSeconds` defaults to 5 (allowed range 1–30), bounding how long alert sending can delay an error response. A failed or cancelled send instead uses `ErrorAlerts__FailureRetrySeconds` (default 60, allowed range 1–3600). A later matching error can then try again; there is no automatic durable alert retry queue. A sender ignoring cancellation may still finish late, so duplicate delivery is possible.

Suppression is per API process and resets on restart. Email alerts still share application SMTP and contain internal exception diagnostics; restrict the recipient and mailbox access. Persistent queue/payment signals are observed through the independent external monitor, including recovery-related 503s that do not trigger unexpected-500 email alerts.

### Verification and incident response

1. Verify healthy API, database and operations endpoints, plus public frontend HTTPS.
2. In an isolated test environment, simulate SMTP failure and a stalled/repeatedly failing queue. Confirm `/health/operations` fails, while `/health/ready` stays healthy when the database is available.
3. Restore SMTP and worker availability; confirm delivery and the monitor's recovery notification. Do not delete queue rows to silence an alert.
4. For payment incidents, inspect the affected intent/event and authoritative provider state. Use the existing recovery/refund workflow; never start a second charge or mark an order paid merely to clear monitoring.
5. Test an unexpected-error email, failed-send cooldown, external downtime notification and recovery using controlled test data. Never intentionally interrupt a client UAT session or create real charges for a monitoring test.

The SQL Server integration tests cover each unhealthy signal and recovery; unit tests cover email suppression, failed-send retry eligibility and send timeout. Live monitor configuration and notification delivery still require deployment verification.
