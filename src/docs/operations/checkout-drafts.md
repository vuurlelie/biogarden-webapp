# Checkout drafts

Checkout saves incomplete contact, billing and delivery fields in the API database. The frontend keeps the editable form in memory and sends changes after 800 ms without typing. It no longer writes personal checkout fields to `localStorage` or `sessionStorage`; startup removes drafts saved by older frontend versions. The fulfillment method and selected GLS point ID are saved; restoring a GLS selection resolves it again through the API and can return a delivery-point error. Payment selection, checkout step and legal acceptance are not restored. Saved account addresses remain separate from an unfinished order.

## Ownership and conflicts

A draft belongs to an active cart. Guest requests require that cart's HTTP-only session cookie; knowing its ID is insufficient. Signed-in browser requests require cart ownership and bind the draft to the cookie session's random, non-secret session ID. A fresh login cannot restore an earlier login's draft. Explicit bearer API consumers retain token-hash binding; the browser no longer stores a JWT.

Login merges cart items but discards guest personal checkout details. It preserves an existing draft for the destination user's current login. Guest personal data is never automatically adopted into an account. Explicit browser logout clears the current session's draft and revokes the server ticket before reporting success. Account switching clears the in-memory form; a new session cannot restore the earlier session's draft. Expiry/cleanup handle abandoned or expired sessions. Cookie session details belong in [configuration](configuration.md#browser-authentication-sessions).

GET and PUT return a draft version, expiry and fields. PUT and DELETE require the version last read by the browser (null for a new draft). A stale version returns 409. Autosaves are serialized within a page; after an error the form preserves local edits and offers retry or an explicitly labelled reload that discards those edits. Competing tabs sharing a login cannot silently overwrite a newer version. A new login starts with no restored draft; saving replaces the earlier login's draft on that same cart.

`/api/checkout-drafts` requires `X-Checkout-Request: 1`, including guest requests, and uses the explicit-origin credentialed CORS policy. The custom header requires a browser preflight for cross-origin scripts and prevents ordinary cross-site form submissions. Do not configure wildcard/reflected credentialed origins. Responses use `Cache-Control: no-store`. Requests are bounded to 16 KiB and reuse the cart mutation rate limit. Draft validators accept incomplete strings while enforcing types, permitted fields and length limits. Unknown fields are rejected, and private-customer drafts discard company/billing-address fields.

## Retention and cleanup

Each successful save sets expiry to the earlier of 30 minutes from that save or the cart's expiry. Saving does not renew stock reservations. Reads reject expired/checked-out carts and do not return expired draft contents, even if background cleanup has not run. Order creation clears the draft in the same transaction as order persistence; a rolled-back order retains its draft. Explicit discard removes the stored fields.

`CheckoutDraftCleanupWorker` clears expired or retired-cart draft fields in batches of up to 200, once per minute while the API is running. It logs counts or error types without draft contents. `CheckoutDrafts:CleanupEnabled` defaults to `true`; disabling it is intended for isolated tests, not normal hosting.

On IIS/SmarterASP, configure and verify application availability if timely physical cleanup is required. Sleeping/stopped applications delay deletion; expiry still applies when the application resumes. Check cleanup logs and the oldest remaining expired draft during UAT. Database backups and transaction logs can retain older copies according to their own retention policy. Confirm database/backup encryption and access controls with the host; application-level payload encryption is not implemented. Never enable sensitive-data logging or copy real draft payloads into test evidence.

## Order and payment recovery

Drafts are not authoritative orders. Checkout still validates inputs, cart state and legal acceptance when placing an order. The cart ID supplies a stable idempotency key without browser storage. The server's ownership checks and existing order/cart uniqueness protections remain authoritative.

The frontend now retains the submission key before placing an order and the order ID before starting online payment, so lost responses and refreshes can recover the existing order. Ownership is still required. A cart-refresh failure after a pickup order cannot turn successful order creation into a failed checkout. See [payment recovery](payment-recovery.md) for browser behaviour, provider attempts and reconciliation; those rules are separate from draft persistence.

## Deployment and verification

Apply migration `20260929112302_AddCheckoutDrafts` before deploying this API, following the [deployment runbook](deployment-runbook.md). It adds nullable draft fields and an expiry index to `Carts`; existing cart data is preserved. Deploy the matching frontend. A rollback to the old frontend reintroduces its browser-storage behaviour, so treat frontend/API rollback as one reviewed release decision.

`CheckoutDraftTests` exercises real HTTP handling, Identity/JWT, migration history and SQL Server for ownership, session isolation, CSRF-header enforcement, malformed input, competing versions, expiry, cleanup, login merge and order transaction rollback. Browser tests use mocked API responses to cover restoration, discard, conflict/error recovery, legacy storage removal and existing-order payment retry. See [engineering verification](../verification.md) for commands.

On the hosted release, verify restoration after refresh, discard, logout and fresh login, a stale edit from a second tab, expiry and cleanup, guest cookie delivery, and online-payment failure recovery using approved test payment arrangements. Record results privately; repository tests do not establish hosted acceptance.
