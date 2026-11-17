import { apiBase } from "./apiBase";

export async function checkInitialSetup(): Promise<boolean> {
  const response = await fetch(`${apiBase}/api/v1/setup/status`, { cache: "no-store" });
  if (response.status === 404) return false;
  if (!response.ok) throw new Error("Initial setup status could not be checked.");
  const payload = (await response.json()) as { available: boolean };
  return payload.available;
}

export async function createInitialSystemAdmin(input: {
  email: string;
  password: string;
  confirmPassword: string;
  setupToken: string;
}): Promise<void> {
  const response = await fetch(`${apiBase}/api/v1/setup/system-admin`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-OneCart-Setup-Token": input.setupToken
    },
    body: JSON.stringify({
      email: input.email,
      password: input.password,
      confirmPassword: input.confirmPassword
    })
  });

  if (!response.ok) {
    const payload = (await response.json().catch(() => null)) as
      | { error?: string; title?: string; errors?: Record<string, string[]> }
      | null;
    const detail = payload?.errors ? Object.values(payload.errors).flat().join(" ") : undefined;
    throw new Error(payload?.error ?? detail ?? payload?.title ?? `Setup failed (${response.status}).`);
  }
}
