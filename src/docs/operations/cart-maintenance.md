# Expired cart maintenance

## Eligibility and retention

Reservations stop counting as soon as `ExpiresAtUtc <= now`, using the application's injected UTC clock. Physical deletion is separate: `CartCleanupService` deletes only carts that have been expired for at least the configured retention period, are not checked out, and have no linked order. The default retention is **seven days after expiry**.

Active carts, recently expired carts, checked-out carts (including retired merge sources), and carts referenced by any order are preserved. This applies to pending, paid, cancelled and historical orders, even if a legacy cart has an incorrect checkout flag. Cleanup neither cancels unpaid orders nor changes payment state or product stock. Deleting eligible carts cascades to their cart items. Cart rows and item reservations are not order or accounting records.

Personal checkout draft fields have a separate, shorter lifetime and purge process; see [checkout drafts](checkout-drafts.md#retention-and-cleanup). The seven-day cart retention does not extend draft availability or personal-data retention. Backups have their own [retention and recovery policy](backup-recovery.md).

## Configuration and trigger

`CartCleanup` settings are validated at startup:

| Setting | Default | Allowed values |
| --- | --- | --- |
| `Enabled` | `true` | `true` / `false` |
| `RetentionDays` | `7` | 1–365 days after cart expiry |
| `BatchSize` | `100` | 1–1,000 carts per run |
| `IntervalMinutes` | `60` | 1–1,440 minutes |

Environment overrides use double underscores, for example `CartCleanup__BatchSize=100`. Changes require an API restart. `Enabled=false` stops physical cart deletion; it does not extend reservation validity or disable the separate draft purge worker.

The API worker runs one batch on startup and waits the configured interval after each run. It processes the oldest eligible carts first and never drains an entire backlog in a tight loop. Each batch is one conditional SQL deletion; it does not load cart/item graphs or delete from a stale tracked snapshot. Existing expiry and order-cart indexes support the query. The order foreign key supplies an additional safeguard.

The limit applies per API process and per run. Multiple instances may each process a batch; overlapping work can contend or produce a transient database failure. Eligibility remains part of the SQL statement, and a failed batch is retried through the next scheduled run. No cluster-wide throughput limit is claimed.

## Hosting and monitoring

On IIS/SmarterASP.NET the worker runs only while the API process is alive. Verify application availability and recycling behavior on the host. Downtime delays physical cleanup; it does not make expired reservations valid again. At the defaults, one continuously running instance can delete roughly 2,400 eligible carts per day; monitor backlog before increasing a bounded batch size or reducing the interval.

Each completed run logs the deleted count, batch limit and retention days, including zero-result runs. Failures log an error type without cart IDs, customer fields, SQL parameters or exception messages. Configure log monitoring for repeated failures, missing expected completion entries, or persistent full batches. Health readiness alone does not prove that cleanup ran.

For restricted operator inspection, use aggregate counts only and match the configured retention:

```sql
DECLARE @cutoff datetime2 = DATEADD(day, -7, SYSUTCDATETIME());
SELECT COUNT_BIG(*) AS EligibleCartCount, MIN(c.ExpiresAtUtc) AS OldestExpiryUtc
FROM Carts AS c
WHERE c.IsCheckedOut = 0 AND c.ExpiresAtUtc <= @cutoff
  AND NOT EXISTS (SELECT 1 FROM Orders AS o WHERE o.CartId = c.Id);
```

## Release verification

No schema migration is required for this change. Deploy the tested API and review the configured retention before enabling it on the hosted database. In UAT, verify a completion log, a bounded decrease in eligible carts, and preservation of active and order-linked carts. Do not mark hosted cleanup verified from local test results.

SQL integration tests cover the exact retention boundary, ordering and batch limits, repeated execution, cascading item deletion, refreshed carts, cancellation, preservation of pending/paid orders and stock, and a real worker startup batch followed by shutdown. Reservation tests separately cover the injected expiry boundary and grouped query exclusions.

Disabling cleanup prevents future deletions. An application rollback cannot restore already-deleted expired cart rows; recovery, if actually needed, uses the reviewed database backup procedure. Retained orders and their snapshots are unaffected.

## Product availability and stock

`Available` is the administrator's sales setting. Saving zero stock, selling the last unit,
and cancelling an order do not change that setting. This preserves deliberate disabling
and allows restocking to restore sales for an enabled product.

Product list and detail responses also expose `inStock` (physical stock greater than zero).
The storefront requires both flags; the available/unavailable product filters use the same
rule. These are snapshots, not stock guarantees: the cart availability endpoint additionally
accounts for live reservations and the current cart. Cart mutation and checkout remain the
authoritative checks. No migration is required; previously disabled products remain disabled
until an administrator enables them.
