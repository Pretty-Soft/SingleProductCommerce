import { apiBase } from "./apiBase";

export interface StorefrontProduct {
  id: string;
  name: string;
  slug: string;
  description: string;
  currencyCode: string;
  brandName: string;
  storefrontHeadline: string;
  aboutUs: string;
  benefits: StorefrontContentItem[];
  usageSteps: StorefrontContentItem[];
  storefrontSubtitle: string;
  variants: StorefrontVariant[];
  attributeGroups: StorefrontAttributeGroup[];
  flashSale: StorefrontFlashSale;
  socialSharingEnabled: boolean;
  whatsAppContactUrl: string | null;
  images: StorefrontProductImage[];
  brandLogoUrl: string | null;
  trustItems: StorefrontTrustItem[];
  averageRating: number;
  reviewCount: number;
  reviews: StorefrontReview[];
  theme: StorefrontTheme;
}

export interface StorefrontTheme {
  primaryColor: string;
  accentColor: string;
  textColor: string;
  mutedTextColor: string;
  backgroundColor: string;
  surfaceColor: string;
  borderColor: string;
  productTitleFontSizePx: number;
  detailsFontSizePx: number;
}

const previousThemeDefaults: Record<keyof Pick<StorefrontTheme, "primaryColor" | "accentColor" | "textColor" | "mutedTextColor" | "backgroundColor" | "surfaceColor" | "borderColor">, string> = {
  primaryColor: "#242020",
  accentColor: "#c17b88",
  textColor: "#282326",
  mutedTextColor: "#777078",
  backgroundColor: "#fbf8f8",
  surfaceColor: "#ffffff",
  borderColor: "#eee5e8"
};

const rosyThemeDefaults: typeof previousThemeDefaults = {
  primaryColor: "#171316",
  accentColor: "#e8437a",
  textColor: "#211b1e",
  mutedTextColor: "#756970",
  backgroundColor: "#ffffff",
  surfaceColor: "#ffffff",
  borderColor: "#f1dce4"
};

export function withRosyDefaultPalette<T extends StorefrontTheme>(theme: T): T {
  const updated = { ...theme };
  for (const key of Object.keys(previousThemeDefaults) as Array<keyof typeof previousThemeDefaults>) {
    if (updated[key].toLowerCase() === previousThemeDefaults[key]) updated[key] = rosyThemeDefaults[key] as T[typeof key];
  }
  return updated;
}

export interface CartPriceQuote {
  token: string;
  currencyCode: string;
  unitPrice: number;
  quotedAtUtc: string;
}

export interface StoreOrderConfirmation {
  orderNumber: string;
  status: string;
  currencyCode: string;
  total: number;
  createdAtUtc: string;
  lines: Array<{ productName: string; variantName: string; quantity: number; unitPrice: number }>;
}

export interface StorefrontProductImage {
  id: string;
  url: string;
  mediaKind: "product" | "logo";
  altText: string;
  displayOrder: number;
}

export interface StorefrontTrustItem {
  title: string;
  description: string;
}

export interface StorefrontContentItem { id: string; title: string; description: string }
export interface StorefrontReview { displayName: string; rating: number; title: string; content: string }

export interface StorefrontFlashSale {
  enabled: boolean;
  label: string;
  discountType: "percentage" | "fixedAmount";
  discountValue: number;
  startsAtUtc: string | null;
  endsAtUtc: string | null;
  serverTimeUtc: string;
}

export interface StorefrontAttributeGroup {
  id: string;
  name: string;
  isRequired: boolean;
  allowMultiple: boolean;
  options: StorefrontAttributeOption[];
}

export interface StorefrontAttributeOption {
  id: string;
  name: string;
  priceAdjustment: number;
}

export interface StorefrontVariant {
  id: string;
  sku: string;
  displayName: string;
  price: number;
  salePrice: number | null;
  currencyCode: string;
  isAvailable: boolean;
}

export async function getDefaultStoreLanguage(): Promise<string> {
  const response = await fetch(`${apiBase}/api/v1/store/configuration`, { credentials: "omit" });
  if (!response.ok) return "en";
  const payload = await response.json() as { defaultLanguageCode?: string };
  return payload.defaultLanguageCode ?? "en";
}

export async function getPublishedProduct(language = "en"): Promise<StorefrontProduct | null> {
  const response = await fetch(`${apiBase}/api/v1/store/product?language=${encodeURIComponent(language)}`, { credentials: "omit" });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`The catalog could not be loaded (${response.status}).`);
  const product = (await response.json()) as StorefrontProduct;
  product.theme = withRosyDefaultPalette(product.theme);
  return product;
}

export async function quoteCartSelection(variantId: string, attributeOptionIds: string[]): Promise<CartPriceQuote> {
  const response = await fetch(`${apiBase}/api/v1/store/cart/quote`, {
    method: "POST",
    credentials: "omit",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ variantId, attributeOptionIds })
  });
  return readResponse<CartPriceQuote>(response);
}

export async function placeStoreOrder(request: {
  customerName: string;
  phoneNumber: string;
  deliveryAddress: string;
  lines: Array<{ quoteToken: string; quantity: number }>;
}): Promise<StoreOrderConfirmation> {
  const response = await fetch(`${apiBase}/api/v1/store/orders`, {
    method: "POST",
    credentials: "omit",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  return readResponse<StoreOrderConfirmation>(response);
}

async function readResponse<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => null) as { detail?: string; error?: string; title?: string } | null;
  if (!response.ok) {
    throw new Error(body?.detail ?? body?.error ?? body?.title ?? `Store request failed (${response.status}).`);
  }
  return body as T;
}

export function getMediaUrl(path: string): string {
  return new URL(path, `${apiBase}/`).toString();
}
