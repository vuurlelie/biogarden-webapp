import { test as base, type BrowserContext } from "@playwright/test";
export { expect } from "@playwright/test";
export type { Page, Route } from "@playwright/test";
export type MockSession = { userId: string; email: string; roles: string[]; sessionId: string; expiresAtUtc: string };
const sessions = new WeakMap<BrowserContext, MockSession | null>();
export function setMockSession(context: BrowserContext, session: MockSession | null) { sessions.set(context, session); }
export function getMockSession(context: BrowserContext) { return sessions.get(context) ?? null; }
export const test = base.extend<{ browserSession: void }>({
  browserSession: [async ({ context }, use) => {
    sessions.set(context, null);
    await context.route(
      "http://localhost:5097/api/auth/session",
      route => {
        const session = getMockSession(context);
        return route.fulfill(session ? { json: session } : { status: 204 });
      }
    );
    await context.route(
      "http://localhost:5097/api/auth/logout",
      route => {
        sessions.set(context, null);
        return route.fulfill({ status: 204 });
      }
    );
    await use();
  }, { auto: true }],
});
