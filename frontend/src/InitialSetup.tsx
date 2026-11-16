import { FormEvent, useEffect, useState } from "react";
import { checkInitialSetup, createInitialSystemAdmin } from "./setupApi";

export default function InitialSetup() {
  const [available, setAvailable] = useState<boolean | null>(null);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [setupToken, setSetupToken] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [complete, setComplete] = useState(false);

  useEffect(() => {
    let active = true;
    void checkInitialSetup()
      .then((canInstall) => { if (active) setAvailable(canInstall); })
      .catch((reason: unknown) => {
        if (active) setError(reason instanceof Error ? reason.message : "Setup could not reach the secure API.");
      });
    return () => { active = false; };
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await createInitialSystemAdmin({ email, password, confirmPassword, setupToken });
      setComplete(true);
      setAvailable(false);
      setPassword("");
      setConfirmPassword("");
      setSetupToken("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "The initial System Admin could not be created.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="admin-page setup-page">
      <section className="admin-content">
        <div className="admin-card login-card setup-card">
          <span className="admin-kicker">OneCart Commerce</span>
          <h1>Initial store setup</h1>
          {complete ? (
            <div className="setup-status" role="status">
              <p>Your System Admin account is ready. The one-time setup token can now be rotated on the server.</p>
              <a className="button button-primary" href="/admin">Continue to admin sign in</a>
            </div>
          ) : available === false ? (
            <div className="setup-status" role="status">
              <p>Initial setup is disabled or has already been completed.</p>
              <a className="button button-primary" href="/admin">Go to admin sign in</a>
            </div>
          ) : available === null ? (
            <p className="admin-muted" role="status">Checking secure setup availability…</p>
          ) : (
            <>
              <p className="admin-muted">Create the first System Admin account. This one-time form is protected by the setup token configured by the server operator.</p>
              {error && <p className="setup-error" role="alert">{error}</p>}
              <form onSubmit={submit}>
                <label>
                  Administrator email
                  <input type="email" autoComplete="email" maxLength={254} required value={email} onChange={(event) => setEmail(event.target.value)} />
                </label>
                <label>
                  Password
                  <input type="password" autoComplete="new-password" minLength={12} maxLength={256} required value={password} onChange={(event) => setPassword(event.target.value)} />
                </label>
                <label>
                  Confirm password
                  <input type="password" autoComplete="new-password" minLength={12} maxLength={256} required value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} />
                </label>
                <label>
                  One-time setup token
                  <input type="password" autoComplete="off" maxLength={512} required value={setupToken} onChange={(event) => setSetupToken(event.target.value)} />
                </label>
                <button className="login-submit" type="submit" disabled={busy}>{busy ? "Creating account…" : "Create System Admin"}</button>
              </form>
            </>
          )}
        </div>
      </section>
    </main>
  );
}
