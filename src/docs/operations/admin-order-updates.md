# Admin stock, order and payment updates

Admin product, inventory and order reads include a `version` derived from SQL Server rowversion. Updates must send the version that the admin reviewed. Missing versions return 400; stale versions and overlapping database writes return 409. Deploy the matching frontend and API together. Old forms must reload before saving.

The version check detects changes since the form was opened; EF's concurrency check also detects writes between the service read and save. Checkout stock changes, cart reservation guards and provider payment updates advance the corresponding rowversion. Product and inventory edits share the same active-reservation lower bound. Confirming an older order whose stock was not committed also respects other carts' active reservations.

On a conflict, editors retain local input until the administrator loads current data. The refreshed form uses current values and retains the earlier text/choices for comparison; the administrator must deliberately reapply changes. Product image files may need to be selected again. There is no automatic retry that overwrites newer stock or payment information.

## Manual payment policy

| Current payment state | Permitted manual next state |
| --- | --- |
| Pending, Authorized or Failed | Paid, only for offline/legacy orders that are not cancelled |
| Paid | RefundPending |
| RefundPending | PartiallyRefunded or Refunded |
| PartiallyRefunded | RefundPending or Refunded |
| Refunded | No further change |

Keeping the existing state is allowed. Online-card success is provider-controlled. A paid/refunding/refunded order cannot be reset to unpaid. Cancelling a paid or partially refunded order changes it to RefundPending and requires a correction reason. Completed or cancelled fulfillment remains terminal, but payment refund records can still be updated. Partial refunds currently record a state and reason, not a monetary refund ledger.

Refund status changes record an action performed separately; they do not call the provider to transfer money. Check the actual transaction before marking a refund complete. Late provider success/failure notifications preserve recorded refund states, including partial refunds.

Every admin payment-state change requires a 1–500 character `paymentCorrectionReason` and the authenticated admin's identifier. The API obtains the actor from the authenticated principal, not from request JSON. `OrderPaymentCorrections` records the order ID, actor ID, UTC timestamp, old/new payment states and reason. The audit insert, order change, stock adjustment and any status email are saved atomically. Unchanged-state retries do not add another correction. Concurrent cancellation can restore committed stock only once.

Record an operational explanation or reconciliation reference, not card details, credentials or unnecessary personal information. Audit records retain the actor identifier after account removal. There is currently no audit-history screen or edit/delete API; authorized operators can inspect the table using restricted database access. For example, with a parameterized `@OrderId`:

```sql
SELECT CreatedAtUtc, ActorId, PreviousStatus, NewStatus, Reason
FROM OrderPaymentCorrections
WHERE OrderId = @OrderId
ORDER BY CreatedAtUtc, Id;
```

## Deployment and verification

Apply `20261001075525_AddOrderPaymentCorrections` before starting the new API, following the [deployment and backup procedure](deployment-runbook.md). It adds an audit table and index; existing orders are not rewritten. Do not fabricate audit records for earlier changes. Downgrading the migration drops the audit history; retain the additive schema when reviewing a rollback.

SQL tests cover both stock edit paths, stale forms after checkout/payment, audit rollback, and synchronized competing cancellations. Browser tests use mocked APIs for conflict recovery in product, inventory and order editors. On hosted UAT, use two admin sessions and a designated test order to verify 409 recovery, unchanged sold stock, offline payment reasons, terminal-order refunds and the saved actor ID. Keep actual results in the private release record; local tests do not establish hosted acceptance.
