# UAT checklist and client decision log

**Status: awaiting execution and client decisions.** This is a reusable acceptance template, not a record of completed testing. Run it against the [UAT preview](https://raczkrisztina.dev/) and record the actual deployed frontend/API versions. A merged change is not necessarily deployed.

## Prepare a review round

Create a private copy of the record below or use the agreed issue tracker. Record evidence by scenario ID; keep the repository copy free of client/customer details and credentials.

| Field | Record for this round |
| --- | --- |
| Round/date and tester | To record |
| Frontend URL and commit | To verify |
| API environment and commit | To verify |
| Database/migration version | To verify |
| Browser/device | To record, including one mobile device |
| Test customer, admin, products and stock | Use designated test records; share access privately |
| Payment mode | Disabled, provider test, or explicitly agreed real low-value test |
| Email mode | Preview files or SMTP to designated test recipients |
| Feedback tracker and decision owner | To agree |

The developer should confirm the environment using the [deployment smoke checks](operations/deployment-runbook.md#post-deployment-smoke-checks). Before checkout testing, compare the target database's complete migration history with the selected release, including the order and notification outboxes, drafts, pricing/GLS snapshots and payment attempts. Follow the owning deployment guides rather than assuming one migration proves readiness. Do not treat preview files as proof of SMTP delivery. Agree on test orders, payment handling, and cleanup with the client before execution.

### Enabled scope for this round

Record the effective API configuration and client-approved scope privately before executing checkout scenarios. The [pricing guide](operations/checkout-pricing.md#provisional-prices-and-scope) owns provisional rates and supported combinations; those defaults are not evidence that the client approved every method or that the hosted API uses them.

| Fulfillment | Payment combinations to confirm | Round decision and evidence |
| --- | --- | --- |
| Personal pickup | Payment at pickup; online card if enabled | To record separately for each combination |
| Home delivery | Cash on delivery; online card if enabled | To record separately for each combination |
| GLS locker | Online card; COD only for a point supporting collection | To record separately for each combination |
| GLS CsomagPont | Online card; COD only for a point supporting collection | To record separately for each combination |

For each combination record enabled/disabled, approved fees, payment mode, scenario result and decision owner. Compare `GET /api/orders/checkout-methods` and the checkout UI with this record. A disabled choice must also be rejected by a direct quote/order request; hiding it in the UI is insufficient. When SumUp is disabled, remove online card from the effective payment choices as described in the pricing guide. Offline methods must not initiate an online payment.

Record supported desktop/mobile browsers and privacy settings, the designated SMTP recipients, and any explicitly excluded workflows. Disabled online payments may be **Not applicable** to an agreed offline-only round; an enabled but broken payment method is **Fail** or **Blocked**, not an exclusion created after the test. Limited content review and full checkout acceptance are separate scopes.

## Client review scenarios

For each row record **Not run**, **Pass**, **Fail**, **Blocked**, or **Not applicable** in the private results log. Blocked and Not applicable need a reason; neither counts as a pass.

| ID | Action | Expected result |
| --- | --- | --- |
| UAT-01 | Review home, about, contact details, and article pages on desktop and mobile. | Navigation, images, text, and business identity are correct and approved by the client. |
| UAT-02 | Browse/search products and open details. | Names, units, prices, availability, and pickup/delivery descriptions match the intended catalogue. |
| UAT-03 | Add products; change quantities; remove items and empty the cart. | Quantities and totals update correctly; empty/error states provide a clear next step. |
| UAT-04 | Complete a private guest order with the agreed pickup/payment option. | Review shows the agreed total and details; one order is created and appears in administration. |
| UAT-05 | While signed out, place an order using a designated existing account's email; retry the same submission and start payment if enabled. | The order remains guest-owned, the original session retains access, and retry creates no duplicate order. Email matching alone does not add it to the account's history. |
| UAT-06 | Place a company guest order with billing details and printed-invoice preference. | Required fields are validated and the saved billing details/preference match the review. Document wording is subject to the separate professional review. |
| UAT-07 | Register, confirm email, sign in, and recover a password with test accounts. | Confirmation/reset links work in the configured mode; invalid/expired links have a recovery path. |
| UAT-08 | Update a signed-in customer's details/addresses, place an order, and view history. | The order belongs to that account and preserves the submitted contact/billing details. |
| UAT-09 | Use a product limited to personal pickup; try an unavailable quantity. | Incompatible fulfilment and unavailable stock are rejected clearly before an invalid order is accepted. |
| UAT-10 | Complete, cancel, and retry a configured online payment. | The displayed and admin payment states agree with verified provider results; cancellation never appears as successful payment. |
| UAT-11 | Check customer and business order confirmations. | Each recipient receives the correct order number, total, and details through the agreed delivery mode. |
| UAT-12 | As admin, change catalogue/stock and create, edit, preview, and publish a test article. | Saved changes appear as intended; unpublished content is not presented as published content. |
| UAT-13 | As admin, review an order and use agreed status/manual-payment operations. | Updates are persisted, invalid transitions are rejected, and customer-facing status matches. |
| UAT-14 | Submit a contact message and an order-related request as guest and signed-in customer. | Confirmation is clear; the business can find the request and the configured notifications are correct. |
| UAT-15 | Navigate forms by keyboard, submit incomplete fields, and revisit consent choices. | Focus and validation are usable; consent choices can be changed; checkout remains usable on mobile. |
| UAT-16 | Fill checkout details, wait for autosave, refresh, discard, and sign out/in. Edit the same draft in two tabs. | Drafts restore only for their current cart/session; discard removes them; conflicts preserve local input with recovery choices. Verify expiry and cleanup using the [draft guide](operations/checkout-drafts.md). |
| UAT-17 | Exercise each enabled delivery/payment combination using the approved [pricing and GLS setup](operations/checkout-pricing.md), including both locker and CsomagPont selection on desktop/mobile. | Review, saved order, customer/admin views and email agree on fees and destination; an agreed online-payment test charges the saved total. Record payment mode and tested combinations. |

## Developer-assisted checks

Run these in an isolated test environment using test accounts/data. They support client acceptance but are not tasks the client needs to perform unaided.

| ID | Exercise | Required evidence |
| --- | --- | --- |
| TECH-01 | Attempt payment-status updates as an ordinary customer and access another user's/session's order. | Removed confirmation endpoint is unavailable; admin operations and unrelated orders are inaccessible. |
| TECH-02 | Submit two competing checkouts for limited stock and retry a submission. | No overselling or duplicate order; the rejected request has a recoverable message. Use the actual API and SQL Server. |
| TECH-03 | Make SMTP unavailable, place an order, restore SMTP, and restart the API with a queued email. | Order creation succeeds; both messages remain durable and recover as described in [order email delivery](operations/order-email-delivery.md#uat-verification). Record any duplicate delivery. |
| TECH-04 | Check configured request limits and recovery after the retry interval. | Expected 429 responses and usable retry behaviour; follow [abuse protection](operations/abuse-protection.md#manual-testing). |
| TECH-05 | Rehearse restore into a separate environment. | Record the [restore drill](operations/backup-recovery.md#restore-drill), restored data checks, and actual recovery time. |
| TECH-06 | Open checkout review using the keyboard. Test Tab/Shift+Tab, both close buttons, Escape, missing consents, and a delayed/failed order request. Repeat with a screen reader and record its browser/version. | Focus enters the labelled dialog, stays inside while open, and returns to the review button on dismissal. Background content is inert. Consent errors are associated with their checkboxes and the first invalid consent receives focus. During submission, progress is announced and dismissal is disabled; a failed request restores interaction. Confirm the dialog title, errors, and progress are understandable with the screen reader; automated browser checks alone do not establish this. |
| TECH-07 | Exercise changed prices, disabled choices, GLS failure/recovery and a historical order using the [checkout verification guide](operations/checkout-pricing.md#verification-and-release-evidence). | Invalid submissions have no order/stock/email effects; recovery requires a current review; legacy totals remain unchanged. Record real widget and hosted API evidence separately from mocked browser tests. |
| TECH-08 | In separate browsers/tabs, demote an admin, deactivate/reactivate an account, reset its password, and sign out. Try a previously issued session after each security change. | Revoked sessions lose protected access; an active customer receives 403 for admin access; reactivation requires a fresh login; the last active admin is preserved. Follow [session verification](operations/configuration.md#account-authorization-and-revocation). |
| TECH-09 | On the real frontend/API origins, initialize a new guest cart, add two products, refresh, sign in to merge, then sign out. Repeat with an independent guest, two tabs, and supported privacy settings. | Cart quantities persist in the owning session, merge once and never appear in the other guest/account. Record cookie storage/blocking and the supported browser policy; localhost tests do not prove this. |
| TECH-10 | Open two admin editors, change stock or order state in one, then save the stale second form. Test an agreed manual payment/refund correction. | Stale saves return 409 without overwriting newer values; reservations are preserved; corrections require an authorized actor and reason. RefundPending does not claim a refund was executed. See [admin updates](operations/admin-order-updates.md). |
| TECH-11 | Lose the checkout response after submission, refresh, retry, and exercise the enabled payment recovery paths in an isolated provider/test setup. | Recover the original owned order; stock and notifications are not duplicated. Repeated or late verified payment events reconcile according to [payment recovery](operations/payment-recovery.md), including cancelled-order handling. Record real-provider results separately from simulated callbacks. |
| TECH-12 | Submit contact and account requests with a queued notification, interrupt/restart the API, then recover designated SMTP delivery. | Accepted changes persist; pending messages retry; obsolete account links are rejected; both contact recipients are checked. Record observed duplicates because SMTP delivery is not exactly-once. See [notification recovery](operations/notification-recovery.md). |
| TECH-13 | In the isolated test environment, verify expired draft/cart cleanup and preservation of active, checked-out and order-linked carts. Observe a hosted worker cycle using designated data. | Deletions follow configured bounds/retention, and order/payment/stock state is unchanged. Follow [cart maintenance](operations/cart-maintenance.md) and [draft retention](operations/checkout-drafts.md#retention-and-cleanup); readiness alone does not prove worker execution. |
| TECH-14 | Preview a draft/deleted article as an anonymous visitor, publish safe rich text, and attempt invalid uploads using synthetic files. | Draft/deleted content is unavailable publicly; unsafe markup and invalid images are rejected or sanitized as specified. Verify the deployed image decoder, upload persistence and browser headers using [content validation](operations/content-validation.md). |

## Regression traceability

The following references identify the concerns addressed during UAT preparation. They are topic references, not open defect assertions or proof of hosted acceptance. Attach a private issue/evidence reference and the actual tested commit to each applicable row. Automated coverage is described in [engineering verification](verification.md), including its remaining gaps.

| Reference and concern | Automated regression examples | Acceptance evidence |
| --- | --- | --- |
| F01 — session revocation and account ownership | `SessionRevocationTests`, `IdentityOwnershipTests`, `session-lifecycle.spec.ts` | UAT-07/08; TECH-01/08 |
| F02 — guest/user reservation transfer | `CartSessionTests`, `CartReservationQueryTests` | UAT-03/08/09; TECH-02/09 |
| F03 — guest initialization and request ordering | `cart-session.spec.ts`, `RealBrowserJourneyTests` | UAT-03/04; TECH-09 on deployed origins |
| F04 — authoritative checkout totals and GLS destination | `CheckoutPricingTests`, `GlsDeliveryPointTests`, `checkout-pricing.spec.ts`, `gls-picker.spec.ts` | UAT-06/17; TECH-07; saved/email/provider totals and real widget |
| F05 — stale admin writes and payment corrections | `AdminConcurrencyTests`, `AdminStockTests`, `admin-concurrency.spec.ts` | UAT-12/13; TECH-10 |
| F06 — interrupted checkout and payment recovery | `PaymentRecoveryTests`, `OrderWorkflowTests`, `RealBrowserJourneyTests` | UAT-05/10; TECH-01/11; enabled-provider evidence |
| F07 — rich text and managed uploads | `ArticleContentTests`, `ManagedImageTests`, `content-validation.spec.ts` | UAT-01/12; TECH-14 |
| F08 — durable contact/account notifications | `NotificationRecoveryTests`, `AccountNotificationRecoveryTests`, `contact-recovery.spec.ts` | UAT-07/11/14; TECH-03/12 |
| F09 — expiry, cleanup and pending-order policy | `CartCleanupTests`, `CheckoutDraftTests`, `ConcurrentCheckoutTests` | UAT-09/16; TECH-02/13; unpaid-order decision recorded explicitly |
| F10 — hosted operation and release evidence | CI test/coverage gates and loopback browser journeys provide supporting evidence only | TECH-05/06/09; hosted smoke, TLS/origins, restart, SMTP, restore and rollback records |

Passing one automated example does not close the whole reference. In particular, the [coverage follow-ups](verification.md#critical-backend-coverage) include Identity persistence failures, payment-lock failure cleanup, cart removal/clearing and withdrawal handling. Account erasure and saved-address cleanup have SQL workflow coverage in `AccountErasureTests`. Record each applicable unresolved concern with its impact, owner and disposition before accepting the round.

## Results and issue log template

| Scenario | Result | Actual behaviour / reproduction steps | Evidence or issue reference | Owner | Retest date/result |
| --- | --- | --- | --- | --- | --- |
| UAT-01 | Not run | To record | To record privately | To assign | Pending |

For failures include expected versus actual behaviour, device/browser, time, and a test order/reference or request trace ID where useful. Distinguish an implementation defect from a requested change or an unresolved business decision. Assign severity by impact: **Blocker** prevents safe/core use, **Major** materially breaks an agreed workflow, **Minor** has a usable workaround or is cosmetic.

## Client decision log template

These are topics to confirm, not assertions that the client has rejected or left each one unresolved. Replace Pending confirmation with the actual decision and evidence in the private record.

| ID | Decision to confirm | Suggested decision owner | State | Decision/date/evidence |
| --- | --- | --- | --- | --- |
| DEC-01 | Final catalogue, prices, units, photos, and public text | Client | Pending confirmation | To record |
| DEC-02 | Offered pickup/delivery methods and customer instructions | Client | Pending confirmation | To record |
| DEC-03 | Payment methods enabled at launch and payment-test arrangements | Client + developer | Pending confirmation | To record |
| DEC-04 | Customer/business email recipients, branding, and delivery mode | Client + developer | Pending confirmation | To record |
| DEC-05 | Final legal/accounting wording and business processes | Client + professional reviewers | Pending confirmation | Link to [review outcome](legal/legal-accounting-review-checklist.md#review-outcome-to-record) |
| DEC-06 | Production domains, hosting, monitoring, backup ownership and recovery targets | Client + operator | Pending confirmation | Reference configuration and operational runbooks |
| DEC-07 | Accepted minor issues, deferred enhancements, and launch acceptance | Client | Pending confirmation | To record |
| DEC-08 | Pending/unpaid order follow-up, cancellation timing, stock restoration and refund ownership | Client + operator | Pending confirmation | Automatic expired-cart cleanup is not an unpaid-order cancellation policy |
| DEC-09 | Supported browsers/mobile devices and cross-site cookie/privacy limitations | Client + developer | Pending confirmation | Record the tested origin arrangement and explicit exclusions |

## Acceptance of a review round

Record the accepted commit(s), date, client decision owner, passing scenarios, remaining issues, and explicitly accepted exclusions. All applicable core workflows must pass; blockers must be resolved and retested. Explain each deferred issue and its business impact rather than marking it passed.

The round's evidence must identify the same release across CI, application packages and hosted testing. A homepage 404, unknown deployed version, missing migration evidence, or an unverified required integration blocks the affected scope. Do not carry forward a previous round's Pass after changing code, configuration, schema or hosting without recording the relevant retest. Store this record privately using the [release record template](operations/deployment-runbook.md#release-record-template).

**UAT acceptance and production release are separate decisions.** Production still needs the agreed environment settings, professional review outcomes, and operational release checks. Record those through the owning documents in the [documentation index](README.md), rather than copying their checklists into this record.
