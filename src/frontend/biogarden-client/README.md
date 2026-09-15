# Biogarden Frontend

React + TypeScript + Vite client for the Biogarden website, webshop, account area, checkout, and admin interface.

## Setup and verification

Use the [developer guide](../../docs/developer-guide.md#frontend-setup) for installation and startup, [engineering verification](../../docs/verification.md#frontend) for checks, and [configuration](../../docs/operations/configuration.md#frontend-environment) for environment settings.

## Structure

- `src/app`: app-level routing, providers, and layouts.
- `src/features`: feature-owned API clients, components, types, and utilities.
- `src/pages`: route-level page components.
- `src/shared`: cross-feature components, hooks, constants, API helpers, and utilities.
- `e2e`: Playwright browser tests for critical public/account flows.
- `scripts`: SEO file generation and verification scripts.

## Build notes

The build generates SEO files. Inspect generation warnings when article data is unavailable and keep generated output out of commits. Browser tests use mocked API responses; deployed behaviour is recorded separately through the [UAT checklist](../../docs/uat-checklist.md).
