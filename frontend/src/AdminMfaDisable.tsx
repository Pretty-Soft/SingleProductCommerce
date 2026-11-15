import { FormEvent, useState } from "react";
import { adminRequest } from "./adminApi";

export default function AdminMfaDisable({ onComplete }: { onComplete: () => Promise<void> }) {
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function disable(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError("");
    try { await adminRequest<void>("/api/v1/admin/mfa/disable", { method: "POST", body: JSON.stringify({ code }) }); await onComplete(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "MFA could not be disabled."); }
    finally { setBusy(false); }
  }
  return <form className="mfa-disable-form" onSubmit={disable}>
    <label>Current authenticator code<input inputMode="numeric" autoComplete="one-time-code" pattern="[0-9 ]{6,12}" required value={code} onChange={event => setCode(event.target.value)} /></label>
    <button className="button button-quiet" type="submit" disabled={busy}>{busy ? "Updating…" : "Disable optional MFA"}</button>
    {error && <p className="admin-error" role="alert">{error}</p>}
  </form>;
}
