# Order confirmation email delivery

New orders and their customer/business confirmation emails are saved in one database transaction. An SMTP outage cannot turn a committed order into a failed checkout response. Each recipient has a separate `OrderEmails` row, uniquely identified by order and message kind. Retrying checkout returns the existing order without creating more email rows.

## Deployment

Apply the `AddOrderEmailOutbox` migration before starting this version of the API. Follow the database backup and migration review steps in the [deployment runbook](deployment-runbook.md). No existing orders are backfilled and no historical emails are resent automatically.

`OrderEmailOutbox:Enabled` defaults to `true`. Set it to `false` to pause delivery while keeping newly created messages queued. This is separate from `Email:Enabled`: when SMTP is disabled, the existing email service writes local previews, and the worker marks those messages as handled. Use that preview mode only in development/UAT; enabling SMTP later does not resend completed previews.

## Processing and recovery

- The API background worker polls every 15 seconds when idle and processes messages one at a time.
- A conditional database update grants a two-minute lease, preventing ordinary overlapping workers from claiming the same message. Sending has a 30-second cancellation timeout.
- Failures retry after 1, 2, 4, 8, 16, 32, then 60 minutes. Further attempts stay one hour apart until successful or administratively paused.
- A crash or shutdown leaves the message in the database. An expired lease allows a later worker to retry.
- Delivery is **at least once**. If SMTP accepts an email but the process stops before saving completion, the email can be delivered again. SMTP does not provide an atomic transaction with the application database.
- After delivery, recipient, subject, and body fields are cleared. The message ID, order ID, kind, attempt count, and completion timestamp remain for diagnostics and deduplication.

The worker runs only while the API process runs. Configure IIS/application-pool availability and confirm that hosting idle shutdown does not prevent timely notifications. Persistent queuing survives a restart but does not itself wake a stopped application.

## Monitoring

Worker logs record message IDs, attempt counts, and failure types without message bodies or recipient addresses. Check pending/repeated failures using:

```sql
SELECT Id, OrderId, Kind, Attempts, NextAttemptAtUtc, LeaseUntilUtc
FROM OrderEmails
WHERE SentAtUtc IS NULL
ORDER BY NextAttemptAtUtc;
```

Investigate an increasing backlog or repeated attempts: verify SMTP configuration, application uptime, and database access. Pending rows contain email content and should have the same access and backup protection as customer orders. Review repeatedly failing rows rather than deleting them automatically.

## UAT verification

With test recipients, confirm that an order succeeds while SMTP is unavailable, both notification rows remain queued, and restoring SMTP allows both messages to be sent. Retry the original checkout request and verify that neither the order nor its email rows are duplicated. Restart the API with a pending message and verify recovery after any lease expires.

This queue covers order-creation confirmations. Account verification, password reset, withdrawal requests, and order-status-change emails still use their existing delivery paths.
