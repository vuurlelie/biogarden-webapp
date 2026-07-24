# Biogarden Frontend

React + TypeScript + Vite client for the Biogarden website, webshop, account area, checkout, and admin interface.

## Local Commands

Run commands from the repository `src` directory:

```bash
npm.cmd --prefix frontend/biogarden-client install
npm.cmd --prefix frontend/biogarden-client run dev
npm.cmd --prefix frontend/biogarden-client run typecheck
npm.cmd --prefix frontend/biogarden-client run lint
npm.cmd --prefix frontend/biogarden-client run build
npm.cmd --prefix frontend/biogarden-client run test:e2e
```

Use `npm` instead of `npm.cmd` on Linux/macOS or in GitHub Actions.

## Environment

The browser app reads the API base URL from:

```text
VITE_API_BASE_URL=https://your-api-domain.example
```

For local development this is usually the WebApi URL shown by Visual Studio or `dotnet run`. Do not commit `.env` files with real production values.

## Structure

- `src/app`: app-level routing, providers, and layouts.
- `src/features`: feature-owned API clients, components, types, and utilities.
- `src/pages`: route-level page components.
- `src/shared`: cross-feature components, hooks, constants, API helpers, and utilities.
- `e2e`: Playwright browser tests for critical public/account flows.
- `scripts`: SEO file generation and verification scripts.

## Quality Checks

Before opening a frontend PR, run:

```bash
npm.cmd --prefix frontend/biogarden-client run typecheck
npm.cmd --prefix frontend/biogarden-client run lint
npm.cmd --prefix frontend/biogarden-client run build
npm.cmd --prefix frontend/biogarden-client run test:e2e
npm.cmd --prefix frontend/biogarden-client audit --audit-level=moderate
```

The build generates SEO files and must not leave generated artifacts staged unless the source change intentionally updates them.
