# Online payment recovery

## Attempts and retries

Creating an order and starting a payment are separate operations. An order has one stable submission key; its `PaymentAttempts` rows retain the history of SumUp checkouts. Before contacting SumUp, the API saves a unique attempt reference. SQL Server application locks serialize payment startup and reconciliation for an order across API instances, with a five-second acquisition timeout. Order row versions still protect against competing admin updates and cancellations.

A repeated start request fetches the existing checkout. `PENDING` and `FAILED` checkouts reuse their hosted URL; a failed processing attempt is not proof that the checkout can never succeed. Only a verified `EXPIRED` checkout permits a replacement attempt. `PAID` confirms the order and prevents another payment start. Historical checkout IDs remain associated with the order.

The provider adapter rejects unknown or contradictory transaction histories. A `PAID` response must contain exactly one successful transaction with a non-empty code and matching amount, currency and merchant. Pending or refunded transactions require reconciliation rather than enabling another payment. The same paid-code requirement applies when a customer retries and when a notification is handled. Reopening a failed checkout keeps the verified failure state until new provider evidence arrives; duplicate notification suppression must not leave the order incorrectly pending.

Empty orders, non-positive totals and incompatible configured currency are rejected before saving a new provider intent. This avoids creating an unresolved attempt for a request that cannot be sent legitimately.

If SumUp accepts creation but its response or the local save fails, the next request looks up the saved reference. Exactly one matching checkout is required, with the expected merchant, amount, currency, reference and checkout ID. Zero or multiple results require reconciliation; the API does **not** issue another create request. This also means an attempt saved just before a crash, where no provider request actually reached SumUp, can need operator assistance. Do not delete that row simply because the provider temporarily returns no results.

SumUp documents [reference-based checkout lookup](https://developer.sumup.com/api/checkouts/list). This implementation does not assume that repeating checkout creation is idempotent. The provider reference identifies an attempt; the browser redirect reference continues to identify the order for compatibility with payment-status polling.

## Notifications

The public webhook body is limited to 16 KiB. Malformed JSON, missing event fields and invalid checkout IDs return 400; oversized bodies return 413. Unrecognised event types are acknowledged without contacting SumUp. For checkout-status events the API fetches the checkout using its own credentials, then validates its identity and monetary fields. A notification alone cannot mark an order paid.

Verified references can bind an outstanding attempt even if the original create response was lost. Events are deduplicated by provider, checkout and verified status. A losing duplicate insert is detached before reloading the existing event. Processing failures discard dirty EF state before saving diagnostics, preventing a failed order update or processed flag from being accidentally saved by the error handler.

Order updates are idempotent and can be replayed if persisting the processed event fails after saving the order. A delayed failure does not downgrade paid/refund states or replace the successful transaction ID. A failure for a superseded checkout does not fail its replacement. Payment after cancellation leaves fulfillment cancelled and records `RefundPending`; it does not restore stock a second time. A different successful transaction for an already-paid order is flagged for reconciliation without replacing the original transaction.

## Browser recovery

Before submitting an order, checkout saves its submission key in the URL. It stores no personal form fields in browser storage. If the response is lost, `GET /api/orders/checkout-recovery/{idempotencyKey}` can retrieve the committed order using the owning guest cookie or account. An unknown key and an inaccessible order both return 404, and responses are not cached. Knowing a key or order ID does not grant access.

After receiving an order ID, checkout retains that ID in the URL before starting payment. Recovery checks server state before offering another payment attempt. Paid orders return to the receipt; cancelled/refund states cannot be paid again. If the result or owning session is unavailable, the page offers another status check and contact assistance, without silently creating another order. A missing guest cookie cannot be recovered from the URL alone.

## Operator investigation

Use the order number and request trace ID to locate the order, then inspect its attempts and provider events. These read-only queries find candidates for investigation; an outstanding attempt can still be in flight, so age and provider state matter:

```sql
SELECT a.Id, a.OrderId, a.Reference, a.CheckoutId, a.Status, a.CreatedAtUtc
FROM PaymentAttempts a
WHERE a.CheckoutId IS NULL OR a.Status IN ('CREATING', 'PENDING', 'FAILED')
ORDER BY a.CreatedAtUtc;

SELECT CheckoutId, Status, LastProcessingAttemptAtUtc, ProcessingFailedAtUtc, ProcessingError
FROM PaymentProviderEvents
WHERE Provider = 'SumUp' AND ProcessedAtUtc IS NULL
ORDER BY ReceivedAtUtc;
```

Compare the saved reference, merchant, amount, currency and successful transaction with SumUp. Once an unambiguous checkout is available, customer retry or a repeated notification can recover it. If the result remains uncertain, resolve it through the provider and the responsible operator before allowing another payment. Check for a second charge and arrange any necessary refund through the agreed business process. Refund-state labels do not execute provider refunds.

Include older pending attempts in this review even when they already have a checkout ID: provider lookup or transaction validation can fail before a status-specific event is recorded. A refunded provider transaction is not enough to infer whether the local refund should be full or partial; retain the existing state and reconcile it through the documented refund process.

There is no scheduled payment reconciliation worker or administrative attempt-reset endpoint in this release. Failed events remain inspectable and replayable, but automatic provider redelivery is not a substitute for monitoring unresolved payments. Never mark an order paid based on the browser return URL or a customer's screenshot.

## Deployment and verification

Apply `20261001130531_AddPaymentAttempts` before starting the new API. It adds one table, indexes and an order foreign key; it does not rewrite existing orders. Existing checkout IDs are adopted when next used. Deploy the matching API/frontend and verify `sys.sp_getapplock` access on the hosted SQL login. Keep the table and its history during rollback; an older API does not understand historical attempts or the new recovery endpoint. Pause online checkout before reverting to an older release and reconcile outstanding attempts first.

`PaymentRecoveryTests` runs against isolated SQL Server databases with a simulated provider, covering parallel startup, lost responses, binding failures, duplicate callbacks, failed-save replay, ownership, delayed failures, cancellation and second-payment detection. Browser tests use mocked API responses to exercise recovery and refresh. These tests do not establish live SumUp acceptance.

Before accepting a hosted release, use approved test-payment arrangements to verify redirect URLs, webhook reachability, actual reference lookup and hosted-URL reuse, successful payment, a failed attempt, refresh/retry, and a callback arriving after cancellation. Record the deployed commit and results in the private UAT record. Do not perform real charges merely to exercise failure scenarios.
