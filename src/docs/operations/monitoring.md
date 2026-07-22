# Monitoring And Incident Triage

This project exposes lightweight operational checks and correlation metadata so production issues can be diagnosed without exposing sensitive data.

## Health Endpoints

`GET /health/live`

Use this endpoint for basic process liveness checks. It returns healthy when the API process can receive HTTP requests.

`GET /health/ready`

Use this endpoint for readiness checks. It verifies that the API can connect to the configured database before traffic is considered safe to serve.

Recommended behavior:

- Liveness failures should restart the API process.
- Readiness failures should remove the API instance from traffic until the database connection is healthy again.
- Monitoring should alert on repeated readiness failures, not on a single short failure.

## Correlation IDs

Every API response includes an `X-Correlation-ID` header.

Clients and operators can also send an `X-Correlation-ID` request header. If the incoming value is present and reasonably short, the API uses it as the request trace identifier and returns the same value in the response.

Use the correlation ID to connect:

- the browser/network request seen by a user,
- the API error response `traceId`,
- server request logs,
- unexpected-error alert emails.

## Error Alerts

Unexpected server errors are sent through the configured error-alert email path when `ErrorAlerts:Enabled` is true. Validation, authentication, not-found, and normal operation conflicts are intentionally not alert emails.

Alert emails should contain enough context to identify the failing request without including sensitive user input:

- correlation/trace ID,
- HTTP method,
- request path,
- whether a query string was present.

## Production Checklist

- Configure external uptime monitoring for `/health/live`.
- Configure load balancer or hosting readiness checks for `/health/ready`.
- Keep API logs long enough to investigate customer reports using `X-Correlation-ID`.
- Configure alert email delivery only with production SMTP credentials stored outside source control.
- Test one controlled unexpected-error alert before launch.
