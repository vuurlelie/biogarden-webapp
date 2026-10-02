# Contact and account notification recovery

## Contact submissions

`POST /api/contact` requires a random `submissionId` and returns **202 Accepted** only after both business and visitor emails are stored atomically in `NotificationEmails`. This acknowledges receipt, not SMTP delivery. The form retains its submission ID for an unchanged retry while it stays mounted, and gives edited/new messages a new ID. It does not store contact details in browser storage. Reloading the page starts a new form; deduplication across a reload is not promised.

A unique event/kind index makes concurrent identical retries safe. Reusing an ID with different normalized content returns 409. Each recipient has an independent delivery record: a failed visitor confirmation does not replay a completed business notification. Pending message content remains available to authorized operators in the database; there is no new admin inbox screen.

`NotificationEmailOutbox:Enabled` defaults to true. The worker polls every 15 seconds, uses a two-minute database lease and a 30-second send timeout, and retries with exponential delays capped at one hour. Expired leases recover after a restart. Delivery is **at least once**: SMTP may accept a message before the application records completion, so a crash or ambiguous SMTP failure can still cause a duplicate. Submission deduplication cannot remove that SMTP limitation.

Contact payloads are retained while pending for up to 30 days. Successful delivery or expiry erases recipient, subject and bodies. The worker expires at most 100 records per iteration; disabled/stopped workers postpone cleanup until they resume. Event IDs, hashes, attempt counts and outcomes remain for deduplication and diagnosis. Monitor failures before expiry and recover accepted business messages manually if needed. Do not wait 30 days to investigate an outage.

## Operations and deployment

Apply `AddNotificationEmails` before deploying the matching API/frontend. The migration adds a separate table and does not change the order email outbox. Keep the table during application rollback: dropping it loses accepted requests. Older API versions send synchronously, so avoid running old and new deployments concurrently.

Use restricted database access to inspect pending items; do not expose their payloads in logs, public diagnostics or screenshots. A useful metadata-only query is:

```sql
SELECT Id, EventId, Kind, CreatedAtUtc, ExpiresAtUtc, Attempts,
       NextAttemptAtUtc, LeaseUntilUtc, CompletedAtUtc, Outcome
FROM NotificationEmails
ORDER BY CreatedAtUtc DESC;
```

After correcting SMTP, a pending item will retry automatically. An operator can advance `NextAttemptAtUtc` only for an uncompleted, unexpired item whose lease is absent/expired. Never reset a live lease or blindly resend a completed item. Verify the business mailbox before manually resending an ambiguous delivery. Alert on old pending messages, repeated attempts and `expired` outcomes using the existing operations process.

Local tests use captured email and isolated databases. Hosted verification still requires a contact submission, a controlled SMTP failure/recovery, a worker restart, and checking both recipients. When email is disabled, the existing preview sender handles queued items; enabling SMTP later does not replay completed preview messages.
