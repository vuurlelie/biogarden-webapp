# Request validation

The API uses FluentValidation core validators for business request bodies. A global asynchronous action filter resolves the validator and awaits it before the controller runs. The deprecated ASP.NET integration package is no longer used.

## Responsibilities

| Layer | Responsibility |
| --- | --- |
| Model binding | Reject malformed JSON and values that cannot be converted to DTO types |
| Request validators | Required input, lengths, ranges, enums, nested addresses and conditional input rules |
| Services and Identity | Ownership, permissions, current stock, order/payment transitions, account state and password policy |
| Database | Constraints, uniqueness and concurrency protection |
| Options validation | Configuration checks at startup |
| Frontend | Immediate feedback and display of authoritative server errors |

Service guards remain in place. Request validation does not replace authorization, transactions or checks against current database state.

## Project-specific rules

- Authentication requests validate their input; Identity still enforces password and account policies.
- Company registration requires company details and a billing address. Profile editing permits incomplete company details to be saved; a supplied tax number must have a valid format. These flows intentionally have different requirements.
- Account address validators enforce the storage limits and report nested fields such as `shippingAddress.postalCode`.
- Product and post requests validate text lengths, amounts and enum values. Inventory updates reject negative stock.
- Checkout validates the idempotency key, current legal versions, fulfillment method and manual contact/address inputs. Authenticated customers can use saved details; services validate the resolved account data. Partial manual contact details must form a complete contact set.
- Guest company checkout preserves the existing company-name/tax-number pairing rule: both may be absent, but supplying one requires the other. This refactor does not introduce a new business requirement.
- Cart quantities, contact messages, withdrawal requests and admin actions have explicit request validators.

File upload checks remain in controllers, and list/query normalization remains in the existing implementation. This change requires no database migration.

## Error contract

Invalid request bodies return HTTP 400 with `application/problem+json`, for example:

```json
{
  "type": "about:blank",
  "title": "Hibás adatok.",
  "status": 400,
  "detail": "Ellenőrizd a megadott adatokat.",
  "instance": "/api/orders",
  "traceId": "request-correlation-id",
  "errors": {
    "customerEmail": ["Érvényes e-mail-címet adj meg."]
  }
}
```

Messages depend on the failed rule. Field names use camel case, including each segment of nested paths. Binding errors can be request-level rather than associated with a visible input. The trace identifier can be used to correlate the response with server logs.

The result filter adapts controller 4xx responses to the common envelope while preserving status codes. Exception handling uses the same shape and does not expose unexpected exception details. State-dependent service failures can remain form-level errors. Authentication challenges and rate-limiter responses are outside the controller filter; clients must continue to handle empty or legacy error bodies.

The shared frontend HTTP client retains feature-specific error classes and attaches structured field errors. Login, registration, password reset, profile and admin forms map supported fields to their existing inline feedback. Checkout returns to the details step for editable field errors without discarding the draft, and maps legal-version errors to consent fields. Unmapped fields still appear in the error summary.

## Extending validation

1. Decide whether a rule concerns request input or current business state. Keep database-dependent decisions in services.
2. Add or extend the request's `AbstractValidator<T>` in `Biogarden.BusinessLogic/Validation`, with Hungarian messages and nested validators where appropriate.
3. Preserve valid existing flows, including authenticated checkout using saved details and optional profile fields.
4. Map new frontend fields explicitly; retain a summary for errors that cannot be attached to an input.
5. Test invalid input and meaningful valid/conditional cases. API tests check that business DTO request bodies have registered validators and that invalid requests fail before database access. Browser tests cover server field feedback and malformed-error fallback.

Validators are registered from the business assembly. OpenAPI describes DTO types, but FluentValidation constraints are not automatically exported into its schema. Validators and their tests are the source of truth for detailed input rules.

See [Engineering verification](verification.md) for the test and build commands.

