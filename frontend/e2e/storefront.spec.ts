import { expect, test } from "@playwright/test";

const productId = "00000000-0000-0000-0000-000000000001";
const variantBasicId = "00000000-0000-0000-0000-000000000011";
const variantLargeId = "00000000-0000-0000-0000-000000000012";
const attributeGroupId = "00000000-0000-0000-0000-000000000021";
const attributeOptionId = "00000000-0000-0000-0000-000000000022";

function storefrontProduct() {
  const now = Date.now();
  return {
    id: productId,
    name: "Coconut Curls Shampoo",
    slug: "coconut-curls-shampoo",
    description: "A gentle shampoo for soft, defined curls.",
    currencyCode: "BDT",
    brandName: "Configured Test Brand",
    storefrontHeadline: "Your trust, Your Love",
    aboutUs: "A small store focused on thoughtful products.",
    benefits: [{ id: "benefit-1", title: "Define curls naturally", description: "Moisturizes and reduces frizz." }],
    usageSteps: [{ id: "usage-1", title: "Apply", description: "Wet hair, apply gently, and rinse well." }],
    storefrontSubtitle: "For soft, defined curls",
    variants: [
      { id: variantBasicId, sku: "CURL-385", displayName: "Classic 385 ml", price: 1590, salePrice: 1490, currencyCode: "BDT", isAvailable: true },
      { id: variantLargeId, sku: "CURL-750", displayName: "Family 750 ml", price: 2390, salePrice: 2290, currencyCode: "BDT", isAvailable: true }
    ],
    attributeGroups: [{
      id: attributeGroupId,
      name: "Gift wrap",
      isRequired: true,
      allowMultiple: false,
      options: [{ id: attributeOptionId, name: "Add gift wrap", priceAdjustment: 50 }]
    }],
    flashSale: {
      enabled: true,
      label: "Winter Sale",
      discountType: "fixedAmount",
      discountValue: 100,
      startsAtUtc: new Date(now - 60_000).toISOString(),
      endsAtUtc: new Date(now + 3_600_000).toISOString(),
      serverTimeUtc: new Date(now).toISOString()
    },
    socialSharingEnabled: false,
    whatsAppContactUrl: null,
    images: [{ id: "image-1", url: "/api/v1/store/product/images/image-1", mediaKind: "product", altText: "Shampoo bottle", displayOrder: 0 }],
    brandLogoUrl: null,
    trustItems: [],
    averageRating: 4.5,
    reviewCount: 1,
    reviews: [{ displayName: "Mina", rating: 5, title: "Lovely", content: "Feels gentle and fresh." }],
    theme: {
      primaryColor: "#171316",
      accentColor: "#e8437a",
      textColor: "#211b1e",
      mutedTextColor: "#756970",
      backgroundColor: "#ffffff",
      surfaceColor: "#ffffff",
      borderColor: "#f1dce4",
      productTitleFontSizePx: 32,
      detailsFontSizePx: 14
    }
  };
}

test("renders the product landing page and completes a variant COD order", async ({ page }) => {
  let quoteRequest: { variantId?: string; attributeOptionIds?: string[] } | undefined;
  let orderRequest: { customerName?: string; phoneNumber?: string; deliveryAddress?: string; lines?: Array<{ quoteToken: string; quantity: number }> } | undefined;

  await page.route("**/api/v1/store/product**", async (route) => {
    if (new URL(route.request().url()).pathname.includes("/images/")) {
      await route.fulfill({ status: 200, contentType: "image/svg+xml", body: "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"#e8437a\"/></svg>" });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(storefrontProduct()) });
  });
  await page.route("**/api/v1/store/cart/quote", async (route) => {
    quoteRequest = route.request().postDataJSON() as typeof quoteRequest;
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ token: "signed-ci-quote", currencyCode: "BDT", unitPrice: 2340, quotedAtUtc: new Date().toISOString() }) });
  });
  await page.route("**/api/v1/store/orders", async (route) => {
    orderRequest = route.request().postDataJSON() as typeof orderRequest;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        orderNumber: "OC-CI-1001",
        status: "Pending",
        currencyCode: "BDT",
        total: 4680,
        createdAtUtc: new Date().toISOString(),
        lines: [{ productName: "Coconut Curls Shampoo", variantName: "Family 750 ml", quantity: 2, unitPrice: 2340 }]
      })
    });
  });

  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Coconut Curls Shampoo" })).toBeVisible();
  await expect(page.getByText("Winter Sale")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Customer reviews" })).toBeVisible();
  await expect(page.getByRole("img", { name: "Shampoo bottle" })).toBeVisible();
  await expect(page.getByText(`© ${new Date().getFullYear()} Configured Test Brand`)).toBeVisible();

  const addButton = page.getByRole("button", { name: "Add to cart" });
  await expect(addButton).toBeDisabled();
  await page.getByLabel("Family 750 ml", { exact: false }).check();
  await page.getByLabel("Add gift wrap").check();
  await expect(addButton).toBeEnabled();
  await page.getByRole("spinbutton", { name: "Quantity" }).fill("2");
  await addButton.click();

  await expect(page.getByRole("heading", { name: "Shopping cart (2)" })).toBeVisible();
  expect(quoteRequest).toEqual({ variantId: variantLargeId, attributeOptionIds: [attributeOptionId] });
  await page.getByRole("button", { name: "Cash on Delivery checkout" }).click();
  await page.getByLabel("Customer name").fill("CI Shopper");
  await page.getByLabel("Phone number").fill("01800000000");
  await page.getByLabel("Delivery address").fill("1 Example Road, Dhaka");
  await page.getByRole("button", { name: "Place COD order" }).click();

  await expect(page.getByText("OC-CI-1001")).toBeVisible();
  expect(orderRequest).toMatchObject({
    customerName: "CI Shopper",
    phoneNumber: "01800000000",
    deliveryAddress: "1 Example Road, Dhaka",
    lines: [{ quoteToken: "signed-ci-quote", quantity: 2 }]
  });
});

test("keeps the responsive storefront usable on a narrow viewport", async ({ page }) => {
  await page.route("**/api/v1/store/product**", async (route) => {
    if (new URL(route.request().url()).pathname.includes("/images/")) {
      await route.fulfill({ status: 200, contentType: "image/svg+xml", body: "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"#e8437a\"/></svg>" });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(storefrontProduct()) });
  });
  await page.goto("/");
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(page.getByRole("heading", { name: "Coconut Curls Shampoo" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Add to cart" })).toBeVisible();
  await expect(page.getByRole("contentinfo")).toBeVisible();
  const variants = page.locator(".store-variant");
  await expect(variants).toHaveCount(2);
  const firstVariant = await variants.nth(0).boundingBox();
  const secondVariant = await variants.nth(1).boundingBox();
  expect(firstVariant).not.toBeNull();
  expect(secondVariant).not.toBeNull();
  expect(secondVariant!.y).toBeGreaterThan(firstVariant!.y + firstVariant!.height);
  expect(firstVariant!.width).toBeGreaterThan(300);
  expect(firstVariant!.height).toBeLessThan(120);
  const hasHorizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
  expect(hasHorizontalOverflow).toBe(false);
});
