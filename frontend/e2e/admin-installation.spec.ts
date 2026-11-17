import { expect, test } from "@playwright/test";

const apiOrigin = "https://localhost:44300";
const corsHeaders = {
  "access-control-allow-origin": "http://127.0.0.1:5175",
  "access-control-allow-credentials": "true",
  "access-control-allow-methods": "GET, POST, OPTIONS",
  "access-control-allow-headers": "content-type, x-csrf-token, x-onecart-setup-token"
};

const adminUser = {
  id: "00000000-0000-4000-8000-000000000001",
  email: "owner@example.test",
  roles: ["SystemAdmin"],
  mustChangePassword: false,
  twoFactorEnabled: false,
  mfaRequired: false,
  lockoutEnd: null
};

const initialAdminProduct = {
  id: "00000000-0000-4000-8000-000000000010",
  name: "Sample product",
  slug: "sample-product",
  description: "A product for the administrator workflow check.",
  currencyCode: "BDT",
  brandName: "Pretty Shop",
  storefrontHeadline: "Your trust, Your Love",
  storefrontSubtitle: "",
  isPublished: true,
  variants: [{
    id: "00000000-0000-4000-8000-000000000011",
    sku: "SAMPLE-ONE",
    displayName: "Single pack",
    price: 1200,
    currencyCode: "BDT",
    stockQuantity: 5,
    isActive: true
  }],
  attributeGroups: [],
  flashSale: { enabled: false, label: "", discountType: "percentage", discountValue: 0, startsAtUtc: null, endsAtUtc: null },
  socialSharingEnabled: true,
  whatsAppEnabled: false,
  whatsAppPhoneNumber: "",
  whatsAppPrefillMessage: "Hello, I have a question about this product.",
  images: [],
  trustItems: [],
  catalogTranslations: {},
  storefrontTranslations: {}
};

type StorefrontSettingsPayload = {
  aboutUs: string;
  usageSteps: Array<{ title: string; description: string }>;
  reviews: Array<{ displayName: string; rating: number; title: string; content: string; isPublished: boolean; displayOrder: number }>;
};

function mockSystemAdminApi(
  onProductSave: (product: typeof initialAdminProduct) => typeof initialAdminProduct,
  onUserCreate: (user: { email: string; temporaryPassword: string; roleNames: string[] }) => unknown = () => adminUser,
  onSettingsSave: (settings: StorefrontSettingsPayload) => void = () => undefined,
  onOrderStatus: (status: string) => void = () => undefined,
  userOverride: typeof adminUser = adminUser
) {
  let authenticated = false;
  let orderStatus = "Pending";
  return async (route: import("@playwright/test").Route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    const respond = (body: unknown, status = 200) => route.fulfill({
      status,
      headers: corsHeaders,
      contentType: "application/json",
      body: JSON.stringify(body)
    });

    if (method === "OPTIONS") {
      await route.fulfill({ status: 204, headers: corsHeaders });
    } else if (path === "/api/v1/admin/csrf") {
      await respond({ requestToken: "csrf-test-token" });
    } else if (path === "/api/v1/admin/me") {
      await respond(authenticated ? userOverride : { title: "Unauthorized" }, authenticated ? 200 : 401);
    } else if (path === "/api/v1/admin/login" && method === "POST") {
      authenticated = true;
      await respond({ requiresTwoFactor: false });
    } else if (path === "/api/v1/admin/roles") {
      await respond(["SystemAdmin", "CatalogManager", "OrderManager", "StoreManager"].map((name, index) => ({ id: `role-${index}`, name })));
    } else if (path === "/api/v1/admin/users" && method === "POST") {
      await respond(onUserCreate(request.postDataJSON() as { email: string; temporaryPassword: string; roleNames: string[] }), 201);
    } else if (path === "/api/v1/admin/users") {
      await respond([adminUser]);
    } else if (path === "/api/v1/admin/audit") {
      await respond([]);
    } else if (path === "/api/v1/admin/security/mfa-policy") {
      await respond({ required: false });
    } else if (path === "/api/v1/admin/catalog/currency") {
      await respond({ currencyCode: "BDT", canChange: false });
    } else if (path === "/api/v1/admin/orders" && method === "GET") {
      await respond([{
        id: "00000000-0000-4000-8000-000000000030", orderNumber: "OC-TEST-1001", status: orderStatus,
        customerName: "Test shopper", phoneNumber: "01800000000", currencyCode: "BDT", total: 1200,
        createdAtUtc: "2026-01-01T00:00:00Z", itemCount: 1
      }]);
    } else if (path === "/api/v1/admin/orders/summary") {
      await respond([{ currencyCode: "BDT", statuses: [{ status: orderStatus, orderCount: 1, orderTotal: 1200 }] }]);
    } else if (/^\/api\/v1\/admin\/orders\/[0-9a-f-]+$/.test(path)) {
      await respond({
        id: "00000000-0000-4000-8000-000000000030", orderNumber: "OC-TEST-1001", status: orderStatus,
        customerName: "Test shopper", phoneNumber: "01800000000", currencyCode: "BDT", total: 1200,
        createdAtUtc: "2026-01-01T00:00:00Z", updatedAtUtc: "2026-01-01T00:00:00Z", itemCount: 1,
        deliveryAddress: "1 Example Road", lines: [{ productName: "Sample product", sku: "SAMPLE-ONE", variantName: "Single pack", selectedOptions: [], quantity: 1, unitPrice: 1200 }]
      });
    } else if (path.endsWith("/status") && path.includes("/api/v1/admin/orders/") && method === "PUT") {
      orderStatus = (request.postDataJSON() as { status: string }).status;
      onOrderStatus(orderStatus);
      await respond({
        id: "00000000-0000-4000-8000-000000000030", orderNumber: "OC-TEST-1001", status: orderStatus,
        customerName: "Test shopper", phoneNumber: "01800000000", currencyCode: "BDT", total: 1200,
        createdAtUtc: "2026-01-01T00:00:00Z", updatedAtUtc: "2026-01-01T00:01:00Z", itemCount: 1,
        deliveryAddress: "1 Example Road", lines: [{ productName: "Sample product", sku: "SAMPLE-ONE", variantName: "Single pack", selectedOptions: [], quantity: 1, unitPrice: 1200 }]
      });
    } else if (path === "/api/v1/admin/catalog/product" && method === "GET") {
      await respond(initialAdminProduct);
    } else if (path === "/api/v1/admin/catalog/product" && method === "PUT") {
      const saved = onProductSave(request.postDataJSON() as typeof initialAdminProduct);
      await respond(saved);
    } else if (path === "/api/v1/admin/catalog/product" && method === "DELETE") {
      await route.fulfill({ status: 204, headers: corsHeaders });
    } else if (path === "/api/v1/admin/catalog/product/images" && method === "POST") {
      await respond({ id: "uploaded-image-1", url: "/api/v1/store/product/images/uploaded-image-1", mediaKind: "product", altText: "Uploaded product photo", displayOrder: 1 }, 201);
    } else if (path === "/api/v1/admin/catalog/product/images/uploaded-image-1" && method === "PUT") {
      await respond({ id: "uploaded-image-1", url: "/api/v1/store/product/images/uploaded-image-1", mediaKind: "product", ...(request.postDataJSON() as { altText: string; displayOrder: number }) });
    } else if (path === "/api/v1/admin/catalog/product/images/uploaded-image-1" && method === "DELETE") {
      await route.fulfill({ status: 204, headers: corsHeaders });
    } else if (path === "/api/v1/admin/catalog/product/images/uploaded-image-1") {
      await route.fulfill({ status: 200, headers: corsHeaders, contentType: "image/png", body: Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j5ZkAAAAASUVORK5CYII=", "base64") });
    } else if (path === "/api/v1/admin/storefront/settings" && method === "PUT") {
      const settings = request.postDataJSON() as StorefrontSettingsPayload;
      onSettingsSave(settings);
      await respond({ ...settings, reviews: settings.reviews });
    } else if (path === "/api/v1/admin/storefront/settings") {
      await respond({
        brandName: "Pretty Shop", storefrontHeadline: "Your trust, Your Love", aboutUs: "",
        benefits: [], usageSteps: [], reviews: [], storefrontSubtitle: "",
        flashSale: initialAdminProduct.flashSale, socialSharingEnabled: true,
        whatsAppEnabled: false, whatsAppPhoneNumber: "", whatsAppPrefillMessage: "",
        brandLogo: null, productImages: [], trustItems: [], storefrontTranslations: {},
        theme: { primaryColor: "#242020", accentColor: "#c17b88", textColor: "#282326", mutedTextColor: "#777078", backgroundColor: "#fbf8f8", surfaceColor: "#ffffff", borderColor: "#eee5e8", productTitleFontSizePx: 30, detailsFontSizePx: 13 }
      });
    } else if (path === "/api/v1/admin/customers") {
      await respond({ items: [], totalCount: 0, page: 1, pageSize: 25 });
    } else {
      await respond({ title: "Not found" }, 404);
    }
  };
}

test("keeps the administrator sign-in controls distinct and ordered", async ({ page }) => {
  await page.route(`${apiOrigin}/api/**`, async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path === "/api/v1/admin/csrf") {
      await route.fulfill({ status: 200, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ requestToken: "csrf-test-token" }) });
      return;
    }
    if (path === "/api/v1/admin/me") {
      await route.fulfill({ status: 401, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ title: "Unauthorized" }) });
      return;
    }
    await route.fulfill({ status: 404, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ title: "Not found" }) });
  });

  await page.goto("/admin");
  await expect(page.getByRole("heading", { name: "Administrator sign in" })).toBeVisible();
  const password = page.getByLabel("Password");
  const signIn = page.getByRole("button", { name: "Sign in" });
  await expect(password).toBeVisible();
  await expect(signIn).toBeVisible();
  const passwordBox = await password.boundingBox();
  const signInBox = await signIn.boundingBox();
  expect(passwordBox).not.toBeNull();
  expect(signInBox).not.toBeNull();
  expect(signInBox!.y).toBeGreaterThanOrEqual(passwordBox!.y + passwordBox!.height);
});

test("completes the one-time setup form through the configured token header", async ({ page }) => {
  let setupRequest: { email?: string; password?: string; confirmPassword?: string } | undefined;
  let setupTokenHeader = "";

  await page.route(`${apiOrigin}/api/**`, async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === "/api/v1/setup/status" && request.method() === "GET") {
      await route.fulfill({ status: 200, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ available: true }) });
      return;
    }
    if (path === "/api/v1/setup/system-admin" && request.method() === "POST") {
      setupRequest = request.postDataJSON() as typeof setupRequest;
      setupTokenHeader = request.headers()["x-onecart-setup-token"] ?? "";
      await route.fulfill({ status: 200, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ role: "SystemAdmin" }) });
      return;
    }
    await route.fulfill({ status: 404, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ title: "Not found" }) });
  });

  await page.goto("/install");
  await expect(page.getByRole("heading", { name: "Initial store setup" })).toBeVisible();
  await page.getByLabel("Administrator email").fill("owner@example.test");
  await page.getByLabel("Password", { exact: true }).fill("OneCart-Test-Admin-2026!");
  await page.getByLabel("Confirm password").fill("OneCart-Test-Admin-2026!");
  await page.getByLabel("One-time setup token").fill("one-time-test-token");
  await page.getByRole("button", { name: "Create System Admin" }).click();

  await expect(page.getByRole("status")).toContainText("Your System Admin account is ready");
  expect(setupTokenHeader).toBe("one-time-test-token");
  expect(setupRequest).toEqual({
    email: "owner@example.test",
    password: "OneCart-Test-Admin-2026!",
    confirmPassword: "OneCart-Test-Admin-2026!"
  });
});

test("installs a fresh store with database, language, currency, and first SystemAdmin", async ({ page }) => {
  let submitted: Record<string, unknown> | undefined;
  await page.route(`${apiOrigin}/api/**`, async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === "/api/v1/setup/status") {
      await route.fulfill({ status: 200, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ available: true, requiresDatabase: true }) });
    } else if (path === "/api/v1/setup/database/test") {
      expect(route.request().headers()["x-onecart-setup-token"]).toBe("local-setup-token");
      await route.fulfill({ status: 200, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ connected: true }) });
    } else if (path === "/api/v1/setup/install") {
      submitted = route.request().postDataJSON() as Record<string, unknown>;
      await route.fulfill({ status: 201, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ role: "SystemAdmin" }) });
    } else {
      await route.fulfill({ status: 404, headers: corsHeaders });
    }
  });
  await page.goto("/install");
  await page.getByLabel("Database user").fill("store_owner");
  await page.getByLabel("Database password").fill("database-secret");
  await page.getByLabel("One-time setup token").fill("local-setup-token");
  await page.getByRole("button", { name: "Test database connection" }).click();
  await expect(page.getByText("Database connection succeeded.")).toBeVisible();
  await page.getByLabel("Administrator email").fill("owner@example.test");
  await page.getByLabel("Password", { exact: true }).fill("OneCart-Test-Admin-2026!");
  await page.getByLabel("Confirm password").fill("OneCart-Test-Admin-2026!");
  await page.getByLabel("Default storefront language").selectOption("bn");
  await page.getByLabel("Store currency").selectOption("BDT");
  await page.getByRole("button", { name: "Install store" }).click();
  await expect(page.getByRole("status")).toContainText("Your System Admin account is ready");
  expect(submitted).toMatchObject({
    email: "owner@example.test", defaultLanguageCode: "bn", currencyCode: "BDT",
    database: { provider: "PostgreSql", host: "localhost", databaseName: "onecart", userName: "store_owner", password: "database-secret" }
  });
});

test("adds a second variant, edits its stock, and saves them together", async ({ page }) => {
  let savedVariants: typeof initialAdminProduct.variants = [];
  let savedCatalog: typeof initialAdminProduct | undefined;
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi((product) => {
    savedVariants = product.variants;
    savedCatalog = product;
    return product;
  }));

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Overview" })).toBeVisible();
  await page.getByRole("button", { name: "Catalog", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Product catalog" })).toBeVisible();
  await expect(page.getByLabel("Store currency")).toHaveValue("BDT");
  await expect(page.getByLabel("Store currency").locator('option[value="EUR"]')).toHaveText("EUR — Euro");

  const sectionNav = page.getByRole("navigation", { name: "Product editor sections" });
  await sectionNav.getByRole("button", { name: "Variants" }).click();
  const variants = page.locator("#product-variants");
  await variants.getByRole("button", { name: "Add another variant" }).click();
  await variants.getByLabel("SKU").nth(1).fill("SAMPLE-TWO");
  await variants.getByLabel("Display name / options").nth(1).fill("Double pack");
  await variants.getByLabel("Regular price (BDT)").nth(1).fill("2100");

  await sectionNav.getByRole("button", { name: "Inventory" }).click();
  await page.locator("#product-inventory").getByLabel("Stock quantity").nth(1).fill("18");
  await variants.getByRole("button", { name: "Save variant data" }).click();

  await expect(page.getByText("Section changes saved.")).toBeVisible();
  expect(savedVariants).toHaveLength(2);
  expect(savedVariants[1]).toMatchObject({ sku: "SAMPLE-TWO", displayName: "Double pack", price: 2100, stockQuantity: 18, currencyCode: "BDT" });

  await sectionNav.getByRole("button", { name: "Attributes" }).click();
  const attributes = page.locator("#product-attributes");
  await attributes.getByRole("button", { name: "Add attribute group" }).click();
  await attributes.getByLabel("Name").nth(0).fill("Package size");
  await attributes.getByRole("textbox", { name: "Choice" }).nth(0).fill("Large");
  await attributes.getByRole("button", { name: "Add choice" }).nth(0).click();
  await attributes.getByRole("textbox", { name: "Choice" }).nth(1).fill("Small");
  await attributes.getByRole("button", { name: "Add attribute group" }).click();
  await attributes.getByLabel("Name").nth(1).fill("Color");
  await attributes.getByRole("textbox", { name: "Choice" }).nth(2).fill("Rose");
  await attributes.getByRole("button", { name: "Save section" }).click();
  await expect(page.getByText("Section changes saved.")).toBeVisible();
  expect(savedCatalog?.attributeGroups).toHaveLength(2);
  expect(savedCatalog?.attributeGroups[0]).toMatchObject({ name: "Package size", options: [{ name: "Large" }, { name: "Small" }] });
  expect(savedCatalog?.attributeGroups[1]).toMatchObject({ name: "Color", options: [{ name: "Rose" }] });

  page.once("dialog", dialog => dialog.accept());
  await page.locator("#product-basics").getByRole("button", { name: "Delete product" }).click();
  await expect(page.getByText("Product deleted. You can now create a new product.")).toBeVisible();
});

test("offers a first-time store currency choice for a new product", async ({ page }) => {
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi(product => product));
  await page.route(`${apiOrigin}/api/v1/admin/catalog/currency`, route => route.fulfill({
    status: 200, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ currencyCode: null, canChange: true })
  }));
  await page.route(`${apiOrigin}/api/v1/admin/catalog/product`, route => route.fulfill({
    status: 404, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ title: "Not found" })
  }));
  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.getByRole("button", { name: "Catalog", exact: true }).click();
  const currency = page.getByLabel("Store currency");
  await expect(currency).toBeEnabled();
  await currency.selectOption("EUR");
  await expect(currency).toHaveValue("EUR");
  await page.getByRole("navigation", { name: "Product editor sections" }).getByRole("button", { name: "Variants" }).click();
  await expect(page.getByLabel("Regular price (EUR)").first()).toBeVisible();
});

test("requires manual variant repricing when changing an established store currency", async ({ page }) => {
  let saved: Record<string, unknown> | undefined;
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi(product => {
    saved = product as unknown as Record<string, unknown>;
    return product;
  }));
  await page.route(`${apiOrigin}/api/v1/admin/catalog/currency`, route => route.fulfill({
    status: 200, headers: corsHeaders, contentType: "application/json", body: JSON.stringify({ currencyCode: "BDT", canChange: true })
  }));
  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.getByRole("button", { name: "Catalog", exact: true }).click();
  await page.locator("#product-basics").getByRole("button", { name: "Edit", exact: true }).click();
  await page.getByLabel("Store currency").selectOption("EUR");
  await page.getByRole("navigation", { name: "Product editor sections" }).getByRole("button", { name: "Variants" }).click();
  await expect(page.locator("#product-variants").getByLabel("Regular price (EUR)")).toHaveValue("0");
  await page.locator("#product-variants").getByLabel("Regular price (EUR)").fill("15");
  await page.getByLabel("I entered new prices for every variant and priced option. The old flash sale is turned off.").check();
  await page.locator("#product-basics").getByRole("button", { name: "Save section" }).click();
  await expect(page.getByText("Section changes saved.")).toBeVisible();
  expect(saved?.currencyCode).toBe("EUR");
  expect(saved?.repricedVariantIds).toEqual([initialAdminProduct.variants[0].id]);
});

test("creates a limited Catalog Manager account from the System Admin workspace", async ({ page }) => {
  let createdUser: { email: string; temporaryPassword: string; roleNames: string[] } | undefined;
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi(
    (product) => product,
    (user) => {
      createdUser = user;
      return { ...adminUser, id: "00000000-0000-4000-8000-000000000099", email: user.email, roles: user.roleNames, mustChangePassword: true };
    }
  ));

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Overview" })).toBeVisible();
  await page.getByRole("navigation", { name: "Admin sections" }).getByRole("button", { name: "Accounts", exact: true }).click();

  const accounts = page.locator("#accounts");
  const createForm = accounts.locator("form.user-form");
  await createForm.getByLabel("Email address").fill("catalog@example.test");
  await createForm.getByLabel("Temporary password").fill("Temporary-Admin-2026!");
  await createForm.getByLabel("CatalogManager").check();
  await createForm.getByRole("button", { name: "Create user" }).click();

  await expect(page.getByRole("status").last()).toContainText("The user must change this temporary password");
  expect(createdUser).toEqual({ email: "catalog@example.test", temporaryPassword: "Temporary-Admin-2026!", roleNames: ["CatalogManager"] });
});

test("saves About Us, multiline usage, and an unpublished admin-managed review", async ({ page }) => {
  let savedSettings: StorefrontSettingsPayload | undefined;
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi(
    (product) => product,
    () => adminUser,
    (settings) => { savedSettings = settings; }
  ));

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.getByRole("navigation", { name: "Admin sections" }).getByRole("button", { name: "Storefront", exact: true }).click();
  const settings = page.getByRole("heading", { name: "Storefront settings" }).locator("xpath=../../..");

  await settings.getByLabel("About Us", { exact: true }).fill("A neighborhood shop serving local families.");
  await settings.getByLabel("Instructions", { exact: true }).fill("Use a small amount.\n\nRepeat as needed.");
  await settings.getByLabel("Bangla instructions").fill("অল্প পরিমাণ ব্যবহার করুন।");
  await settings.getByLabel("Spanish instructions").fill("Use una pequeña cantidad.");
  await settings.getByRole("button", { name: "Add review" }).click();
  await settings.getByLabel("Display name", { exact: true }).fill("Verified customer");
  await settings.getByLabel("Review title", { exact: true }).fill("A good fit");
  await settings.getByLabel("Review", { exact: true }).fill("This sample review remains unpublished until an administrator approves it.");
  await settings.getByRole("button", { name: "Save storefront settings" }).click();

  await expect(settings.getByRole("status")).toContainText("Storefront settings saved.");
  expect(savedSettings?.aboutUs).toBe("A neighborhood shop serving local families.");
  expect(savedSettings?.usageSteps).toMatchObject([{ title: "Instructions", description: "Use a small amount.\n\nRepeat as needed." }]);
  expect((savedSettings as (StorefrontSettingsPayload & { storefrontTranslations?: Record<string, Record<string, string>> }) | undefined)?.storefrontTranslations).toMatchObject({
    bn: { "usage.00000000-0000-4000-8000-000000000001.description": "অল্প পরিমাণ ব্যবহার করুন।" },
    es: { "usage.00000000-0000-4000-8000-000000000001.description": "Use una pequeña cantidad." }
  });
  expect(savedSettings?.reviews).toHaveLength(1);
  expect(savedSettings?.reviews[0]).toMatchObject({ displayName: "Verified customer", rating: 5, title: "A good fit", isPublished: false });
});

test("moves a Pending COD order into Processing from the admin order workspace", async ({ page }) => {
  let savedStatus = "";
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi(
    (product) => product,
    () => adminUser,
    () => undefined,
    (status) => { savedStatus = status; }
  ));

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.getByRole("navigation", { name: "Admin sections" }).getByRole("button", { name: "Orders", exact: true }).click();
  const orders = page.getByRole("heading", { name: "Orders" }).locator("xpath=../../..");
  await orders.getByRole("button", { name: /OC-TEST-1001/ }).click();
  await orders.locator(".order-status-editor select").selectOption("Processing");
  await orders.getByRole("button", { name: "Save status" }).click();

  await expect(orders.getByText("Order status saved.")).toBeVisible();
  expect(savedStatus).toBe("Processing");
  await expect(orders.locator(".order-details .eyebrow")).toHaveText("Processing");
});

test("keeps the order list and details readable on a phone", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi(product => product, () => adminUser));
  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.locator(".admin-menu-toggle span")).toBeHidden();
  await expect(page.locator(".admin-sign-out span")).toBeHidden();
  await expect(page.getByRole("button", { name: "Sign out" })).toBeVisible();
  await page.getByRole("button", { name: "Menu" }).click();
  await page.getByRole("navigation", { name: "Admin sections" }).getByRole("button", { name: "Orders", exact: true }).click();
  await expect(page.locator(".order-list-item")).toBeVisible();
  await expect(page.locator(".order-details")).toBeVisible();
  const orderWidth = await page.locator(".orders-layout").evaluate(element => element.getBoundingClientRect().width);
  expect(orderWidth).toBeLessThanOrEqual(375);
  const pageWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  expect(pageWidth).toBeLessThanOrEqual(375);
});

test("shows a local QR image during required authenticator enrollment", async ({ page }) => {
  let code = "";
  const user = { ...adminUser, mfaRequired: true, twoFactorEnabled: false };
  const mockApi = mockSystemAdminApi(product => product, undefined, undefined, undefined, user);
  await page.route(`${apiOrigin}/api/**`, async route => {
    if (new URL(route.request().url()).pathname !== "/api/v1/admin/mfa/setup" || route.request().method() === "OPTIONS") return mockApi(route);
    if (route.request().method() === "POST") {
      code = route.request().postDataJSON().code;
      user.twoFactorEnabled = true;
      return route.fulfill({ status: 204, headers: corsHeaders });
    }
    return route.fulfill({ headers: corsHeaders, json: { authenticatorKey: "JBSW Y3DP EHPK 3PXP", account: user.email } });
  });
  await page.goto("/admin");
  await page.getByLabel("Email address").fill(user.email);
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  const qr = page.getByRole("img", { name: "Scan to enroll OneCart in your authenticator app" });
  await expect(qr).toBeVisible();
  await expect(qr).toHaveAttribute("src", /^data:image\/png;base64,/);
  await expect(page.getByText("JBSW Y3DP EHPK 3PXP")).toHaveCount(0);
  await page.getByLabel("Authenticator code").fill("123456");
  await page.getByRole("button", { name: "Verify and enable MFA" }).click();
  await expect(page.getByRole("heading", { name: "Overview" })).toBeVisible();
  expect(code).toBe("123456");
});

test("requires an authenticator code after password sign-in when MFA is enrolled", async ({ page }) => {
  let authenticated = false;
  let submittedCode = "";
  const mfaUser = { ...adminUser, twoFactorEnabled: true };
  await page.route(`${apiOrigin}/api/**`, async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const respond = (body: unknown, status = 200) => route.fulfill({
      status, headers: corsHeaders, contentType: "application/json", body: JSON.stringify(body)
    });
    if (request.method() === "OPTIONS") return route.fulfill({ status: 204, headers: corsHeaders });
    if (path === "/api/v1/admin/csrf") return respond({ requestToken: "csrf-test-token" });
    if (path === "/api/v1/admin/me") return authenticated ? respond(mfaUser) : respond({ title: "Unauthorized" }, 401);
    if (path === "/api/v1/admin/login" && request.method() === "POST") return respond({ requiresTwoFactor: true });
    if (path === "/api/v1/admin/two-factor/login" && request.method() === "POST") {
      submittedCode = (request.postDataJSON() as { code: string }).code;
      authenticated = true;
      return respond({});
    }
    if (path === "/api/v1/admin/roles") return respond(["SystemAdmin", "CatalogManager", "OrderManager", "StoreManager"].map((name, index) => ({ id: `role-${index}`, name })));
    if (path === "/api/v1/admin/users") return respond([mfaUser]);
    if (path === "/api/v1/admin/audit") return respond([]);
    if (path === "/api/v1/admin/security/mfa-policy") return respond({ required: false });
    return respond({ title: "Not found" }, 404);
  });

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Authenticator verification" })).toBeVisible();
  await expect(page.getByLabel("Email address")).toHaveCount(0);
  await page.getByLabel("Authenticator code").fill("123456");
  await page.getByRole("button", { name: "Verify and sign in" }).click();
  await expect(page.getByRole("heading", { name: "Overview" })).toBeVisible();
  expect(submittedCode).toBe("123456");
});

test("sends generic password recovery requests for the entered administrator email", async ({ page }) => {
  let requestedEmail = "";
  await page.route(`${apiOrigin}/api/**`, async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const respond = (body: unknown, status = 200) => route.fulfill({
      status, headers: corsHeaders, contentType: "application/json", body: JSON.stringify(body)
    });
    if (request.method() === "OPTIONS") return route.fulfill({ status: 204, headers: corsHeaders });
    if (path === "/api/v1/admin/csrf") return respond({ requestToken: "csrf-test-token" });
    if (path === "/api/v1/admin/me") return respond({ title: "Unauthorized" }, 401);
    if (path === "/api/v1/admin/password/forgot" && request.method() === "POST") {
      requestedEmail = (request.postDataJSON() as { email: string }).email;
      return respond({});
    }
    return respond({ title: "Not found" }, 404);
  });

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByRole("button", { name: "Forgot password? Send reset link" }).click();
  await expect(page.locator(".login-card .login-feedback[role='status']")).toContainText("If that administrator account exists, a reset link will be sent.");
  expect(requestedEmail).toBe("owner@example.test");
});

test("validates and submits the password reset form without exposing token data in UI", async ({ page }) => {
  let resetRequest: { email: string | null; token: string | null; newPassword: string } | undefined;
  await page.route(`${apiOrigin}/api/**`, async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const respond = (body: unknown, status = 200) => route.fulfill({
      status, headers: corsHeaders, contentType: "application/json", body: JSON.stringify(body)
    });
    if (request.method() === "OPTIONS") return route.fulfill({ status: 204, headers: corsHeaders });
    if (path === "/api/v1/admin/csrf") return respond({ requestToken: "csrf-test-token" });
    if (path === "/api/v1/admin/password/reset" && request.method() === "POST") {
      resetRequest = request.postDataJSON() as { email: string | null; token: string | null; newPassword: string };
      return respond({});
    }
    return respond({ title: "Not found" }, 404);
  });

  await page.goto("/admin/reset-password#email=owner%40example.test&token=one-time-reset-token");
  await page.reload();
  await page.getByLabel("New password").fill("New-Admin-Password-2026!");
  await page.getByLabel("Confirm password").fill("Different-Password-2026!");
  await page.getByRole("button", { name: "Reset password" }).click();
  await expect(page.getByRole("alert")).toContainText("The passwords do not match.");
  expect(resetRequest).toBeUndefined();

  await page.getByLabel("Confirm password").fill("New-Admin-Password-2026!");
  await page.getByRole("button", { name: "Reset password" }).click();
  await expect(page.getByRole("status")).toContainText("Password reset.");
  expect(resetRequest).toEqual({ email: "owner@example.test", token: "one-time-reset-token", newPassword: "New-Admin-Password-2026!" });
});

test("SystemAdmin saves SMTP credentials and tests the saved relay", async ({ page }) => {
  const mockApi = mockSystemAdminApi(product => product);
  let savedPassword: string | undefined;
  let tested = false;
  const settings = { host: "smtp.gmail.com", port: 587, from: "owner@example.test", username: "owner@example.test", hasPassword: true };
  await page.route(`${apiOrigin}/api/**`, async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === "/api/v1/admin/security/smtp" && request.method() !== "OPTIONS") {
      if (request.method() === "PUT") savedPassword = request.postDataJSON().password;
      return route.fulfill({ headers: corsHeaders, json: settings });
    }
    if (path === "/api/v1/admin/security/smtp/test" && request.method() === "POST") {
      tested = true;
      return route.fulfill({ headers: corsHeaders, status: 204 });
    }
    return mockApi(route);
  });
  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.getByText("SMTP and password recovery", { exact: true }).click();
  await expect(page.getByLabel("SMTP password")).toHaveValue("");
  await page.getByLabel("SMTP password").fill("fake-test-app-password");
  await page.getByRole("button", { name: "Save SMTP settings" }).click();
  await expect(page.getByText("SMTP settings saved.")).toBeVisible();
  await expect(page.getByLabel("SMTP password")).toHaveValue("");
  expect(savedPassword).toBe("fake-test-app-password");
  await page.getByRole("button", { name: "Test saved settings" }).click();
  await expect(page.getByText("Check your administrator inbox.", { exact: false })).toBeVisible();
  expect(tested).toBe(true);
});

test("uploads a product image from the catalog editor and shows its saved entry", async ({ page }) => {
  let uploadBody = "";
  const mockApi = mockSystemAdminApi(product => product);
  await page.route(`${apiOrigin}/api/**`, async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === "/api/v1/admin/catalog/product/images" && request.method() === "POST") {
      uploadBody = request.postDataBuffer()?.toString("utf8") ?? "";
    }
    await mockApi(route);
  });

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.getByRole("navigation", { name: "Admin sections" }).getByRole("button", { name: "Catalog", exact: true }).click();
  await page.getByRole("navigation", { name: "Product editor sections" }).getByRole("button", { name: "Product images" }).click();
  const productImages = page.locator("#product-media");
  await productImages.getByLabel("Image file").setInputFiles({
    name: "product.png", mimeType: "image/png", buffer: Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j5ZkAAAAASUVORK5CYII=", "base64")
  });
  await productImages.getByLabel("Alternative text").fill("Uploaded product photo");
  await productImages.getByRole("button", { name: "Save product image" }).click();

  await expect(page.getByRole("status").filter({ hasText: "Product image saved." })).toBeVisible();
  await expect(productImages.getByLabel("Image alternative text")).toHaveValue("Uploaded product photo");
  expect(uploadBody).toContain("product.png");
  expect(uploadBody).toContain("Uploaded product photo");

  await productImages.getByLabel("Image alternative text").fill("Updated product photo");
  await productImages.getByLabel("Display order").fill("2");
  await productImages.getByRole("button", { name: "Save details" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Image details saved." })).toBeVisible();
  await expect(productImages.getByLabel("Image alternative text")).toHaveValue("Updated product photo");
  await productImages.getByRole("button", { name: "Delete image" }).click();
  await expect(productImages.getByText("No product images uploaded.")).toBeVisible();
});

test("shows only the workspace sections assigned to a Catalog Manager", async ({ page }) => {
  const catalogManager = { ...adminUser, roles: ["CatalogManager"] };
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi(product => product, () => adminUser, () => undefined, () => undefined, catalogManager));

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("catalog@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();

  const nav = page.getByRole("navigation", { name: "Admin sections" });
  await expect(nav.getByRole("button", { name: "Catalog", exact: true })).toBeVisible();
  await expect(nav.getByRole("button", { name: "Orders", exact: true })).toHaveCount(0);
  await expect(nav.getByRole("button", { name: "Storefront", exact: true })).toHaveCount(0);
  await expect(nav.getByRole("button", { name: "Accounts", exact: true })).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Product information" })).toBeVisible();
});

test("shows only order and customer sections assigned to an Order Manager", async ({ page }) => {
  const orderManager = { ...adminUser, roles: ["OrderManager"] };
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi(product => product, () => adminUser, () => undefined, () => undefined, orderManager));

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("orders@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();

  const nav = page.getByRole("navigation", { name: "Admin sections" });
  await expect(nav.getByRole("button", { name: "Orders", exact: true })).toBeVisible();
  await expect(nav.getByRole("button", { name: "Customers", exact: true })).toBeVisible();
  await expect(nav.getByRole("button", { name: "Catalog", exact: true })).toHaveCount(0);
  await expect(nav.getByRole("button", { name: "Storefront", exact: true })).toHaveCount(0);
  await expect(nav.getByRole("button", { name: "Accounts", exact: true })).toHaveCount(0);
});

test("shows only storefront settings assigned to a Store Manager", async ({ page }) => {
  const storeManager = { ...adminUser, roles: ["StoreManager"] };
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi(product => product, () => adminUser, () => undefined, () => undefined, storeManager));

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("store@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();

  const nav = page.getByRole("navigation", { name: "Admin sections" });
  await expect(nav.getByRole("button", { name: "Storefront", exact: true })).toBeVisible();
  await expect(nav.getByRole("button", { name: "Catalog", exact: true })).toHaveCount(0);
  await expect(nav.getByRole("button", { name: "Orders", exact: true })).toHaveCount(0);
  await expect(nav.getByRole("button", { name: "Accounts", exact: true })).toHaveCount(0);
});
