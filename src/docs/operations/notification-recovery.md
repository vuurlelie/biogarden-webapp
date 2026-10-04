# Contact and account notification recovery

## Contact submissions

`POST /api/contact` requires a random `submissionId` and returns **202 Accepted** only after both business and visitor emails are stored atomically in `NotificationEmails`. This acknowledges receipt, not SMTP delivery. The form retains its submission ID for an unchanged retry while it stays mounted, and gives edited/new messages a new ID. It does not store contact details in browser storage. Reloading the page starts a new form; deduplication across a reload is not promised.

A unique event/kind index makes concurrent identical retries safe. Reusing an ID with different normalized content returns 409. Each recipient has an independent delivery record: a failed visitor confirmation does not replay a completed business notification. Pending message content remains available to authorized operators in the database; there is no new admin inbox screen.

Contact acceptance retries transient SQL save failures (including deadlocks) up to three times, with delays capped at one second, and rechecks the stored submission before each attempt. Exhausted retries return an error without claiming acceptance; the unchanged form can retry the same submission ID. This retry policy is scoped to contact persistence and does not change payment or order transaction handling.

`NotificationEmailOutbox:Enabled` defaults to true. The worker polls every 15 seconds, uses a two-minute database lease and a 30-second send timeout, and retries with exponential delays capped at one hour. Expired leases recover after a restart. Delivery is **at least once**: SMTP may accept a message before the application records completion, so a crash or ambiguous SMTP failure can still cause a duplicate. Submission deduplication cannot remove that SMTP limitation.

Contact payloads are retained while pending for up to 30 days. Successful delivery or expiry erases recipient, subject and bodies. The worker expires at most 100 records per iteration; disabled/stopped workers postpone cleanup until they resume. Event IDs, hashes, attempt counts and outcomes remain for deduplication and diagnosis. Monitor failures before expiry and recover accepted business messages manually if needed. Do not wait 30 days to investigate an outage.

## Account notifications

Registration, password reset and account lifecycle changes commit their state and notification in the same SQL transaction. If notification persistence fails, the account change rolls back. SMTP runs only in the worker, after commit: an SMTP outage cannot turn a successful registration, password change, deactivation, reactivation, deletion or anonymization into a failed HTTP operation. The existing order-confirmation outbox remains separate.

| Operation | Successful HTTP response | Meaning |
| --- | --- | --- |
| Registration | 201 | Account created and confirmation request stored. |
| Resend confirmation / forgot password | 202 | Request accepted; response is identical for missing or ineligible accounts. |
| Confirm email / reset password | 204 | Account state changed; a successful password reset also queues a security notice. |
| Account lifecycle | Existing 200 / 202 | Account action completed / erasure request awaiting review; neither asserts email delivery. |

Confirmation and reset queue entries contain the user ID, email address and security stamp, **not a reset token or rendered link**. Immediately before delivery, the worker reloads the account and checks its current email, security stamp, active state, lockout and confirmation status. It generates a fresh Identity token only for an eligible account. Changed or deleted accounts produce a `suppressed` outcome and the queued personal data is erased. A password reset changes the stamp, invalidating older tokens and suppressing older queued reset requests. There is still a small interval between checking state and SMTP delivery; Identity validates tokens again when a link is used, so receiving an email does not guarantee its link remains valid.

Unsent reset requests expire after **one hour**, and confirmation requests after **24 hours**. These are queue lifetimes; Identity's configured token lifetime starts when the token is generated. After a prolonged outage the user can request a new link from the login/forgot-password page. Repeated eligible requests create separate messages and remain subject to the existing authentication rate limit; they are not deduplicated like contact submissions.

Password-change and lifecycle notices retain their rendered payload for at most **seven days** while pending. A deletion/anonymization notice temporarily retains the former email address for this purpose; the queue intentionally has no user foreign key. Successful delivery, suppression or expiry clears recipient, subject, bodies, user ID and security stamp. Metadata remains for diagnosis. Email previews, SMTP mailboxes and backups have separate retention policies; clearing the queue does not erase those copies.

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

Local tests use captured email and isolated databases. Hosted verification still requires a contact submission, a controlled SMTP failure/recovery, a worker restart, and checking both recipients. Also exercise registration/confirmation, forgot/reset password, and a disposable account lifecycle action. Verify that accepted changes survive SMTP failure and that links arriving after recovery work only for eligible accounts. IIS must keep the API process running for either email worker to run; use the hosting availability settings described in [order email delivery](order-email-delivery.md). When email is disabled, the existing preview sender handles queued items; enabling SMTP later does not replay completed preview messages.
