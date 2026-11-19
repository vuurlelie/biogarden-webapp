# Expired unconfirmed accounts

Registration only creates the requested account and queues its confirmation message. It does not enumerate or delete older accounts.

An expired account's email address becomes available for a new registration only after cleanup has successfully deleted that account. Until then, registration retains the normal duplicate-email response; it does not bypass uniqueness or perform synchronous maintenance.

`UnconfirmedAccountCleanupWorker` processes one bounded batch on startup and then once per interval. Defaults are:

| Setting | Default | Allowed range |
| --- | --- | --- |
| `UnconfirmedAccountCleanup:Enabled` | `true` | boolean |
| `UnconfirmedAccountCleanup:BatchSize` | `100` | 1–1000 |
| `UnconfirmedAccountCleanup:IntervalMinutes` | `60` | 1–1440 |
| `AuthEmail:UnconfirmedAccountRetentionDays` | `14` | positive days |

Use double underscores for environment variables, for example `UnconfirmedAccountCleanup__BatchSize=100`. The retention cutoff is strict: an account exactly at the cutoff is retained until a later batch.

Only active, unconfirmed accounts older than the retention period qualify. Confirmed, deactivated and anonymized accounts are preserved. Accounts with roles, orders, checked-out carts or account-lifecycle records are excluded and remain subject to the explicit account-management workflows.

Each deletion owns a transaction and the same SQL application lock used by account-security mutations. The worker rechecks eligibility after obtaining the lock; Identity concurrency checks also protect confirmation updates. Multiple API instances can run the worker without deleting the same account twice. Open carts are removed with the account. Saved addresses are removed only if no other user or historical order references them. All these changes roll back if Identity or persistence rejects deletion. No account-deletion notice is sent for an expired registration.

Failures log a sanitized error type and leave the account eligible for a later attempt. A failed account does not prevent attempts for the remaining selected batch. Persistent failures can consume batch capacity: monitor repeated error logs and investigate the underlying database/Identity problem. Completion logs include the deletion count and batch limit. A backlog larger than the limit drains over subsequent intervals rather than extending registration or running an unbounded loop. On IIS this job runs only while the application is running; it is not a guaranteed wall-clock deletion schedule.

To pause maintenance, set `UnconfirmedAccountCleanup__Enabled=false` and recycle the application. Do not change account confirmation timestamps or delete migration-history rows to work around a cleanup failure. Test data and fault injection belong in disposable local databases, not the hosted UAT database.
