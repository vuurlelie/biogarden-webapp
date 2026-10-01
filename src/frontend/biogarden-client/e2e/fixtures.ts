import type { Page, Route } from "@playwright/test";

export const apiBaseUrl = "http://localhost:5097";

type OrderStatus = 0 | 1 | 2 | 3 | 4;
type PaymentStatus = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export const product = {
  version: "AAAAAAAAAAE=",
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

const pageTwoProduct = {
  id: 13,
  name: "Masodik oldali szorp",
  price: 2200,
  category: 3,
  image: "uploads/products/defaultimg.png",
  unit: 1,
  unitAmount: 250,
  unitAmountMeasure: 3,
  available: true,
  deliveryAvailable: false,
};

const pagedProducts = [
  product,
  relatedProduct,
  ...Array.from({ length: 10 }, (_, index) => ({
    ...product,
    id: index + 3,
    name: `Teszt termek ${index + 3}`,
  })),
];

const pageOnePost = {
  id: 1,
  title: "Elso teszt bejegyzes",
  category: 0,
  article: "Hasznos tudastar tartalom elso oldalon.",
  image: "uploads/posts/defaultimg.png",
  publishedAt: "2026-07-20T10:00:00Z",
  draft: false,
};

const pagedPosts = [
  pageOnePost,
  ...Array.from({ length: 3 }, (_, index) => ({
    ...pageOnePost,
    id: index + 2,
    title: `Teszt bejegyzes ${index + 2}`,
  })),
];

const pageTwoPost = {
  id: 5,
  title: "Masodik oldali bejegyzes",
  category: 1,
  article: "Hasznos tudastar tartalom masodik oldalon.",
  image: "uploads/posts/defaultimg.png",
  publishedAt: "2025-05-19T10:00:00Z",
  draft: false,
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
  version: 'AAAAAAAAAAE=',
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
  acceptedTermsVersion: "aszf-2026-07-22",
  acceptedPrivacyPolicyVersion: "privacy-2026-07-22",
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
  subtotal: product.price,
  deliveryFee: 0,
  paymentFee: 0,
  currency: "HUF",
  paymentMethod: 1,
  glsDeliveryPointId: null,
  glsDeliveryPointName: null,
  glsDeliveryPointAddress: null,
  glsDeliveryPointCountryCode: null,
  status: 0 as OrderStatus,
  paymentStatus: 0 as PaymentStatus,
  fulfillmentMethod: 0,
  sumUpCheckoutId: null,
  sumUpTransactionId: null,
  cancellationReason: null,
  withdrawalRequests: [],
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

export async function mockCatalogPaginationApi(page: Page) {
  await page.route(`${apiBaseUrl}/uploads/**`, async (route) => {
    await route.fulfill({ status: 204 });
  });

  await page.route(
    (url) => url.origin === apiBaseUrl && url.pathname === "/api/cart",
    async (route) => {
      if (route.request().method() === "GET") {
        await route.fulfill({ json: createCart([]) });
        return;
      }

      await route.fallback();
    },
  );

  await page.route(
    (url) => url.origin === apiBaseUrl && url.pathname === "/api/products",
    async (route) => {
      const requestUrl = new URL(route.request().url());
      const pageNumber = Number(requestUrl.searchParams.get("page") ?? "1");
      const items = pageNumber === 2 ? [pageTwoProduct] : pagedProducts;

      await route.fulfill({
        json: createPagedResult(items, pageNumber, 12, 13),
      });
    },
  );

  await page.route(
    (url) => url.origin === apiBaseUrl && url.pathname === "/api/posts",
    async (route) => {
      const requestUrl = new URL(route.request().url());
      const pageNumber = Number(requestUrl.searchParams.get("page") ?? "1");
      const pageSize = Number(requestUrl.searchParams.get("pageSize") ?? "12");
      const items = pageSize >= 60
        ? [...pagedPosts, pageTwoPost]
        : pageNumber === 2 ? [pageTwoPost] : pagedPosts;

      await route.fulfill({
        json: createPagedResult(items, pageNumber, 4, 5),
      });
    },
  );
}

export async function mockKnowledgePostsApi(page: Page) {
  await page.route(`${apiBaseUrl}/uploads/**`, async (route) => {
    await route.fulfill({ status: 204 });
  });

  await page.route(
    (url) => url.origin === apiBaseUrl && url.pathname === "/api/cart",
    async (route) => {
      if (route.request().method() === "GET") {
        await route.fulfill({ json: createCart([]) });
        return;
      }

      await route.fallback();
    },
  );

  await page.route(
    (url) => url.origin === apiBaseUrl && url.pathname === "/api/posts",
    async (route) => {
      const requestUrl = new URL(route.request().url());
      const pageSize = Number(requestUrl.searchParams.get("pageSize") ?? "12");

      await route.fulfill({
        json: createPagedResult(
          pageSize >= 60 ? [...pagedPosts, pageTwoPost] : pagedPosts,
          1,
          pageSize >= 60 ? 60 : 4,
          pageSize >= 60 ? pagedPosts.length + 1 : pagedPosts.length,
        ),
      });
    },
  );

  await page.route(`${apiBaseUrl}/api/posts/1`, async (route) => {
    await route.fulfill({
      json: pageOnePost,
    });
  });
}

export async function mockPublicShopApi(page: Page) {
  let items: Array<typeof cartItem> = [];

  await page.route(`${apiBaseUrl}/uploads/**`, async (route) => {
    await route.fulfill({ status: 204 });
  });

  await page.route(
    (url) => url.origin === apiBaseUrl && url.pathname === "/api/products",
    async (route) => {
      await route.fulfill({
        json: createPagedResult([product, relatedProduct], 1, 60, 2),
      });
    },
  );

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

export async function mockCheckoutApi(
  page: Page,
  onlinePayment = false,
  onCreateOrder?: (payload: Record<string, unknown>) => void,
) {
  let createdPaymentMethod: number | null = null;
  await page.route(`${apiBaseUrl}/api/orders/checkout-recovery/*`, route => createdPaymentMethod === null
    ? route.fulfill({ status: 404 })
    : route.fulfill({ json: { ...successfulOrder, paymentMethod: createdPaymentMethod } }));
  await page.route(`${apiBaseUrl}/api/orders/${successfulOrder.id}/receipt`, route => route.fulfill({
    json: { ...successfulOrder, paymentMethod: onlinePayment ? 0 : 1 },
  }));
  await mockCheckoutDraftApi(page);
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
    createdPaymentMethod = route.request().postDataJSON().paymentMethod;
    if (route.request().method() === "POST" && onCreateOrder) {
      onCreateOrder(route.request().postDataJSON() as Record<string, unknown>);
    }

    await route.fulfill({ status: 201, json: { ...successfulOrder, paymentMethod: route.request().postDataJSON().paymentMethod } });
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
  await mockCheckoutDraftApi(page);
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
  await page.route(`${apiBaseUrl}/api/orders/${successfulOrder.id}/receipt`, route => route.fulfill({
    json: { ...successfulOrder, paymentMethod: 0 },
  }));
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

export async function mockSumUpReturnStatusApi(
  page: Page,
  paymentStatus: PaymentStatus,
) {
  await page.route(`${apiBaseUrl}/api/orders/${successfulOrder.id}/receipt`, route => route.fulfill({ status: 404, json: { detail: "Owning session unavailable" } }));
  await page.route(`${apiBaseUrl}/api/cart`, async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: createCart([]) });
      return;
    }

    await route.fallback();
  });

  await page.route(
    (url) =>
      url.origin === apiBaseUrl &&
      url.pathname === `/api/orders/${successfulOrder.id}/payment-status`,
    async (route) => {
      await route.fulfill({
        json: {
          orderId: successfulOrder.id,
          orderNumber: successfulOrder.orderNumber,
          status: paymentStatus === 2 ? 1 : successfulOrder.status,
          paymentStatus,
          confirmedAtUtc: paymentStatus === 2 ? "2026-07-22T10:05:00Z" : null,
        },
      });
    },
  );
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
        paymentStatus: 2,
      },
    });
  });
}

export async function mockAccountOrdersApi(page: Page) {
  const accountOrder = {
    ...successfulOrder,
    userId: "44444444-4444-4444-4444-444444444444",
    status: 1 as OrderStatus,
    paymentStatus: 2 as PaymentStatus,
    confirmedAtUtc: "2026-07-22T10:05:00Z",
  };

  await page.route(`${apiBaseUrl}/api/cart`, async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: createCart([]) });
      return;
    }

    await route.fallback();
  });

  await page.route(`${apiBaseUrl}/api/orders`, async (route) => {
    await route.fulfill({ json: [accountOrder] });
  });

  await page.route(`${apiBaseUrl}/api/orders/${accountOrder.id}`, async (route) => {
    await route.fulfill({ json: accountOrder });
  });
}

export async function signInAsCustomer(page: Page) {
  await page.addInitScript(() => {
    const tokenPayload = btoa(JSON.stringify({
      exp: Math.floor(Date.now() / 1000) + 3600,
    }));

    localStorage.setItem("biogarden_auth", JSON.stringify({
      userId: "44444444-4444-4444-4444-444444444444",
      email: "customer@example.com",
      roles: [],
      token: `test.${tokenPayload}.token`,
    }));
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

export async function mockCheckoutDraftApi(page: Page) {
  await page.route(`${apiBaseUrl}/api/orders/checkout-methods`, route => route.fulfill({ json: [
    { fulfillmentMethod: 0, deliveryFee: 0, paymentMethods: [0, 1] },
    { fulfillmentMethod: 1, deliveryFee: 2740, paymentMethods: [0, 2] },
    { fulfillmentMethod: 2, deliveryFee: 2090, paymentMethods: [0, 2] },
    { fulfillmentMethod: 3, deliveryFee: 2590, paymentMethods: [0, 2] },
  ] }));
  await page.route(`${apiBaseUrl}/api/orders/quote`, route => route.fulfill({ json: checkoutQuote(route.request().postDataJSON()) }));
  let stored: { version: string | null; expiresAtUtc: string | null; fields: unknown } = { version: null, expiresAtUtc: null, fields: null };
  let revision = 0;
  await page.route(`${apiBaseUrl}/api/checkout-drafts/**`, async route => {
    if (route.request().method() === "PUT") {
      const request = route.request().postDataJSON();
      if (request.version !== stored.version) {
        await route.fulfill({ status: 409, json: { detail: "A piszkozat megváltozott vagy lejárt." } });
        return;
      }
      stored = { version: `revision-${++revision}`, expiresAtUtc: new Date(Date.now() + 1_800_000).toISOString(), fields: request.fields };
    } else if (route.request().method() === "DELETE") {
      stored = { version: null, expiresAtUtc: null, fields: null };
      await route.fulfill({ status: 204 });
      return;
    }
    await route.fulfill({ json: stored });
  });
}

export function checkoutQuote(request: { fulfillmentMethod: number; paymentMethod: number; glsDeliveryPointId?: string | null }) {
  const deliveryFee = [0, 2740, 2090, 2590][request.fulfillmentMethod];
  const paymentFee = request.paymentMethod === 2 ? 600 : 0;
  return { cartId: "33333333-3333-3333-3333-333333333333", ...request, currency: "HUF", subtotal: product.price,
    deliveryFee, paymentFee, totalPrice: product.price + deliveryFee + paymentFee, version: "A".repeat(64), items: successfulOrder.items,
    deliveryPoint: request.glsDeliveryPointId ? { id: request.glsDeliveryPointId, type: request.fulfillmentMethod,
      name: request.fulfillmentMethod === 2 ? "Verified locker" : "Verified shop", countryCode: "HU", postalCode: "8200", city: "Veszprém", address: "GLS utca 1.", acceptsCash: false, acceptsCard: true } : null };
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

function createPagedResult<T>(items: T[], page: number, pageSize: number, totalCount: number) {
  return {
    items,
    page,
    pageSize,
    totalCount,
    totalPages: Math.max(1, Math.ceil(totalCount / pageSize)),
  };
}

export async function expectNoUnhandledApiCall(route: Route) {
  throw new Error(`Unhandled API call: ${route.request().method()} ${route.request().url()}`);
}
