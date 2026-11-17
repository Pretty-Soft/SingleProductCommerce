import { apiBase } from "./apiBase";

export interface InitialSetupStatus { available: boolean; requiresDatabase: boolean }

export interface DatabaseSetupInput {
  provider: "PostgreSql" | "SqlServer" | "MySql";
  host: string;
  port: number | null;
  databaseName: string;
  userName: string;
  password: string;
  trustServerCertificate: boolean;
  encryptConnection: boolean;
}

export async function checkInitialSetup(): Promise<InitialSetupStatus> {
  const response = await fetch(`${apiBase}/api/v1/setup/status`, { cache: "no-store" });
  if (response.status === 404) return { available: false, requiresDatabase: false };
  if (!response.ok) throw new Error("Initial setup status could not be checked.");
  const payload = (await response.json()) as Partial<InitialSetupStatus>;
  return { available: Boolean(payload.available), requiresDatabase: Boolean(payload.requiresDatabase) };
}

async function setupPost(path: string, body: unknown, setupToken: string): Promise<void> {
  const response = await fetch(`${apiBase}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-OneCart-Setup-Token": setupToken },
    body: JSON.stringify(body)
  });
  if (!response.ok) {
    const payload = (await response.json().catch(() => null)) as
      | { error?: string; title?: string; errors?: Record<string, string[]> } | null;
    const detail = payload?.errors ? Object.values(payload.errors).flat().join(" ") : undefined;
    throw new Error(payload?.error ?? detail ?? payload?.title ?? `Setup failed (${response.status}).`);
  }
}

export async function testInstallationDatabase(database: DatabaseSetupInput, setupToken: string): Promise<void> {
  await setupPost("/api/v1/setup/database/test", database, setupToken);
}

export async function completeInstallation(input: {
  database: DatabaseSetupInput;
  email: string;
  password: string;
  confirmPassword: string;
  defaultLanguageCode: string;
  currencyCode: string;
  setupToken: string;
}): Promise<void> {
  const { setupToken, ...body } = input;
  await setupPost("/api/v1/setup/install", body, setupToken);
}

export async function createInitialSystemAdmin(input: {
  email: string;
  password: string;
  confirmPassword: string;
  setupToken: string;
}): Promise<void> {
  await setupPost("/api/v1/setup/system-admin", {
    email: input.email, password: input.password, confirmPassword: input.confirmPassword
  }, input.setupToken);
}
