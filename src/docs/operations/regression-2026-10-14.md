# Deployed regression — 14 October 2026

Target: https://raczkrisztina.dev and https://api.raczkrisztina.dev. User reported upload complete. Verified frontend entry: `/assets/index-NX0x0iyv.js`.

| Test/action | Expected | Actual | Result |
| --- | --- | --- | --- |
| Fetch homepage and /bejelentkezes HTML | New build and HTML revalidation | Both 200, new entry bundle, Cache-Control no-cache | PASS |
| Request nonexistent /assets/uat-missing-20261014.js | 404 instead of SPA index | 404 with server error document, no app entry | PASS |
| Fetch exact homepage, login, admin login, product detail and dashboard bundles referenced by entry | JavaScript files available | All five returned 200 application/javascript | PASS |
| Inspect deployed entry for recovery UI | Corrected recovery code deployed | Hungarian error heading and reload action present | PASS — deployment check |
| Open Cukkini, reserve last available 1 kg through normal UI | No free stock wording; additional purchase blocked | “Jelenleg nincs szabad készlet”; quantity and add disabled; reservation explanation shown | PASS |
| Remove test item, revisit product | Empty basket and released availability | Empty basket; product again showed 1 kg and enabled purchase | PASS |
| User signs into admin; inspect dashboard and reload | Pending count and list consistent | Count 1; list still says no pending requests after reload | FAIL |
| Open request management with pending filter | Identify pending record behind dashboard count | August 2 pending request visible; it is older than seven days | Confirms dashboard defect still deployed |
| Log out using public header while signed in as admin | Homepage loads and authenticated controls clear | Homepage rendered normally, guest links appeared | PASS |
| Navigate homepage to customer login and submit empty form | Login renders and required email validation appears | Login rendered; email alert appeared and field focused | PASS |
| Reload admin tab after logout | Protected content inaccessible | Redirected to admin login form | PASS |
| Inspect homepage image load state | All static homepage images loaded | All 10 images complete with naturalWidth > 0 | PASS |
| Read browser errors during this run | No new application errors | No error entries returned for public tab | PASS — observed run only |
| GET API /health/ready | API ready | 200 text/plain | PASS |

## Interpretation and limits

Frontend deployment is confirmed. The dashboard still behaves exactly like the old backend's seven-day filtering. Republish the corrected Biogarden.WebApi Release build using the existing profile, then repeat dashboard verification. No migration or environment-variable change is needed. Backend binary version was not independently inspected, so missing API deployment is an inference from behavior.

The forced lazy-import failure/reload tests passed locally before release; deployed recovery code and normal navigation were verified here. No artificial network failure was injected into the live browser, and this single successful session cannot prove an intermittent network problem will never recur.

No orders were submitted, no existing content or request was changed. The temporary 1 kg Cukkini reservation was removed. Both browser tabs are left logged out following the session regression.

Evidence under `src/artifacts/uat-20261013/`: `regression-reserved-stock.jpg`, `regression-dashboard-still-mismatch.jpg`.

## Recheck after API republish

User republished the API and restored the admin session. Navigated to `/admin` and inspected the freshly loaded dashboard.

Expected: one pending request in both summary and list, including the request older than seven days.

Actual: summary showed 1; the list showed BK-20260802-13596586, dated 2026-08-02 23:00. The prior empty-list message was absent. Both cancelled UAT orders remained cancelled; product/post totals remained 23/10.

Result: **PASS — deployed dashboard mismatch resolved.** This supersedes the earlier failed dashboard check above. No data was changed during this recheck. Admin session left signed in.

Evidence: `src/artifacts/uat-20261013/regression-dashboard-fixed.jpg`. Previously stated limits for live fault injection and intermittent failures still apply.

## Guest email and GLS follow-up

- Guest email: user confirmed receipt of BK-20261013-C6C213EF at the authorized test inbox, with matching one Bazsalikom / 500 Ft / personal pickup / payment-at-pickup details. PASS (user inbox verification).
- GLS locker: attempting to continue without a point correctly displayed validation and focused the map button. Map loaded visually with search and location markers; no browser errors were returned. PASS for required-point validation and map loading.
- Map selection itself is pending user assistance: the provider renders a closed shadow-root component, unavailable to this browser automation. No fabricated selection or direct state injection was used. Test contact is synthetic UAT TEST GLS with the authorized inbox. No GLS order submitted.

## Mobile focus-zoom follow-up

User reports persistent focus zoom on iPhone Chrome. Added a minimum 16px/rem text size for editable controls on narrow screens or coarse-pointer devices, before focus as well as after blur. Normal pinch zoom remains enabled; no viewport maximum-scale restriction or forced blur zoom reset was added. This prevents the common small-text focus-zoom trigger rather than attempting to override a user's zoom.

Four mobile Chromium regression cases passed (login, registration, password-reset request, product quantity): fields at least 16px, stable width after focus/blur, no horizontal overflow, no zoom-disabling viewport setting. Production build and two SEO checks passed, with ten post previews. Physical iPhone Chrome behavior is not simulated by these checks and remains for device verification after upload.

Frontend-only package: `src/artifacts/releases/mobile-inputs-20261014/frontend-mobile-inputs-20261014.zip`. Includes prior fixes. No API publish or migration needed for this CSS change. Upload new assets before HTML and retain old hashed assets during deployment.

## GLS email address link correction

User subsequently reported successful GLS selection/order testing and receipt of its email, but the mail client split Veresegyház around á when automatically linking the plain address. This is user-reported completion, not an independently observed full GLS checkout.

Code inspection found no explicit Maps link in the email template. Added explicit whole-address links for GLS pickup/address rows in confirmation, business-notification and status-change HTML emails. The Maps search query uses URI encoding of the saved postal code, city, street address and country; HTML text/attributes remain escaped. Plain-text email also includes an explicit encoded map URL. The exact mail-client transformation was not independently inspected.

Validation: all 25 EmailTemplateServiceTests passed, including locker and shop cases using Veresegyház, Szőlő, ampersands and HTML-like point names. Tests verify complete anchor text and identical encoded destination queries across email types.

Deployment: republish API; no database migration or environment-variable change. Applies to newly generated emails, not already delivered messages or previously rendered outbox bodies. A newly generated email must be clicked in the user's email client after deployment to verify the observed client-specific symptom is resolved.

## Final published smoke and regression run

Date: 14 October 2026. User confirmed publication. Current frontend entry: `/assets/index-8n71uBWg.js`.

| Action | Expected | Actual | Result |
| --- | --- | --- | --- |
| Fetch home/login HTML and API live/ready health endpoints | Successful responses; HTML revalidated | All returned 200; HTML Cache-Control no-cache | PASS |
| Request missing asset `/assets/uat-missing-final.js` | Real 404, not SPA HTML | 404, no app entry | PASS |
| Browse home/carousel and About page | Static images render | All 10 homepage images loaded after carousel activation; all 4 About-page images loaded | PASS |
| Browse login and inspect mobile controls | Readable input sizes; zoom remains available | Email/password 16px; unrestricted viewport; no horizontal overflow | PASS for inspected viewport |
| Open basket and continue to checkout | Basket retained and checkout renders | One Bazsalikom / 500 Ft retained; checkout rendered; no order submitted | PASS |
| Rerun mobile, route-recovery and product-loading Playwright suites | No regression | 11 passed | PASS — local browser tests, not live fault injection |
| Rerun EmailTemplateServiceTests | Correct encoded links and escaped content | 25 passed | PASS — local unit tests |
| User clicks link in a newly generated GLS email after API publication | Correct Veresegyház pickup location opens | User confirmed “New email link works correctly” | PASS — user email-client verification |
| Inspect authenticated dashboard after user sign-in | Pending count/list agree; prior test cleanup retained | Count 1 and August 2 pending request visible; both previous UAT orders cancelled; products/posts 23/10 | PASS |
| Fully navigate from dashboard to order list | Valid session remains authenticated | Redirected to admin login; no captured browser errors | INCONCLUSIVE — immediate sign-in/refresh reproduction requested |

Dashboard evidence: `src/artifacts/uat-20261013/final-smoke-dashboard.jpg`. Public navigation produced no captured browser errors. A locator click failed in the automation layer; direct navigation to its observed destination was used. This alone is not classified as an application click defect.

No orders or admin records were created, changed or deleted in this final run. The earlier agent test basket remains unsubmitted. Two newer user-created GLS orders are visible; they were not modified.

Readiness: public smoke, targeted automated regressions, dashboard correction, guest email and corrected GLS email link have passed. Physical iPhone Chrome focus/blur verification and the admin sign-in/refresh check remain outstanding. Do not describe every test as passed until those checks are resolved. Online payment/refund, concurrent populated-cart merge, image upload/publication and session survival across pool recycling remain outside the verified scope; this is UAT readiness assessment, not unrestricted production certification.

## Final UAT readiness decision

User completed both outstanding checks on 14 October 2026:

| Action | Expected | Actual | Result |
| --- | --- | --- | --- |
| Sign in as admin and immediately refresh | Remain authenticated | User confirmed “Stayed signed in after refresh” | PASS — user verification |
| On physical iPhone Chrome, refresh the published site, focus an input, then leave it | Page remains at normal size | User confirmed “Yes, normal size” | PASS — user device verification |

These confirmations supersede the outstanding checks in the preceding readiness assessment. The earlier admin redirect was not reproduced by the immediate sign-in/refresh check; its cause was not established.

**Decision: READY FOR UAT within the documented tested scope.** Published smoke checks and targeted regressions passed, including user-assisted email and physical-device verification. No unresolved blocker remains in this tested scope. This does not expand coverage to the excluded scenarios listed above or constitute production sign-off. No further publish is required for the fixes verified in this report.
