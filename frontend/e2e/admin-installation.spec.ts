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

function mockSystemAdminApi(onProductSave: (product: typeof initialAdminProduct) => typeof initialAdminProduct) {
  let authenticated = false;
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
    } else if (path === "/api/v1/admin/users") {
      await respond([adminUser]);
    } else if (path === "/api/v1/admin/audit") {
      await respond([]);
    } else if (path === "/api/v1/admin/security/mfa-policy") {
      await respond({ required: false });
    } else if (path === "/api/v1/admin/catalog/product" && method === "GET") {
      await respond(initialAdminProduct);
    } else if (path === "/api/v1/admin/catalog/product" && method === "PUT") {
      const saved = onProductSave(request.postDataJSON() as typeof initialAdminProduct);
      await respond(saved);
    } else if (path === "/api/v1/admin/storefront/settings") {
      await respond({
        brandName: "Pretty Shop", storefrontHeadline: "Your trust, Your Love", aboutUs: "",
        benefits: [], usageSteps: [], reviews: [], storefrontSubtitle: "",
        flashSale: initialAdminProduct.flashSale, socialSharingEnabled: true,
        whatsAppEnabled: false, whatsAppPhoneNumber: "", whatsAppPrefillMessage: "",
        brandLogo: null, productImages: [], trustItems: [], storefrontTranslations: {},
        theme: { primaryColor: "#242020", accentColor: "#c17b88", textColor: "#282326", mutedTextColor: "#777078", backgroundColor: "#fbf8f8", surfaceColor: "#ffffff", borderColor: "#eee5e8", productTitleFontSizePx: 30, detailsFontSizePx: 13 }
      });
    } else if (path === "/api/v1/admin/orders" || path === "/api/v1/admin/orders/summary") {
      await respond([]);
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
