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
  onOrderStatus: (status: string) => void = () => undefined
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
      await respond(authenticated ? adminUser : { title: "Unauthorized" }, authenticated ? 200 : 401);
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

test("adds a second variant, edits its stock, and saves them together", async ({ page }) => {
  let savedVariants: typeof initialAdminProduct.variants = [];
  await page.route(`${apiOrigin}/api/**`, mockSystemAdminApi((product) => {
    savedVariants = product.variants;
    return product;
  }));

  await page.goto("/admin");
  await page.getByLabel("Email address").fill("owner@example.test");
  await page.getByLabel("Password").fill("OneCart-Test-Admin-2026!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Overview" })).toBeVisible();
  await page.getByRole("button", { name: "Catalog", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Product catalog" })).toBeVisible();

  const sectionNav = page.getByRole("navigation", { name: "Product editor sections" });
  await sectionNav.getByRole("button", { name: "Variants" }).click();
  const variants = page.locator("#product-variants");
  await variants.getByRole("button", { name: "Edit variants" }).click();
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
  await page.getByRole("navigation", { name: "Admin sections" }).getByRole("button", { name: "Accounts & roles", exact: true }).click();

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
  await page.getByRole("navigation", { name: "Admin sections" }).getByRole("button", { name: "Storefront settings", exact: true }).click();
  const settings = page.getByRole("heading", { name: "Storefront settings" }).locator("xpath=../../..");

  await settings.getByLabel("About Us", { exact: true }).fill("A neighborhood shop serving local families.");
  await settings.getByLabel("Instructions", { exact: true }).fill("Use a small amount.\n\nRepeat as needed.");
  await settings.getByRole("button", { name: "Add review" }).click();
  await settings.getByLabel("Display name", { exact: true }).fill("Verified customer");
  await settings.getByLabel("Review title", { exact: true }).fill("A good fit");
  await settings.getByLabel("Review", { exact: true }).fill("This sample review remains unpublished until an administrator approves it.");
  await settings.getByRole("button", { name: "Save storefront settings" }).click();

  await expect(settings.getByRole("status")).toContainText("Storefront settings saved.");
  expect(savedSettings?.aboutUs).toBe("A neighborhood shop serving local families.");
  expect(savedSettings?.usageSteps).toMatchObject([{ title: "Instructions", description: "Use a small amount.\n\nRepeat as needed." }]);
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

  await page.goto("/admin/reset-password?email=owner%40example.test&token=one-time-reset-token");
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
