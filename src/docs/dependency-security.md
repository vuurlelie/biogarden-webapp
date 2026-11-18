# Frontend dependency maintenance

Keep `package.json` and `package-lock.json` together. Use `npm ci`, `npm audit --audit-level=moderate`, lint, typecheck, build, SEO verification and browser tests before merging dependency updates. Do not lower the audit threshold or use `npm audit fix --force` as a substitute for compatibility checks.

The TypeScript ESLint parser currently requires minimatch 10. A scoped override selects minimatch 11.0.1 to address its glob-complexity advisory. Other consumers retain their supported minimatch major. In particular, forcing brace-expansion 5 onto minimatch 3 breaks ESLint's CommonJS interface. Remove the scoped override when the upstream parser dependency incorporates the fix.

## Upstream follow-up, checked 2026-11-18

The initial Vite, Zod and minimatch findings are addressed by the locked updates. However, [GHSA-fc5q-39xc-vjpf](https://github.com/advisories/GHSA-fc5q-39xc-vjpf), updated November 17, lists Picomatch through 4.0.7 as affected and no patched version. Picomatch is a transitive Vite build-tool dependency; it is not included as a server in the static deployed website. This does not establish that the build tooling is unaffected.

During verification npm first reported this advisory, then returned zero vulnerabilities for the same Picomatch version after a reinstall. Treat the upstream advisory as unresolved despite that inconsistent registry response. Keep build glob patterns repository-controlled, do not run the development server as the public deployment, and update/retest when an upstream fix becomes available. The CI audit remains enabled and may fail as the registry advisory data changes. A green audit alone is not evidence that this advisory has been fixed.
