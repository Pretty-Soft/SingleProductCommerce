import { FormEvent, useEffect, useState } from "react";
import { adminRequest } from "./adminApi";

export default function AdminMfaEnrollment({ onComplete }: { onComplete: () => Promise<void> }) {
  const [key, setKey] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    void adminRequest<{ authenticatorKey: string }>("/api/v1/admin/mfa/setup")
      .then(result => setKey(result.authenticatorKey))
      .catch(reason => setError(reason instanceof Error ? reason.message : "Authenticator setup could not be loaded."));
  }, []);

  async function confirm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError("");
    try {
      await adminRequest("/api/v1/admin/mfa/setup", { method: "POST", body: JSON.stringify({ code }) });
      await onComplete();
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The authenticator code was rejected."); }
    finally { setBusy(false); }
  }

  return <section className="admin-card login-card">
    <p className="eyebrow">Secure your administrator account</p>
    <h1>Set up an authenticator app</h1>
    <p className="admin-muted">Add this account in your authenticator app using the setup key, then enter its current six-digit code. Admin features stay locked until setup is confirmed.</p>
    {key && <div className="mfa-key" aria-label="Authenticator setup key">{key}</div>}
    <form onSubmit={confirm}>
      <label>Authenticator code<input inputMode="numeric" autoComplete="one-time-code" pattern="[0-9 ]{6,12}" required value={code} onChange={event => setCode(event.target.value)} /></label>
      <button className="button login-submit" type="submit" disabled={busy || !key}>{busy ? "Verifying…" : "Verify and enable MFA"}</button>
      {error && <p className="admin-error login-feedback" role="alert">{error}</p>}
    </form>
  </section>;
}
