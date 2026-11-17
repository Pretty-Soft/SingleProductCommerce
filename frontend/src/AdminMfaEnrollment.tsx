import { FormEvent, useEffect, useState } from "react";
import QRCode from "qrcode";
import { adminRequest } from "./adminApi";

export default function AdminMfaEnrollment({ onComplete }: { onComplete: () => Promise<void> }) {
  const [qrCode, setQrCode] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;
    void adminRequest<{ authenticatorKey: string; account: string }>("/api/v1/admin/mfa/setup")
      .then(({ authenticatorKey, account }) => {
        const secret = authenticatorKey.replace(/\s/g, "").toUpperCase();
        if (!account || !/^[A-Z2-7]+$/.test(secret)) throw new Error("Authenticator setup could not be loaded.");
        const parameters = new URLSearchParams({ secret, issuer: "OneCart" });
        const enrollment = `otpauth://totp/OneCart:${encodeURIComponent(account)}?${parameters}`;
        return QRCode.toDataURL(enrollment, { width: 256, margin: 4, errorCorrectionLevel: "M" });
      })
      .then(image => { if (active) setQrCode(image); })
      .catch(() => { if (active) setError("Could not load the authenticator QR code. Refresh the page to try again."); });
    return () => { active = false; };
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
    <p className="admin-muted">In your authenticator app, choose to add an account and scan this QR code. Then enter the six-digit code shown in the app.</p>
    {qrCode ? <img className="mfa-qr" src={qrCode} width={256} height={256} alt="Scan to enroll OneCart in your authenticator app" />
      : !error && <p role="status">Loading authenticator QR code…</p>}
    <form onSubmit={confirm}>
      <label>Authenticator code<input inputMode="numeric" autoComplete="one-time-code" pattern="[0-9 ]{6,12}" required value={code} onChange={event => setCode(event.target.value)} /></label>
      <button className="button login-submit" type="submit" disabled={busy || !qrCode}>{busy ? "Verifying…" : "Verify and enable MFA"}</button>
      {error && <p className="admin-error login-feedback" role="alert">{error}</p>}
    </form>
  </section>;
}
