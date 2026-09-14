# Biogarden — Bakony Kincse Biokertészet

A full-stack e-commerce and content platform built for **Bakony Kincse Biokertészet**, a Hungarian organic smallholding. The project supports the business' online presence, product sales, customer accounts, and day-to-day catalogue and order administration.

> **Project status: User Acceptance Testing (UAT).** The application is under client review. Client feedback and outstanding business decisions are pending; production launch is subject to their resolution and final acceptance.

**[View the UAT preview](https://biogarden.raczkrisztina.dev/)** — Client review environment; content and functionality may change during UAT.

## Business context

The application brings the farm's story, products, and purchasing experience into one website, with an administration area for the business to manage its content and shop operations. Development covers customer-facing journeys as well as the operational requirements behind them: stock handling, order processing, payment status, customer communication, and deployment preparation.

This repository presents the implementation of a real client project through its UAT phase. The current milestone is client validation of the delivered workflows and agreement on the remaining launch requirements.

## Delivered scope

| Area | Business capability |
| --- | --- |
| Public website | Introduces the farm, presents products, publishes articles, and provides a contact form. |
| Storefront and checkout | Product browsing, shopping cart, order review, and checkout with individual and company billing details. |
| Customer accounts | Authentication, profile and address management, and access to customer orders. |
| Business administration | Product, inventory, order, customer, and article management, with a dashboard and rich-text content editing. |
| Payment and communication | SumUp checkout and payment-status integration, plus configurable SMTP customer and administrator notifications. |
| Operational support | Health checks, request tracing, abuse protection, and documented deployment, backup, monitoring, and recovery procedures. |

Payment and email availability depend on environment configuration. Integration code in this repository does not imply that production services are enabled or that launch approval has been granted.

## Engineering approach

- **Separate presentation, business logic, and persistence:** the ASP.NET Core API delegates workflows to business services, with Entity Framework Core handling SQL Server persistence through the data-access layer.
- **Feature-based frontend:** checkout, cart, accounts, products, content, and administration have dedicated feature modules, supported by shared UI and API utilities.
- **Business-flow verification:** backend unit and API tests cover service and request behaviour; Playwright suites exercise critical browser flows, error states, accessibility, privacy, and SEO behaviour.
- **Release checks in CI:** GitHub Actions builds both applications and runs backend tests, formatting checks, frontend linting and type checks, browser tests, SEO verification, and dependency audits.
- **Documented handover:** setup instructions and operational runbooks accompany the application so deployment and maintenance have an explicit process.

## Technology

| Layer | Stack |
| --- | --- |
| Frontend | React 19, TypeScript, Vite, React Router, Tailwind CSS, Tiptap |
| Backend | C#, ASP.NET Core Web API, .NET 9 |
| Data and authentication | SQL Server, Entity Framework Core, ASP.NET Core Identity, JWT |
| Integrations | SumUp, SMTP email |
| Verification and CI | Backend unit/API tests, Playwright, ESLint, TypeScript checks, GitHub Actions |

## Repository structure

```text
src/
├── backend/
│   ├── Biogarden.WebApi/          API endpoints, authentication, middleware, startup
│   ├── Biogarden.BusinessLogic/   Business services and workflow orchestration
│   ├── Biogarden.DataAccess/      Entities, repositories, database migrations
│   └── tests/                    Unit and API tests
├── frontend/biogarden-client/     React application and browser tests
└── docs/                         Developer and operational documentation
.github/workflows/                Continuous integration
```

## Run locally

Prerequisites: .NET 9 SDK, Node.js 24 (the CI version), npm, SQL Server, and the Entity Framework Core CLI for migrations.

Start with the [developer guide](src/docs/developer-guide.md) for database configuration, local secrets, migrations, and frontend environment variables. Once configured, run these commands from the repository's `src` directory in separate terminals:

```bash
# API
dotnet run --project backend/Biogarden.WebApi/Biogarden.WebApi.csproj
```

```bash
# Frontend
npm --prefix frontend/biogarden-client ci
npm --prefix frontend/biogarden-client run dev
```

Set `VITE_API_BASE_URL` to your local API address. Keep credentials in local user secrets or environment configuration. On Windows, use `npm.cmd` if PowerShell blocks the npm script.

## UAT and launch

UAT is the current delivery milestone. The next steps are to collect client feedback, resolve the client's outstanding business decisions, apply agreed changes, and complete final acceptance and launch verification.

The [configuration guide](src/docs/operations/configuration.md) and [deployment runbook](src/docs/operations/deployment-runbook.md) document environment and operational requirements. Production payment, email, and business-policy settings must be confirmed for the launch environment.

## Further reading

- [Developer guide and verification commands](src/docs/developer-guide.md)
- [Frontend documentation](src/frontend/biogarden-client/README.md)
- [Monitoring](src/docs/operations/monitoring.md)
- [Backup and recovery](src/docs/operations/backup-recovery.md)
- [Abuse protection](src/docs/operations/abuse-protection.md)
- [License](LICENSE)
