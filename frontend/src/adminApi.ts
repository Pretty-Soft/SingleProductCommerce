const configuredApiBase = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7241";
const apiBase = configuredApiBase.replace(/\/$/, "");

if (new URL(apiBase).protocol !== "https:") {
  throw new Error("The OneCart API base URL must use HTTPS.");
}

let csrfToken: string | null = null;

export interface AdminUser {
  id: string;
  email: string;
  roles: string[];
  mustChangePassword: boolean;
  lockoutEnd: string | null;
}

export interface AdminRole {
  id: string;
  name: string;
}

export interface AdminAuditEvent {
  id: string;
  actorEmail: string;
  action: string;
  target: string;
  details: string;
  createdAtUtc: string;
}

export async function refreshCsrfToken(): Promise<void> {
  const response = await fetch(`${apiBase}/api/v1/admin/csrf`, {
    credentials: "include"
  });
  if (!response.ok) {
    throw new Error("Could not initialize the secure admin session.");
  }

  const payload = (await response.json()) as { requestToken: string };
  csrfToken = payload.requestToken;
}

export async function adminRequest<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const method = (options.method ?? "GET").toUpperCase();
  const mutatesState = !["GET", "HEAD", "OPTIONS"].includes(method);
  if (mutatesState && !csrfToken) {
    await refreshCsrfToken();
  }

  const headers = new Headers(options.headers);
  if (options.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (mutatesState && csrfToken) {
    headers.set("X-CSRF-TOKEN", csrfToken);
  }

  const response = await fetch(`${apiBase}${path}`, {
    ...options,
    method,
    headers,
    credentials: "include"
  });

  if (!response.ok) {
    const payload = (await response.json().catch(() => null)) as
      | { error?: string; title?: string; errors?: Record<string, string[]> }
      | null;
    const validationMessage = payload?.errors
      ? Object.values(payload.errors).flat().join(" ")
      : undefined;
    throw new Error(payload?.error ?? validationMessage ?? payload?.title ?? `Request failed (${response.status}).`);
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}
