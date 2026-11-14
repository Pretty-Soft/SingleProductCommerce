import { FormEvent, useEffect, useState } from "react";
import AdminCatalogEditor from "./AdminCatalogEditor";
import { adminRequest, AdminRole, AdminUser, refreshCsrfToken } from "./adminApi";

type RoleChanges = Record<string, string[]>;

export default function AdminApp() {
  const [currentUser, setCurrentUser] = useState<AdminUser | null>(null);
  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [roleChanges, setRoleChanges] = useState<RoleChanges>({});
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function loadAdminData() {
    const [loadedRoles, loadedUsers] = await Promise.all([
      adminRequest<AdminRole[]>("/api/v1/admin/roles"),
      adminRequest<AdminUser[]>("/api/v1/admin/users")
    ]);
    setRoles(loadedRoles);
    setUsers(loadedUsers);
    setRoleChanges(Object.fromEntries(loadedUsers.map((user) => [user.id, user.roles])));
  }

  async function loadSession() {
    await refreshCsrfToken();
    const user = await adminRequest<AdminUser>("/api/v1/admin/me");
    setCurrentUser(user);
    if (!user.mustChangePassword && user.roles.includes("SystemAdmin")) {
      await loadAdminData();
    }
  }

  useEffect(() => {
    let active = true;
    void loadSession().catch((reason: unknown) => {
      if (active) {
        setCurrentUser(null);
        if (reason instanceof TypeError) {
          setError("The API could not be reached. Check that the HTTPS API and its local certificate are ready.");
        }
      }
    });
    return () => {
      active = false;
    };
  }, []);

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await refreshCsrfToken();
      await adminRequest("/api/v1/admin/login", {
        method: "POST",
        body: JSON.stringify({ email, password })
      });
      await loadSession();
      setPassword("");
      setMessage("Signed in successfully.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Sign-in failed.");
    } finally {
      setBusy(false);
    }
  }

  async function handleCreateUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await adminRequest<AdminUser>("/api/v1/admin/users", {
        method: "POST",
        body: JSON.stringify({ email, temporaryPassword: password, roleNames: selectedRoles })
      });
      setEmail("");
      setPassword("");
      setSelectedRoles([]);
      await loadAdminData();
      setMessage("Account created. The user must change this temporary password at first sign-in.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "The account could not be created.");
    } finally {
      setBusy(false);
    }
  }

  async function handleSaveRoles(userId: string) {
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await adminRequest<void>(`/api/v1/admin/users/${userId}/roles`, {
        method: "PUT",
        body: JSON.stringify({ roleNames: roleChanges[userId] ?? [] })
      });
      try {
        await loadSession();
      } catch {
        setCurrentUser(null);
        setUsers([]);
        setRoles([]);
      }
      setMessage("Role assignment saved.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Roles could not be saved.");
    } finally {
      setBusy(false);
    }
  }

  async function handlePasswordChange(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await adminRequest<void>("/api/v1/admin/password", {
        method: "POST",
        body: JSON.stringify({ currentPassword, newPassword })
      });
      setCurrentPassword("");
      setNewPassword("");
      await loadSession();
      setMessage("Password updated.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Password could not be changed.");
    } finally {
      setBusy(false);
    }
  }

  async function handleLogout() {
    setBusy(true);
    try {
      await adminRequest<void>("/api/v1/admin/logout", { method: "POST" });
      setCurrentUser(null);
      setUsers([]);
      setRoles([]);
      await refreshCsrfToken();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Sign-out failed.");
    } finally {
      setBusy(false);
    }
  }

  const isSystemAdmin = currentUser?.roles.includes("SystemAdmin") ?? false;

  return (
    <main className="admin-page">
      <header className="admin-header">
        <a className="brand" href="/" aria-label="OneCart storefront">
          <span className="brand-mark">O</span>
          <span>OneCart Admin</span>
        </a>
        {currentUser && (
          <div className="admin-account">
            <span>{currentUser.email}</span>
            <button className="button button-quiet" onClick={handleLogout} disabled={busy}>
              Sign out
            </button>
          </div>
        )}
      </header>

      <section className="admin-content">
        {!currentUser ? (
          <form className="admin-card login-card" onSubmit={handleLogin}>
            <p className="eyebrow">Secure access</p>
            <h1>Administrator sign in</h1>
            <p className="admin-muted">Use the email and password created for your account.</p>
            <label>
              Email address
              <input type="email" autoComplete="username" required value={email} onChange={(event) => setEmail(event.target.value)} />
            </label>
            <label>
              Password
              <input type="password" autoComplete="current-password" required value={password} onChange={(event) => setPassword(event.target.value)} />
            </label>
            <button className="button" type="submit" disabled={busy}>{busy ? "Signing in…" : "Sign in"}</button>
          </form>
        ) : currentUser.mustChangePassword ? (
          <form className="admin-card login-card" onSubmit={handlePasswordChange}>
            <p className="eyebrow">First sign in</p>
            <h1>Change your temporary password</h1>
            <label>
              Temporary password
              <input type="password" autoComplete="current-password" required value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} />
            </label>
            <label>
              New password
              <input type="password" autoComplete="new-password" minLength={12} required value={newPassword} onChange={(event) => setNewPassword(event.target.value)} />
            </label>
            <p className="admin-muted">Use at least 12 characters with uppercase, lowercase, a number, and a symbol.</p>
            <button className="button" type="submit" disabled={busy}>Update password</button>
          </form>
        ) : !isSystemAdmin ? (
          <section className="admin-card">
            <p className="eyebrow">Account ready</p>
            <h1>Welcome</h1>
            <p className="admin-muted">Your account has no enabled administration area yet. Ask a System Admin to confirm your role assignment.</p>
          </section>
        ) : (
          <>
            <div className="admin-title-row">
              <div>
                <p className="eyebrow">System administration</p>
                <h1>Users and roles</h1>
                <p className="admin-muted">Only a System Admin can create accounts or change role assignments.</p>
              </div>
              <span className="admin-badge">System Admin</span>
            </div>

            <section className="admin-card">
              <div className="section-heading">
                <div>
                  <h2>Create account</h2>
                  <p className="admin-muted">The user must replace the temporary password at first sign-in.</p>
                </div>
              </div>
              <form className="user-form" onSubmit={handleCreateUser}>
                <label>
                  Email address
                  <input type="email" autoComplete="off" required value={email} onChange={(event) => setEmail(event.target.value)} />
                </label>
                <label>
                  Temporary password
                  <input type="password" autoComplete="new-password" minLength={12} required value={password} onChange={(event) => setPassword(event.target.value)} />
                </label>
                <fieldset>
                  <legend>Assign role</legend>
                  <div className="role-options">
                    {roles.map((role) => (
                      <label className="role-option" key={role.id}>
                        <input
                          type="checkbox"
                          checked={selectedRoles.includes(role.name)}
                          onChange={(event) => setSelectedRoles((current) => event.target.checked
                            ? [...current, role.name]
                            : current.filter((name) => name !== role.name))}
                        />
                        <span>{role.name}</span>
                      </label>
                    ))}
                  </div>
                </fieldset>
                <button className="button" type="submit" disabled={busy || selectedRoles.length === 0}>
                  Create user
                </button>
              </form>
            </section>

            <AdminCatalogEditor />

            <section className="admin-card">
              <div className="section-heading">
                <div>
                  <h2>Existing users</h2>
                  <p className="admin-muted">Assign one or more built-in roles. The last System Admin cannot be demoted.</p>
                </div>
                <span className="user-count">{users.length} accounts</span>
              </div>
              <div className="user-list">
                {users.map((user) => (
                  <article className="user-row" key={user.id}>
                    <div className="user-summary">
                      <strong>{user.email}</strong>
                      <span>{user.mustChangePassword ? "Must change password" : user.roles.join(", ")}</span>
                    </div>
                    <div className="role-editor">
                      {roles.map((role) => (
                        <label className="role-option" key={role.id}>
                          <input
                            type="checkbox"
                            checked={(roleChanges[user.id] ?? user.roles).includes(role.name)}
                            onChange={(event) => setRoleChanges((current) => {
                              const selected = current[user.id] ?? user.roles;
                              return {
                                ...current,
                                [user.id]: event.target.checked
                                  ? [...selected, role.name]
                                  : selected.filter((name) => name !== role.name)
                              };
                            })}
                          />
                          <span>{role.name}</span>
                        </label>
                      ))}
                    </div>
                    <button className="button button-quiet" onClick={() => void handleSaveRoles(user.id)} disabled={busy || (roleChanges[user.id] ?? user.roles).length === 0}>
                      Save roles
                    </button>
                  </article>
                ))}
                {users.length === 0 && <p className="admin-muted">No accounts yet.</p>}
              </div>
            </section>
          </>
        )}

        {message && <p className="admin-message" role="status">{message}</p>}
        {error && <p className="admin-error" role="alert">{error}</p>}
      </section>
      <footer className="admin-footer">OneCart Commerce · Secure administrator access</footer>
    </main>
  );
}
