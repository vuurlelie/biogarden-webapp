# UAT checklist and client decision log

**Status: awaiting execution and client decisions.** This is a reusable acceptance template, not a record of completed testing. Run it against the [UAT preview](https://biogarden.raczkrisztina.dev/) and record the actual deployed frontend/API versions. A merged change is not necessarily deployed.

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

The developer should confirm the environment using the [deployment smoke checks](operations/deployment-runbook.md#post-deployment-smoke-checks). Before checkout testing, confirm `AddOrderEmailOutbox` is applied and the delivery mode is understood. Do not treat preview files as proof of SMTP delivery. Agree on test orders, payment handling, and cleanup with the client before execution.

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

## Developer-assisted checks

Run these in an isolated test environment using test accounts/data. They support client acceptance but are not tasks the client needs to perform unaided.

| ID | Exercise | Required evidence |
| --- | --- | --- |
| TECH-01 | Attempt payment-status updates as an ordinary customer and access another user's/session's order. | Removed confirmation endpoint is unavailable; admin operations and unrelated orders are inaccessible. |
| TECH-02 | Submit two competing checkouts for limited stock and retry a submission. | No overselling or duplicate order; the rejected request has a recoverable message. Use the actual API and SQL Server. |
| TECH-03 | Make SMTP unavailable, place an order, restore SMTP, and restart the API with a queued email. | Order creation succeeds; both messages remain durable and recover as described in [order email delivery](operations/order-email-delivery.md#uat-verification). Record any duplicate delivery. |
| TECH-04 | Check configured request limits and recovery after the retry interval. | Expected 429 responses and usable retry behaviour; follow [abuse protection](operations/abuse-protection.md#manual-testing). |
| TECH-05 | Rehearse restore into a separate environment. | Record the [restore drill](operations/backup-recovery.md#restore-drill), restored data checks, and actual recovery time. |

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

## Acceptance of a review round

Record the accepted commit(s), date, client decision owner, passing scenarios, remaining issues, and explicitly accepted exclusions. All applicable core workflows must pass; blockers must be resolved and retested. Explain each deferred issue and its business impact rather than marking it passed.

**UAT acceptance and production release are separate decisions.** Production still needs the agreed environment settings, professional review outcomes, and operational release checks. Record those through the owning documents in the [documentation index](README.md), rather than copying their checklists into this record.
