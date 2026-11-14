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
  variants: StorefrontVariant[];
}

export interface StorefrontVariant {
  id: string;
  displayName: string;
  price: number;
  currencyCode: string;
  isAvailable: boolean;
}

export async function getPublishedProduct(): Promise<StorefrontProduct | null> {
  const response = await fetch(`${apiBase}/api/v1/store/product`, { credentials: "omit" });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`The catalog could not be loaded (${response.status}).`);
  return (await response.json()) as StorefrontProduct;
}
