# Checkout pricing and GLS UAT setup

This guide owns checkout prices, payment combinations, GLS destination selection and their release checks. The application is in UAT; these settings do not establish client acceptance or a production carrier agreement. Use the [configuration guide](configuration.md) for secrets and SumUp setup, and the [deployment runbook](deployment-runbook.md) for backups and migration execution.

## Provisional prices and scope

The client plans small parcels in the XS–M range. For UAT, the webshop uses the following fixed customer-facing delivery fees; it does not calculate parcel dimensions, weight, packing, multiple parcels or a live GLS shipping charge.

| Fulfillment | Configuration key | Fee (HUF) | Allowed payments |
| --- | --- | ---: | --- |
| Personal pickup | `PersonalPickup` | 0 | Online card, payment at pickup |
| Home delivery | `HomeDelivery` | 2740 | Online card, cash on delivery |
| GLS locker | `ParcelLocker` | 2090 | Online card, cash on delivery if the point supports collection |
| GLS CsomagPont | `ParcelShop` | 2590 | Online card, cash on delivery if the point supports collection |

These approximate test prices were selected for the project on 2026-09-30. They are not a claim that each amount equals a current public GLS tariff. Before production, obtain the client's contracted rates and approve the fees customers will pay, including any packaging or other costs. GLS distinguishes occasional parcel sending from [regular business shipping quotations](https://gls-group.com/HU/hu/rendszeres-csomagfeladas/arajanlatkeres/).

The provisional COD fees and limits follow the published [GLS CsomagPont CashService schedule](https://gls-group.com/HU/hu/gls-atadopontok/csomagpont/csomagfeladas/) (reference checked 2026-10-01). Applying those tiers to every enabled delivery method is a webshop UAT policy, not proof of a contracted carrier rate for that method. The inclusive fee calculation below is the application's explicit policy.

## Pricing and checkout contract

`CheckoutPricing` in the API configuration owns delivery fees and enabled payment methods. These are provisional UAT values, not a GLS contract or live carrier quote. Product prices remain in the database. `DeliveryFees` uses named fulfillment keys; only configured methods with an allowed payment combination are offered. The backend configuration enables personal pickup (0 HUF), home delivery (2740 HUF), GLS lockers (2090 HUF), and GLS CsomagPont shops (2590 HUF).

`EnabledPaymentMethods` uses `OnlineCard`, `PayAtPickup`, and `CashOnDelivery`. Pickup permits online card or payment at pickup; deliveries permit online card or COD. Actual online payment also needs the separate SumUp configuration. `CodLowerFee`, `CodUpperFee`, `CodLowerLimit`, and `CodMaximum` configure provisional COD tiers (600/1190 HUF, 30000/499999 HUF). The tier basis is the final collected amount, including products, delivery and COD fee: choose the lower fee if it fits the lower limit, otherwise the upper fee, and reject amounts above the maximum. The branch supports HUF only and rounds each product line to whole forints, midpoint away from zero, before summing; fees must be nonnegative whole forints. Invalid configuration fails startup.

`GET /api/orders/checkout-methods` returns configured method combinations. `POST /api/orders/quote` accepts numeric `fulfillmentMethod` and `paymentMethod` identifiers and derives the cart from the authenticated user or existing guest session. Its response includes line items, currency, subtotal, delivery fee, payment fee, total and a comparison `version`. New order requests must send the selected `paymentMethod` and `quoteVersion`. The server recalculates; a changed quote returns 409 without persisting stock, order, or email effects. Repeated submissions of an existing order retain the original snapshot. The version is an optimistic comparison token, not a signed payment authorization or a guaranteed price reservation.

API identifiers: fulfillment `PersonalPickup=0`, `HomeDelivery=1`, `ParcelLocker=2`, `ParcelShop=3`; payment `OnlineCard=0`, `PayAtPickup=1`, `CashOnDelivery=2`. Use named constants in clients. Historical order snapshot fields are nullable: never infer a historical payment method or fee from today's configuration.


Checkout reads enabled delivery/payment combinations from the API and obtains a quote after a valid selection. Its review displays the quoted line items and subtotal/delivery/payment/total breakdown, including items added in another browser tab. Submission sends the payment method and quote version, never a client-calculated total. Changed selections invalidate the quote; out-of-date quote responses cannot replace the current selection. A submission conflict closes the review, refreshes pricing and requires renewed review and legal consent. Pricing failures block submission and offer retry without clearing the form.

Order success, customer/admin details and order emails show the saved breakdown and GLS destination. Historical fields remain unknown rather than being recalculated from current fees. The payment-return page obtains these details through `GET /api/orders/{id}/receipt`, which checks the account or original guest-cart session and returns `Cache-Control: no-store`. It does not add personal details to the limited public payment-status response. If the owning session is unavailable, payment status can still be shown but the detailed receipt is omitted.

SumUp charges the saved order total and currency. An order explicitly placed with pickup payment or cash on delivery cannot initiate online payment. Legacy orders with a null payment method retain their previous online-payment path, subject to existing ownership/status checks; the UI labels their method as unknown. A saved currency different from the current SumUp configuration blocks checkout instead of silently converting it. Already paid/refunding/refunded orders cannot start another payment, and provider confirmation compares the saved currency (configured currency only for legacy orders). Provider attempt reconciliation remains a separate reliability task.

## GLS delivery point validation

For locker/shop quotes and orders, send `glsDeliveryPointId` together with the matching fulfillment method. `GET /api/shipping/gls/points/{id}?type=2` (locker) or `type=3` (shop) resolves a selection for display. The API independently resolves the ID using the [GLS Hungarian delivery point directory](https://map.gls-hungary.com/data/deliveryPoints/hu.json); it accepts only Hungarian points with the matching provider type and delivery capability. A manual address cannot substitute for a valid GLS point. Cash on delivery additionally requires the point to advertise cash or card acceptance.

The server includes the resolved destination in the quote version and saves its ID, name, country and address on the order. Postal code and city are saved in the existing delivery address fields; `DeliveryStreet` contains the provider's complete street address, so a separate house number is not required. A changed destination requires a new quote. Existing order retries return their saved snapshot without depending on the current GLS directory.

The validated `Gls` options default to `CountryCode=HU`, `CacheMinutes=15`, `TimeoutSeconds=10`, `RetryDelaySeconds=30`, and `MaximumResponseBytes=8388608`. `DirectoryUrl` defaults to the public HTTPS feed above; no MyGLS credentials are needed. Set overrides through `Gls__<OptionName>` on the API host. Redirects are disabled. Allow outbound HTTPS to the configured directory host. The cache is per API process, refreshes are serialized, and failed refreshes have a short retry delay. Validation uses a fresh cached snapshot, not a live carrier guarantee on every request. Expired data is not served when refresh fails: GLS operations return 503, while personal pickup and home delivery remain independent of this directory. Unknown or mismatched selections return 409; missing selection IDs return 400.

Drafts store the selected method and point ID and discard manual delivery fields for locker/shop delivery. Restoring a draft resolves the point again; a removed point or provider outage returns `deliveryPointError` alongside the retained customer fields. Quote and order submission still validate the selection. This integration validates destinations only; it does not book shipments or create labels.

## GLS map picker

The checkout presents GLS lockers and CsomagPont shops separately. Opening the picker loads the [official GLS widget](https://map.gls-hungary.com/widget/example/) from `https://map.gls-hungary.com/widget/gls-dpm.js`. It is configured for Hungary, Hungarian language, delivery capability, and the selected point type. Only the selected ID is sent to the API; the UI displays the address returned by server validation. Switching delivery modes clears the selection. Restored draft IDs are checked again before continuing. Script-load errors, invalid points and directory outages support retry or choosing another delivery method.

The widget runs inside a native modal dialog, including Escape dismissal and return of focus to the opening button. The browser CSP permits scripts, styles and fonts from `https://map.gls-hungary.com`; existing HTTPS connection and image permissions cover map data and tiles. Mirror these allowances if the host also sends a CSP response header. The provider script and its dependencies are loaded from GLS at runtime, so include the real widget in hosted smoke checks after release or provider updates. No customer contact or billing details are passed to the widget. GLS and its map providers receive normal browser network requests when it opens; users can choose another fulfillment method without opening it.

Before deployment, check the actual widget on desktop and mobile: open each point type, search for a town/postal code, select a result, verify its name/address against the confirmed summary, change it, reload a saved draft, and switch back to home delivery/pickup. Use keyboard search, Tab/Shift+Tab and Escape; verify focus returns and the dialog does not overflow horizontally. Block the widget script or directory request to check retry and alternative-method recovery. Browser regression tests mock the official custom-element boundary and the API; they do not prove live carrier availability. Confirm that reviewed, saved, emailed and charged amounts agree in the deployed environment before accepting the release.

## Changing configuration

Edit `CheckoutPricing` in the API configuration, or supply host environment overrides such as `CheckoutPricing__DeliveryFees__HomeDelivery=2740`. `CheckoutPricing__EnabledPaymentMethods__0=OnlineCard` is an indexed array entry. Configure every application instance consistently and restart it after changing options; this implementation uses startup options, not a live admin price editor.

ASP.NET configuration layers merge dictionary keys and array indices. Omitting a method from an environment-specific file or setting a shorter array does not necessarily remove entries inherited from `appsettings.json`. To disable a method, remove its base configuration entry and check the complete effective configuration before release. There is no per-method enable flag or administrative pricing screen. Verify the returned `checkout-methods` list and a direct quote request; hiding a radio button is insufficient. If SumUp is disabled, also remove `OnlineCard` from the effective enabled methods so customers are not offered an unusable payment path.

Do not add delivery fees to frontend constants. Product prices come from SQL; delivery/COD amounts come from validated options. For a product line priced at 10.25 HUF with quantity 2, the line total is 21 HUF. Round each line before adding the subtotal, not the final basket only.

For home delivery, these examples demonstrate the fee-inclusive COD policy:

| Product subtotal | Delivery | COD fee | Collected total | Result |
| ---: | ---: | ---: | ---: | --- |
| 26660 | 2740 | 600 | 30000 | Lower tier, inclusive limit |
| 26661 | 2740 | 1190 | 30591 | Upper tier |
| 496069 | 2740 | 1190 | 499999 | Maximum accepted |
| 496070 | 2740 | 1190 | 500000 | Rejected |

The fee jump is intentional. A quote is recalculated at order submission; a changed price, fee, payment choice or GLS destination requires a fresh review. An existing order retains its saved amount after configuration or catalogue changes.

## Migrations and deployment compatibility

Deploy the API and frontend from the same release. The older frontend does not send the required payment choice and quote version. During the deployment window, stop checkout traffic, apply the reviewed schema changes, replace the API and frontend, and then run hosted smoke checks. An already-open old browser tab may need a refresh.

These migrations are required, in the existing migration history order:

- `20260930063759_AddOrderPricingSnapshots`
- `20260930111436_AddGlsDeliveryPointSnapshots`

Follow the [migration checks](deployment-runbook.md#migration-checks) and backup gate for the verified target database. Check its `__EFMigrationsHistory` after execution. A successful local migration or disposable SQL test is not evidence of hosted deployment. This guide adds no migration.

Both migrations are additive. Existing totals and line amounts stay unchanged; legacy pricing/payment/currency and GLS snapshot fields remain null rather than being guessed from current settings. Test upgrades using representative existing data. Downgrading these migrations drops snapshot data. Prefer retaining the additive schema and a reviewed forward fix. Rolling back binaries is not automatically safe: roll back the API and frontend together, verify their schema compatibility and whether the older code understands `ParcelShop=3` and newly created orders before reopening traffic. Preserve a database backup and both application packages.

## Verification and release evidence

Use the commands in [engineering verification](../verification.md); SQL-backed tests require the disposable SQL Server test connection and must run without skips for release evidence.

| Concern | Automated coverage |
| --- | --- |
| Allowed combinations, private/company billing, fabricated amounts, changed/disabled configuration, persisted totals, retry effects and legacy migrations | `CheckoutPricingTests` (real API and SQL) |
| COD boundaries across delivery methods, line rounding, invalid configuration and quote versions | `CheckoutPricingServiceTests` |
| Point replacement, outage recovery, snapshots, COD capability and drafts | `GlsDeliveryPointTests` (real API and SQL; fake carrier directory) |
| Provider schema, cache expiry, concurrent refresh and failure handling | `GlsDeliveryPointDirectoryTests` |
| Saved payment amounts/currency, incompatible payment initiation and email snapshots | `SumUpCheckoutServiceTests`, `OrderServiceTests`, `EmailTemplateServiceTests` |
| Review, stale responses, consent renewal, receipt display, widget interaction and focus | Browser pricing, GLS and accessibility suites (mock API/provider boundaries) |

Keep actual results in the private release record with the tested commit and environment. Automated tests do not book shipments, charge real cards or prove SMTP delivery. Execute the real-widget checks above and the linked [UAT scenarios](../uat-checklist.md) after deployment. For an agreed online-payment test, compare the reviewed total, saved order, email and provider amount; for offline payments, verify that online checkout cannot be initiated. Check a legacy order without inventing a fee breakdown.

Before production, confirm the client's parcel assumptions and contracted prices, enabled payment methods, shipping/label workflow and final customer instructions. Destination selection does not include shipment booking, labels, tracking, guaranteed locker capacity or a packing calculation. Payment-attempt reconciliation remains a separate reliability task; completing this branch alone does not establish readiness of the whole application.

