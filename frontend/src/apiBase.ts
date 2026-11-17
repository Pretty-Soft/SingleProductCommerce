declare global {
  interface Window {
    __ONECART_API_BASE_URL__?: string;
  }
}

export function resolveApiBase(runtimeValue: string | undefined, buildValue: string | undefined): string {
  const configured = runtimeValue?.trim() || buildValue?.trim() || "https://localhost:44300";
  let url: URL;
  try {
    url = new URL(configured);
  } catch {
    throw new Error("The OneCart API base URL is invalid.");
  }
  if (url.protocol !== "https:" || url.username || url.password || url.search || url.hash) {
    throw new Error("The OneCart API base URL must use HTTPS without credentials, a query, or a fragment.");
  }
  return url.toString().replace(/\/+$/, "");
}

export const apiBase = resolveApiBase(
  typeof window === "undefined" ? undefined : window.__ONECART_API_BASE_URL__,
  import.meta.env.VITE_API_BASE_URL
);
