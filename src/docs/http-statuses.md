# HTTP response statuses

Responses distinguish resource creation, successful commands and deferred work. The convention follows [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html#section-15.3).

| Operation | Successful response |
| --- | --- |
| Create an order | `201 Created`, the order DTO and its `Location` |
| Retry checkout when the order already exists | `200 OK`, the existing order DTO |
| Register an account | `201 Created`, no response body |
| Create an address or product | `201 Created`, existing response body and resource `Location` |
| Create a post or withdrawal request | `201 Created`, existing ID or request DTO |
| Upload a product/post image | `201 Created`, image `Location` and the existing `{ path }` body |
| Account lifecycle request awaiting later processing | `202 Accepted`, result describing pending status |
| Completed account lifecycle action | `200 OK`, result describing completion |
| Confirm/reset credentials, resend confirmation, request password recovery, send a contact message | `204 No Content` |
| Update/delete without a response representation | `204 No Content` |
| Read data or return updated cart/order/admin state | `200 OK`, response DTO |
| Create or reuse a SumUp checkout | `200 OK`, checkout DTO |
| Acknowledge a processed SumUp callback | `200 OK`, unchanged provider-facing acknowledgement |

Creation metadata is returned by the order service as `OrderCreationResult`; it is not added to the public JSON body. Existing-order branches, including persistence-race recovery, report `Created: false`. Do not infer creation from timestamps or perform a separate existence check in the controller.

An order's `Location` identifies its existing authenticated detail route. It does not grant access: guest checkout remains session-owned and guests cannot use the account-only GET endpoint. No new public order-reading endpoint is introduced. Posts can be drafts, and withdrawal requests have no dedicated public read route, so those responses do not advertise a fabricated detail URL.

Queued order notifications do not turn checkout into a `202` operation: the order, stock changes and email queue records have already been persisted when checkout succeeds. A withdrawal request is likewise created immediately even though a person handles it later. In contrast, the account lifecycle command returns `202` while the requested account deletion remains pending.

## Error responses

- `400`: invalid input or malformed request, including request validator failures.
- `401`: missing/invalid authentication, including a cart merge without a usable user identity.
- `403`: an authenticated caller lacks the required role.
- `404`: resource not found, including intentionally concealed ownership mismatches.
- `409`: a conflict with current state, including unavailable cart stock and concurrency conflicts.
- `429`: rate limit exceeded; retain the retry guidance.
- `500`: unexpected server failure, with safe client-facing details.
- `503`: temporary database deadlock (SQL Server 1205), lock timeout (1222), or command timeout (-2), including wrapped provider errors. Details are localized and do not expose SQL or connection information. Existing delivery-directory and payment-recovery 503 responses keep their specific meaning.

See [Request validation](validation.md) for the error body contract. The frontend checks `response.ok`, accepts all successful 2xx responses, and only parses bodies for operations that return data. External clients that previously required exactly 200 for creation or empty commands must adopt the updated statuses.

Database errors do not trigger generic automatic retries. A command/commit timeout can have
an uncertain outcome; clients must use the existing checkout idempotency/recovery and payment
reconciliation flows before repeating writes. Contact acceptance retains its bounded deadlock
retry, and returns 503 when exhausted. Optimistic concurrency conflicts remain 409. Detailed
exceptions remain in server logs with the response trace ID.

Cart and public withdrawal actions use the central exception handler. Wrapped database
failures must not be converted by controller catches into business conflicts or expose
provider messages. Unknown database failures return a generic 500; known transient SQL
errors return 503 and genuine optimistic concurrency conflicts return 409.
