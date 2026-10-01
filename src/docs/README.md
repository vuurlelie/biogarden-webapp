# Project documentation

Biogarden is in **User Acceptance Testing (UAT)**. Client feedback and outstanding business decisions are pending. Repository tests and historical audit results do not establish acceptance of a deployed release.

## Choose a guide

| Purpose | Document | Maintained here |
| --- | --- | --- |
| Understand the business and delivered scope | [Project overview](../../README.md) | Portfolio introduction and UAT preview link |
| Run the application locally | [Developer guide](developer-guide.md) | Prerequisites, setup, local migrations, architecture |
| Verify a code change | [Engineering verification](verification.md) | Build, test, format, dependency, and SEO commands; test limitations |
| Understand input validation | [Request validation](validation.md) | Validation responsibilities, conditional rules and error contract |
| Understand response statuses | [HTTP response statuses](http-statuses.md) | Creation, retries, deferred actions and error semantics |
| Review the application with the client | [UAT checklist and decision log](uat-checklist.md) | Scenarios, evidence template, decisions, acceptance criteria |
| Configure an environment | [Configuration](operations/configuration.md) | Setting names, secrets, frontend origins, payment setup |
| Deploy or roll back | [Deployment runbook](operations/deployment-runbook.md) | Release sequence, migration script, smoke checks, release record |
| Back up or recover data | [Backup and recovery](operations/backup-recovery.md) | Scope, proposed targets, restore drill, ownership |
| Diagnose failures | [Monitoring](operations/monitoring.md) | Health endpoints, request tracing, alerts |
| Operate queued order notifications | [Order email delivery](operations/order-email-delivery.md) | Retry semantics, queue monitoring, worker availability |
| Operate private checkout drafts | [Checkout drafts](operations/checkout-drafts.md) | Ownership, expiry, cleanup, conflicts and order recovery |
| Configure checkout pricing and GLS | [Checkout pricing and GLS](operations/checkout-pricing.md) | Provisional fees, COD policy, destination selection, compatibility and verification |
| Tune request limits | [Abuse protection](operations/abuse-protection.md) | Rate limits and verification |
| Manage conflicting admin edits and payment corrections | [Admin order updates](operations/admin-order-updates.md) | Versions, reservation rules, payment transitions and audit records |
| Obtain professional wording/process review | [Legal and accounting handoff](legal/legal-accounting-review-checklist.md) | Review scope and approval evidence |
| Work on the browser application | [Frontend notes](../frontend/biogarden-client/README.md) | Frontend structure and build behaviour |

## Current guidance and historical evidence

The [July 2026 quality report](code-quality-readiness.md) is a historical milestone record. Its test counts and dependency results apply only to that recorded run. Use the current PR's CI results and a UAT record identifying the deployed commit for new decisions.

The tracked documentation is intended for public repository readers. Keep completed client feedback, personal data, credentials, and private approval evidence in the agreed private tracker; the UAT document provides blank templates. Local audit/work notes that are not tracked are not part of this documentation set.

## Keeping documents consistent

Update the owning document in the table when a procedure changes and link to it elsewhere. Keep the overview concise, retain dates on historical reports, and do not copy test results into several current guides. Configuration describes available settings; the private release/UAT record describes which settings and integrations were actually verified for a particular deployment.
