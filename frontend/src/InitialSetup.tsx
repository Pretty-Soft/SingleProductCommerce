import { FormEvent, useEffect, useState } from "react";
import { checkInitialSetup, completeInstallation, createInitialSystemAdmin, DatabaseSetupInput, testInstallationDatabase } from "./setupApi";

const currencyChoices = ["BDT", "USD", "EUR", "GBP", "INR", "AED", "SAR", "CAD", "AUD", "JPY", "CNY", "SGD", "MYR", "PKR", "NPR", "LKR", "CHF", "NZD", "QAR", "KWD"];

export default function InitialSetup() {
  const [available, setAvailable] = useState<boolean | null>(null);
  const [requiresDatabase, setRequiresDatabase] = useState(false);
  const [database, setDatabase] = useState<DatabaseSetupInput>({ provider: "PostgreSql", host: "localhost", port: 5432, databaseName: "onecart", userName: "", password: "", trustServerCertificate: false, encryptConnection: true });
  const [databaseTested, setDatabaseTested] = useState(false);
  const [defaultLanguageCode, setDefaultLanguageCode] = useState("en");
  const [currencyCode, setCurrencyCode] = useState("");
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
      .then((status) => { if (active) { setAvailable(status.available); setRequiresDatabase(status.requiresDatabase); } })
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
      if (requiresDatabase) {
        await completeInstallation({ database, email, password, confirmPassword, defaultLanguageCode, currencyCode, setupToken });
      } else {
        await createInitialSystemAdmin({ email, password, confirmPassword, setupToken });
      }
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

  function updateDatabase(changes: Partial<DatabaseSetupInput>) {
    setDatabase(current => ({ ...current, ...changes }));
    setDatabaseTested(false);
  }

  async function testDatabase() {
    setBusy(true);
    setError("");
    try {
      await testInstallationDatabase(database, setupToken);
      setDatabaseTested(true);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Database connection failed.");
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
              <p className="admin-muted">{requiresDatabase ? "Connect a database, create the first SystemAdmin, and set the store defaults. The installer applies its schema during setup." : "Create the first System Admin account."} This one-time form is protected by the setup token configured by the server operator.</p>
              {error && <p className="setup-error" role="alert">{error}</p>}
              <form onSubmit={submit}>
                {requiresDatabase && <fieldset className="setup-database-fields">
                  <legend>Database connection</legend>
                  <label>Database provider
                    <select value={database.provider} onChange={event => updateDatabase({ provider: event.target.value as DatabaseSetupInput["provider"], port: event.target.value === "PostgreSql" ? 5432 : null })}>
                      <option value="PostgreSql">PostgreSQL</option>
                      <option value="SqlServer">SQL Server (experimental)</option>
                    </select>
                  </label>
                  <label>Host or server name<input required maxLength={255} value={database.host} onChange={event => updateDatabase({ host: event.target.value })} /></label>
                  <label>Port (optional for SQL Server)<input type="number" min={1} max={65535} value={database.port ?? ""} onChange={event => updateDatabase({ port: event.target.value ? Number(event.target.value) : null })} /></label>
                  <label>Database name<input required maxLength={128} value={database.databaseName} onChange={event => updateDatabase({ databaseName: event.target.value })} /></label>
                  <label>Database user<input required maxLength={128} autoComplete="username" value={database.userName} onChange={event => updateDatabase({ userName: event.target.value })} /></label>
                  <label>Database password<input required type="password" maxLength={512} autoComplete="new-password" value={database.password} onChange={event => updateDatabase({ password: event.target.value })} /></label>
                  {database.provider === "SqlServer" && <label className="setup-check"><input type="checkbox" checked={database.trustServerCertificate} onChange={event => updateDatabase({ trustServerCertificate: event.target.checked })} /> Trust this SQL Server certificate (local testing only)</label>}
                  {database.provider === "SqlServer" && <label className="setup-check"><input type="checkbox" checked={database.encryptConnection} onChange={event => updateDatabase({ encryptConnection: event.target.checked })} /> Encrypt SQL connection (recommended; disable only for local servers without TLS)</label>}
                  <button className="button button-quiet" type="button" disabled={busy || !setupToken || !database.userName || !database.password} onClick={() => void testDatabase()}>Test database connection</button>
                  {databaseTested && <p role="status">Database connection succeeded.</p>}
                </fieldset>}
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
                {requiresDatabase && <fieldset className="setup-store-fields">
                  <legend>Store defaults</legend>
                  <label>Default storefront language<select value={defaultLanguageCode} onChange={event => setDefaultLanguageCode(event.target.value)}><option value="en">English</option><option value="bn">Bangla</option><option value="es">Spanish</option></select></label>
                  <label>Store currency<select required value={currencyCode} onChange={event => setCurrencyCode(event.target.value)}><option value="">Choose currency</option>{currencyChoices.map(code => <option value={code} key={code}>{code}</option>)}</select></label>
                </fieldset>}
                <button className="login-submit" type="submit" disabled={busy || (requiresDatabase && !databaseTested)}>{busy ? "Installing…" : requiresDatabase ? "Install store" : "Create System Admin"}</button>
              </form>
            </>
          )}
        </div>
      </section>
    </main>
  );
}
