import { expect, test } from "@playwright/test";
import { mockCheckoutApi, mockEmptyCartApi } from "./fixtures";

test("checkout returns to editable details and renders server field errors", async ({ page }) => {
  await mockCheckoutApi(page);
  await page.route("http://localhost:5097/api/orders", async (route) => {
    await route.fulfill({ status: 400, contentType: "application/problem+json", json: {
      title: "Hibás adatok.", detail: "Ellenőrizd a megadott adatokat.", status: 400,
      traceId: "validation-test", errors: { customerPhone: ["A szerver nem fogadja el ezt a telefonszámot."] },
    } });
  });
  await page.goto("/penztar");
  await page.getByLabel(/Nev|Név/).fill("Teszt Elek");
  await page.getByLabel(/Email cim|Email cím/).fill("teszt@example.com");
  const phone = page.getByLabel(/Telefonszam|Telefonszám/);
  await phone.fill("+36301234567");
  await page.getByRole("button", { name: /Tovabb a penztarhoz|Tovább a pénztárhoz/ }).click();
  await page.getByLabel(/Fizetes atvetelkor|Fizetés átvételkor/).check();
  await page.getByRole("button", { name: /Rendeles attekintese|Rendelés áttekintése/ }).click();
  await page.getByLabel(/Altalanos Szerzodesi Feltetelek|Általános Szerződési Feltételek/).check();
  await page.getByLabel(/Adatkezelesi Tajekoztatot|Adatkezelési Tájékoztatót/).check();
  await page.getByRole("button", { name: /Megrendeles elkuldese|Megrendelés elküldése/ }).click();
  await expect(phone).toBeVisible();
  await expect(phone).toHaveAttribute("aria-invalid", "true");
  await expect(page.getByText("A szerver nem fogadja el ezt a telefonszámot.").first()).toBeVisible();
  await expect(phone).toHaveValue("+36301234567");
  await phone.fill("+36307654321");
  await expect(phone).toHaveAttribute("aria-invalid", "false");
});

test("login attaches structured backend errors to the input", async ({ page }) => {
  await mockEmptyCartApi(page);
  await page.route("http://localhost:5097/api/auth/login", async (route) => {
    await route.fulfill({ status: 400, json: {
      detail: "Ellenőrizd a megadott adatokat.",
      errors: { email: ["A megadott email cím nem fogadható el."] },
    } });
  });
  await page.goto("/bejelentkezes");
  const email = page.getByLabel(/Email cím|Email cim/);
  await email.fill("teszt@example.com");
  await page.locator("#login-password").fill("Password123!");
  await page.getByRole("button", { name: "Bejelentkezés", exact: true }).click();
  await expect(email).toHaveAttribute("aria-invalid", "true");
  await expect(page.getByText("A megadott email cím nem fogadható el.").first()).toBeVisible();
});

test("invalid error payload preserves a useful fallback", async ({ page }) => {
  await mockEmptyCartApi(page);
  await page.route("http://localhost:5097/api/auth/forgot-password", async (route) => {
    await route.fulfill({ status: 400, json: { errors: { email: "not-an-array" }, detail: "Próbáld újra később." } });
  });
  await page.goto("/elfelejtett-jelszo");
  await page.getByLabel(/Email cim|Email cím/).fill("teszt@example.com");
  await page.getByRole("button", { name: /Jelszó-visszaállító link küldése/ }).click();
  await expect(page.getByRole("alert")).toContainText("Próbáld újra később.");
});
