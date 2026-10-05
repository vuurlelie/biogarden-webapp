# Backup And Recovery Runbook

This runbook defines the minimum production backup and restore process for Biogarden data.

The production system has two important data stores:

- SQL Server database,
- uploaded files under API static upload storage.

## Recovery Targets

Set these targets before launch with the site owner and hosting provider:

| Target | Initial recommendation |
| --- | --- |
| RPO | 24 hours or less for normal backups; immediate backup before migrations. |
| RTO | Same business day for non-critical incidents; faster for checkout-blocking incidents. |
| Retention | Daily backups for 14 days, weekly backups for 8 weeks, monthly backups for 12 months. |

These are proposed targets, not verified hosting capabilities or client commitments. Record the agreed values and owner in the private UAT/release record after confirming the provider's capabilities.

## Backup Scope

Database backup must include:

- users and roles,
- products and inventory,
- carts and orders,
- payment provider event records,
- durable payment attempts and manual payment-correction audit records,
- queued order emails and their delivery state,
- queued contact/account notifications, leases and expiry state,
- private checkout drafts (handle their personal data with the backup's restricted retention policy),
- account lifecycle requests,
- all EF Core migration history.

File backup must include:

- product images,
- post images,
- email assets if managed outside the repository,
- any future uploaded documents.

Do not rely only on source control. Source control does not contain customer data, orders, or uploaded production files.

## Backup Schedule

Minimum production schedule:

- automated database backup every night,
- automated uploaded-file backup every night,
- manual database backup before every migration,
- manual uploaded-file backup before any upload-storage migration,
- restore rehearsal at least once before launch and after major hosting changes.

Backups should be encrypted at rest and stored outside the production server or database instance.

## Database Backup Checklist

Before a planned deployment:

1. Identify the production database connection.
2. Create a full database backup.
3. Store it in the approved backup location.
4. Confirm backup size and timestamp.
5. Record the backup path in the release record.
6. Restore it into a non-production database when rehearsing or when the migration is high risk.

Verify and record the backup/export controls available for the target SmarterASP.NET plan before launch.

## Uploaded Files Backup Checklist

Before a planned storage change:

1. Identify the production upload storage location.
2. Copy all files to the approved backup location.
3. Preserve directory structure and file names.
4. Confirm file count and total size.
5. Record backup path and timestamp in the release record.

If uploads move to cloud object storage later, enable bucket/container versioning if available.

## Restore Drill

Run this rehearsal before first production launch:

1. Create or choose a non-production restore environment.
2. Restore the latest database backup into that environment.
3. Restore uploaded files into the matching non-production file storage.
4. Point the API to the restored database and file storage.
5. Before the first API start, pause both email workers with `OrderEmailOutbox__Enabled=false` and `NotificationEmailOutbox__Enabled=false`, disable real SMTP/payment/error-alert delivery, and pause draft/cart cleanup while checking the restored baseline. A restored queue can include messages already sent after the backup point. Keep the restore environment private and callbacks pointed at the intended environment. Resume each worker only as part of its deliberate test; use designated test recipients/preview mode and reconcile pending messages before enabling delivery.
6. Check `/health/live` and `/health/ready`.
7. Open the frontend against the restored API.
8. Verify products, posts, account login, orders, and admin order details.
9. Reconcile representative order totals, stock, payment attempts/events/corrections, notification queue state and uploaded-image references with the backup point. Account sessions and tokens restored from a backup may reflect an earlier security state; decide how sessions will be revoked before reopening a recovered environment. Do not send restored reset links or treat database payment state as proof that the provider has reversed or forgotten a charge.
10. Record restore duration and any manual fixes, then record whether the measured data loss/recovery time meet the agreed RPO/RTO. A failed or unexecuted rehearsal remains a failed or unverified release gate.

The restore drill is successful only when the restored system can serve real application pages, not merely when a backup file exists.

Keep the drill record private: source backup identifier/time, target isolated environment, database/file restoration start/end times, selected application packages, migration state, safety settings before first start, actual scenario outcomes, discrepancies, reconciliation, operator and evidence references. Link its identifier from the [release record](deployment-runbook.md#release-record-template). A locally migrated empty database is not a restore drill from a hosted backup.

## Incident Recovery

For data loss, bad migration, or accidental deletion:

1. Stop writes if continued use could make the damage worse.
2. Record the incident time and suspected cause.
3. Identify the most recent clean database and file backups.
4. Decide whether to restore everything or repair only affected records/files.
5. Restore into non-production first when time allows.
6. Verify the restored data.
7. Apply the same restore or repair to production.
8. Run smoke checks from the deployment runbook.
9. Document what happened and how to prevent it next time.

If customer orders may have been created after the backup point, do not restore production blindly. Preserve current data first, then reconcile orders manually.

## Ownership

Before launch, assign named owners for:

- daily backup monitoring,
- failed backup alerts,
- restore approval,
- restore execution,
- customer communication if data is affected.

Until ownership is assigned, deployment to production should be treated as blocked.

## Monthly Review

Once live, review monthly:

- latest successful backup timestamp,
- backup storage access,
- restore drill age,
- backup retention usage/cost,
- whether new data stores were added and need backup coverage.
