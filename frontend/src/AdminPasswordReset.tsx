import { FormEvent, useState } from "react";
import { adminRequest } from "./adminApi";

export default function AdminPasswordReset() {
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const params = new URLSearchParams(window.location.search);

  async function reset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(""); setMessage("");
    if (password !== confirmation) { setError("The passwords do not match."); setBusy(false); return; }
    try {
      await adminRequest<void>("/api/v1/admin/password/reset", { method: "POST", body: JSON.stringify({ email: params.get("email"), token: params.get("token"), newPassword: password }) });
      setMessage("Password reset. Return to administrator sign in and enroll your authenticator app.");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The reset link could not be used."); }
    finally { setBusy(false); }
  }

  return <main className="admin-page"><section className="admin-card login-card">
    <p className="eyebrow">Account recovery</p><h1>Choose a new password</h1>
    <form onSubmit={reset}>
      <label>New password<input type="password" autoComplete="new-password" minLength={12} maxLength={256} required value={password} onChange={event => setPassword(event.target.value)} /></label>
      <label>Confirm password<input type="password" autoComplete="new-password" minLength={12} maxLength={256} required value={confirmation} onChange={event => setConfirmation(event.target.value)} /></label>
      <p className="admin-muted">Use at least 12 characters with uppercase, lowercase, a number, and a symbol.</p>
      <button className="button login-submit" type="submit" disabled={busy || !params.get("email") || !params.get("token")}>{busy ? "Saving…" : "Reset password"}</button>
      {message && <p className="admin-message" role="status">{message}</p>}{error && <p className="admin-error login-feedback" role="alert">{error}</p>}
    </form>
  </section></main>;
}
