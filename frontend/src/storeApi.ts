const configuredApiBase = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7241";
const apiBase = configuredApiBase.replace(/\/$/, "");

if (new URL(apiBase).protocol !== "https:") {
  throw new Error("The OneCart API base URL must use HTTPS.");
}

export interface StorefrontProduct {
  id: string;
  name: string;
  slug: string;
  description: string;
  brandName: string;
  storefrontHeadline: string;
  storefrontSubtitle: string;
  variants: StorefrontVariant[];
  attributeGroups: StorefrontAttributeGroup[];
  flashSale: StorefrontFlashSale;
  socialSharingEnabled: boolean;
  whatsAppContactUrl: string | null;
  images: StorefrontProductImage[];
  brandLogoUrl: string | null;
  trustItems: StorefrontTrustItem[];
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
  displayName: string;
  price: number;
  salePrice: number | null;
  currencyCode: string;
  isAvailable: boolean;
}

export async function getPublishedProduct(): Promise<StorefrontProduct | null> {
  const response = await fetch(`${apiBase}/api/v1/store/product`, { credentials: "omit" });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`The catalog could not be loaded (${response.status}).`);
  return (await response.json()) as StorefrontProduct;
}

export function getMediaUrl(path: string): string {
  return new URL(path, `${apiBase}/`).toString();
}
