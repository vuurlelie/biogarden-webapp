# Abuse Protection

This project uses ASP.NET Core rate limiting to reduce automated abuse against public and sensitive API endpoints.

## Current Limits

The API has a configurable global fixed-window limit of 120 requests per minute for each authenticated user or IP address.

Some endpoints have stricter limits:

| Policy | Limit | Endpoints |
| --- | ---: | --- |
| Sensitive auth | 5 requests / minute | Register, login, email confirmation, resend confirmation, forgot password, reset password |
| Public form | 3 requests / minute | Contact message submission, order creation |
| Payment | 20 requests / minute | SumUp checkout creation, SumUp checkout status callback |

When a limit is exceeded, the API returns:

```text
429 Too Many Requests
```

The response also includes a `Retry-After` header and a JSON error body with a `traceId`.

## Configuration

The limits are configured in `AbuseProtection` inside the API configuration:

```json
"AbuseProtection": {
  "Global": {
    "PermitLimit": 120,
    "WindowSeconds": 60
  },
  "SensitiveAuth": {
    "PermitLimit": 5,
    "WindowSeconds": 60
  },
  "PublicForm": {
    "PermitLimit": 3,
    "WindowSeconds": 60
  },
  "Payment": {
    "PermitLimit": 20,
    "WindowSeconds": 60
  },
  "RetryAfterSeconds": 60
}
```

All values must be positive. The API validates them on startup.

## Client Identity

Rate limits are partitioned by authenticated user ID when the request has a valid signed-in user. Anonymous requests are partitioned by client IP address.

The API also reads forwarded headers:

- `X-Forwarded-For`
- `X-Forwarded-Proto`

This matters in production because the hosting platform or reverse proxy may otherwise make many real visitors appear to come from the same server IP.

Before launch, confirm that the production host or proxy sends trusted forwarded headers correctly. If the hosting environment requires explicit trusted proxy/network configuration, add it before going live.

## Manual Testing

To test the auth limit locally, send more than five login attempts within one minute from the same client:

```bash
for i in {1..6}; do
  curl -i -X POST http://localhost:5097/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@example.com","password":"WrongPassword1"}'
done
```

The first five requests should reach the login flow. The sixth request should return `429 Too Many Requests`.

Check the response headers too:

```text
Retry-After: 60
```

## What To Watch After Launch

Review these limits after real traffic appears:

- If real customers hit `429` during normal checkout, raise the public form or payment limits.
- If the contact form receives spam while staying below the limit, add a CAPTCHA or challenge step.
- If login attacks continue, keep the strict auth limit and consider temporary IP blocking at the hosting/firewall layer.
- If SumUp callback delivery is retried aggressively, adjust only the payment policy.

Do not loosen all limits globally just to solve one endpoint problem. Tune the smallest policy that matches the real issue.
