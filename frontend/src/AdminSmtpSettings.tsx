import { FormEvent, useEffect, useState } from "react";
import { adminRequest } from "./adminApi";

type Settings = { host: string; port: number; from: string; username: string | null; hasPassword: boolean };
const path = "/api/v1/admin/security/smtp";

export default function AdminSmtpSettings() {
  const [settings, setSettings] = useState<Settings | null>(null);
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    void adminRequest<Settings>(path).then(value => { if (active) setSettings(value); })
      .catch(() => { if (active) setError("Could not load SMTP settings."); });
    return () => { active = false; };
  }, []);

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setMessage("");
    setError("");
    try { await action(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "SMTP operation failed."); }
    finally { setBusy(false); }
  }

  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void run(async () => {
      const saved = await adminRequest<Settings>(path, {
        method: "PUT", body: JSON.stringify({ ...settings, password: password || null })
      });
      setSettings(saved);
      setPassword("");
      setMessage("SMTP settings saved.");
    });
  }

  return <div>
    {error && <p role="alert">{error}</p>}
    {message && <p role="status">{message}</p>}
    {settings && <form onSubmit={save}>
      <p className="admin-muted">For Gmail, use smtp.gmail.com, port 587, and a Google app password. Other SMTP providers can use their own STARTTLS settings.</p>
      <label>SMTP host<input required maxLength={253} value={settings.host} onChange={event => setSettings({ ...settings, host: event.target.value })} /></label>
      <label>STARTTLS port<input required type="number" min={1} max={65535} value={settings.port} onChange={event => setSettings({ ...settings, port: Number(event.target.value) })} /></label>
      <label>Sender email<input required type="email" maxLength={254} value={settings.from} onChange={event => setSettings({ ...settings, from: event.target.value })} /></label>
      <label>SMTP username<input maxLength={254} value={settings.username ?? ""} onChange={event => setSettings({ ...settings, username: event.target.value })} /></label>
      <label>SMTP password<input type="password" autoComplete="new-password" maxLength={512} placeholder={settings.hasPassword ? "Password already saved" : undefined} value={password} onChange={event => setPassword(event.target.value)} /></label>
      <div>
        <button className="button" disabled={busy} type="submit">Save SMTP settings</button>{" "}
        <button className="button" disabled={busy} type="button" onClick={() => void run(async () => {
          await adminRequest<void>(`${path}/test`, { method: "POST" });
          setMessage("The saved SMTP relay accepted the test message. Check your administrator inbox.");
        })}>Test saved settings</button>
      </div>
    </form>}
  </div>;
}
