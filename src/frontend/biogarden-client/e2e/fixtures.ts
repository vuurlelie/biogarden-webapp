import type { Page, Route } from "@playwright/test";

export const apiBaseUrl = "http://localhost:5097";

type OrderStatus = 0 | 1 | 2 | 3 | 4;
type PaymentStatus = 0 | 1 | 2 | 3 | 4 | 5 | 6;

const product = {
  id: 1,
  name: "Teszt levendula tea",
  price: 1800,
  category: 2,
  image: "uploads/products/defaultimg.png",
  unit: 1,
  unitAmount: 50,
  unitAmountMeasure: 4,
  available: true,
  deliveryAvailable: true,
};

const relatedProduct = {
  id: 2,
  name: "Teszt kakukkfu tea",
  price: 1600,
  category: 2,
  image: "uploads/products/defaultimg.png",
  unit: 1,
  unitAmount: 40,
  unitAmountMeasure: 4,
  available: true,
  deliveryAvailable: true,
};

const cartItem = {
  id: "cart-item-1",
  productId: product.id,
  productName: product.name,
  productDescription: "Illatos teszt termek a rendelesi folyamat ellenorzesere.",
  unitPrice: product.price,
  image: product.image,
  unit: product.unit,
  quantity: 1,
  stockQuantity: 8,
  reservedQuantityExcludingCurrentCart: 0,
  availableQuantityForCart: 8,
  available: true,
  deliveryAvailable: true,
  lineTotal: product.price,
};

export const successfulOrder = {
  id: "11111111-1111-1111-1111-111111111111",
  orderNumber: "BG-2026-0001",
  userId: null,
  addressId: null,
  customerName: "Teszt Elek",
  customerEmail: "teszt@example.com",
  customerPhone: "+36301234567",
  postalCode: "",
  city: "",
  street: "",
  houseNumber: "",
  building: null,
  floor: null,
  door: null,
  doorbell: null,
  billingCompanyName: null,
  billingTaxNumber: null,
  customerType: 0,
  printedInvoiceRequested: false,
  deliveryPostalCode: null,
  deliveryCity: null,
  deliveryStreet: null,
  deliveryHouseNumber: null,
  deliveryBuilding: null,
  deliveryFloor: null,
  deliveryDoor: null,
  deliveryDoorbell: null,
  createdAtUtc: "2026-07-22T10:00:00Z",
  confirmedAtUtc: null,
  cancelledAtUtc: null,
  deletedAtUtc: null,
  totalPrice: product.price,
  status: 0 as OrderStatus,
  paymentStatus: 0 as PaymentStatus,
  fulfillmentMethod: 0,
  sumUpCheckoutId: null,
  sumUpTransactionId: null,
  cancellationReason: null,
  items: [
    {
      productId: product.id,
      productName: product.name,
      unitPrice: product.price,
      unit: product.unit,
      quantity: 1,
      lineTotal: product.price,
    },
  ],
};

export async function mockPublicShopApi(page: Page) {
  let items: Array<typeof cartItem> = [];

  await page.route(`${apiBaseUrl}/uploads/**`, async (route) => {
    await route.fulfill({ status: 204 });
  });

  await page.route(`${apiBaseUrl}/api/products`, async (route) => {
    await route.fulfill({ json: [product, relatedProduct] });
  });

  await page.route(`${apiBaseUrl}/api/products/1`, async (route) => {
    await route.fulfill({
      json: {
        ...product,
        description: "Nyugtato gyogytea teszt leirassal.",
        stockQuantity: 8,
      },
    });
  });

  await page.route(`${apiBaseUrl}/api/cart/products/1/availability`, async (route) => {
    await route.fulfill({ json: { availableQuantity: 8 } });
  });

  await page.route(`${apiBaseUrl}/api/cart`, async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: createCart(items) });
      return;
    }

    await route.fallback();
  });

  await page.route(`${apiBaseUrl}/api/cart/items`, async (route) => {
    items = [cartItem];
    await route.fulfill({ json: createCart(items) });
  });
}

export async function mockCheckoutApi(page: Page, onlinePayment = false) {
  await page.route(`${apiBaseUrl}/api/cart`, async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: createCart([cartItem]) });
      return;
    }

    await route.fallback();
  });

  await page.route(`${apiBaseUrl}/api/cart/validate`, async (route) => {
    await route.fulfill({ json: { isValid: true, issues: [] } });
  });

  await page.route(`${apiBaseUrl}/api/orders`, async (route) => {
    await route.fulfill({ json: successfulOrder });
  });

  await page.route(`${apiBaseUrl}/api/orders/${successfulOrder.id}/sumup-checkout`, async (route) => {
    await route.fulfill({
      status: onlinePayment ? 500 : 200,
      json: onlinePayment
        ? { detail: "SumUp checkout is not enabled." }
        : {
            orderId: successfulOrder.id,
            checkoutId: "sumup-checkout-1",
            url: "https://example.com/sumup-checkout",
          },
    });
  });
}

export async function mockEmptyCartApi(page: Page) {
  await page.route(`${apiBaseUrl}/api/cart`, async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: createCart([]) });
      return;
    }

    await route.fallback();
  });
}

export async function mockCheckoutValidationIssueApi(page: Page, issueMessage: string) {
  await page.route(`${apiBaseUrl}/api/cart`, async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: createCart([cartItem]) });
      return;
    }

    await route.fallback();
  });

  await page.route(`${apiBaseUrl}/api/cart/validate`, async (route) => {
    await route.fulfill({
      json: {
        isValid: false,
        issues: [
          {
            productId: product.id,
            productName: product.name,
            message: issueMessage,
          },
        ],
      },
    });
  });
}

export async function mockCancelledPaymentRetryApi(page: Page) {
  await page.route(`${apiBaseUrl}/api/cart`, async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: createCart([]) });
      return;
    }

    await route.fallback();
  });

  await page.route(`${apiBaseUrl}/api/orders/${successfulOrder.id}/sumup-checkout`, async (route) => {
    await route.fulfill({
      status: 500,
      json: { detail: "SumUp checkout is not enabled." },
    });
  });
}

export async function mockAdminOrdersApi(page: Page) {
  const adminOrder = {
    ...successfulOrder,
    userId: "22222222-2222-2222-2222-222222222222",
    customerType: 1,
    billingCompanyName: "Teszt Kft.",
    billingTaxNumber: "12345678-1-12",
    printedInvoiceRequested: true,
    postalCode: "1111",
    city: "Budapest",
    street: "Teszt utca",
    houseNumber: "1",
  };

  await page.route(`${apiBaseUrl}/api/cart`, async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: createCart([]) });
      return;
    }

    await route.fallback();
  });

  await page.route(`${apiBaseUrl}/api/admin/orders`, async (route) => {
    await route.fulfill({ json: [adminOrder] });
  });

  await page.route(`${apiBaseUrl}/api/admin/orders/${adminOrder.id}`, async (route) => {
    await route.fulfill({
      json: {
        ...adminOrder,
        status: 1,
        paymentStatus: 1,
      },
    });
  });
}

export async function signInAsAdmin(page: Page) {
  await page.addInitScript(() => {
    const tokenPayload = btoa(JSON.stringify({
      exp: Math.floor(Date.now() / 1000) + 3600,
    }));

    localStorage.setItem("biogarden_auth", JSON.stringify({
      userId: "22222222-2222-2222-2222-222222222222",
      email: "admin@example.com",
      roles: ["Admin"],
      token: `test.${tokenPayload}.token`,
    }));
  });
}

function createCart(items: Array<typeof cartItem>) {
  return {
    id: "33333333-3333-3333-3333-333333333333",
    userId: null,
    sessionId: "test-session",
    createdAtUtc: "2026-07-22T10:00:00Z",
    lastActivityAtUtc: "2026-07-22T10:00:00Z",
    expiresAtUtc: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
    isCheckedOut: false,
    totalItemCount: items.reduce((sum, item) => sum + item.quantity, 0),
    totalPrice: items.reduce((sum, item) => sum + item.lineTotal, 0),
    items,
  };
}

export async function expectNoUnhandledApiCall(route: Route) {
  throw new Error(`Unhandled API call: ${route.request().method()} ${route.request().url()}`);
}
