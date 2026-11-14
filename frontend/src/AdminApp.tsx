import { FormEvent, useEffect, useState } from "react";
import AdminCatalogEditor from "./AdminCatalogEditor";
import AdminOrders from "./AdminOrders";
import { adminRequest, AdminAuditEvent, AdminRole, AdminUser, refreshCsrfToken } from "./adminApi";

type RoleChanges = Record<string, string[]>;

export default function AdminApp() {
  const [currentUser, setCurrentUser] = useState<AdminUser | null>(null);
  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [auditEvents, setAuditEvents] = useState<AdminAuditEvent[]>([]);
  const [roleChanges, setRoleChanges] = useState<RoleChanges>({});
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [activeSection, setActiveSection] = useState("overview");
  const [sidebarOpen, setSidebarOpen] = useState(false);

  async function loadAdminData() {
    const [loadedRoles, loadedUsers, loadedAuditEvents] = await Promise.all([
      adminRequest<AdminRole[]>("/api/v1/admin/roles"),
      adminRequest<AdminUser[]>("/api/v1/admin/users"),
      adminRequest<AdminAuditEvent[]>("/api/v1/admin/audit")
    ]);
    setRoles(loadedRoles);
    setUsers(loadedUsers);
    setAuditEvents(loadedAuditEvents);
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
      setAuditEvents([]);
      await refreshCsrfToken();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Sign-out failed.");
    } finally {
      setBusy(false);
    }
  }

  const isSystemAdmin = currentUser?.roles.includes("SystemAdmin") ?? false;
  const hasWorkspace = Boolean(currentUser && !currentUser.mustChangePassword && isSystemAdmin);

  function navigateSection(sectionId: string) {
    setActiveSection(sectionId);
    setSidebarOpen(false);
    document.getElementById(sectionId)?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  return (
    <main className={`admin-page${hasWorkspace ? " admin-workspace" : ""}`}>
      {hasWorkspace && (
        <>
          <aside className={`admin-sidebar${sidebarOpen ? " is-open" : ""}`} aria-label="Administration menu">
            <a className="admin-sidebar-brand" href="/" aria-label="OneCart storefront">
              <span className="admin-sidebar-mark">O</span>
              <span><strong>OneCart</strong><small>Commerce Admin</small></span>
            </a>
            <nav className="admin-sidebar-nav" aria-label="Admin sections">
              <p className="admin-nav-label">STORE WORKSPACE</p>
              {adminMenuItems.map((item) => (
                <button
                  className={`admin-nav-item${activeSection === item.id ? " active" : ""}`}
                  type="button"
                  key={item.id}
                  aria-current={activeSection === item.id ? "page" : undefined}
                  onClick={() => navigateSection(item.id)}
                >
                  <AdminMenuIcon name={item.id} />
                  <span>{item.label}</span>
                  {activeSection === item.id && <span className="admin-nav-active-dot" aria-hidden="true" />}
                </button>
              ))}
            </nav>
            <div className="admin-sidebar-footer"><span aria-hidden="true">●</span> Secure admin workspace</div>
          </aside>
          {sidebarOpen && <button className="admin-sidebar-backdrop" type="button" aria-label="Close admin menu" onClick={() => setSidebarOpen(false)} />}
        </>
      )}

      <div className={hasWorkspace ? "admin-workspace-main" : "admin-auth-main"}>
      <header className={`admin-header${hasWorkspace ? " admin-workspace-header" : ""}`}>
        {hasWorkspace ? (
          <>
            <button className="admin-menu-toggle" type="button" aria-expanded={sidebarOpen} onClick={() => setSidebarOpen((open) => !open)}>
              <span aria-hidden="true">☰</span> Menu
            </button>
            <div className="admin-page-heading">
              <p>ONECART ADMIN</p>
              <strong>{adminMenuItems.find((item) => item.id === activeSection)?.label ?? "Overview"}</strong>
            </div>
          </>
        ) : (
          <a className="brand" href="/" aria-label="OneCart storefront">
            <span className="brand-mark">O</span>
            <span>OneCart Admin</span>
          </a>
        )}
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
            <div className="admin-title-row" id="overview">
              <div>
                <p className="eyebrow">STORE WORKSPACE</p>
                <h1>Overview</h1>
                <p className="admin-muted">Manage your product, customer orders, and administrator access.</p>
              </div>
              <span className="admin-badge">System Admin</span>
            </div>

            <div className="admin-overview-links" aria-label="Workspace shortcuts">
              {adminMenuItems.filter((item) => item.id !== "overview").map((item) => (
                <button className="admin-overview-link" type="button" key={item.id} onClick={() => navigateSection(item.id)}>
                  <AdminMenuIcon name={item.id} />
                  <span><strong>{item.label}</strong><small>{item.description}</small></span>
                  <span aria-hidden="true">→</span>
                </button>
              ))}
            </div>

            <div id="accounts" className="admin-nav-target">
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

            </div>

            <div id="catalog" className="admin-nav-target"><AdminCatalogEditor /></div>

            <div id="orders" className="admin-nav-target"><AdminOrders /></div>

            <section className="admin-card admin-nav-target" id="audit">
              <div className="section-heading">
                <div>
                  <h2>Audit history</h2>
                  <p className="admin-muted">Recent account, role, and catalog changes performed by administrators.</p>
                </div>
                <span className="user-count">Latest {auditEvents.length}</span>
              </div>
              <div className="audit-list">
                {auditEvents.map((event) => (
                  <article className="audit-row" key={event.id}>
                    <div className="audit-summary">
                      <div className="audit-heading">
                        <strong>{formatAuditAction(event.action)}</strong>
                        <time dateTime={event.createdAtUtc}>{formatAuditDate(event.createdAtUtc)}</time>
                      </div>
                      <p><span>Actor</span> {event.actorEmail}</p>
                      <p><span>Target</span> {event.target}</p>
                      <details>
                        <summary>View recorded details</summary>
                        <pre>{formatAuditDetails(event.details)}</pre>
                      </details>
                    </div>
                  </article>
                ))}
                {auditEvents.length === 0 && <p className="admin-muted">No administrative changes recorded yet.</p>}
              </div>
            </section>
          </>
        )}

        {message && <p className="admin-message" role="status">{message}</p>}
        {error && <p className="admin-error" role="alert">{error}</p>}
      </section>
      <footer className="admin-footer">OneCart Commerce · Secure administrator access</footer>
      </div>
    </main>
  );
}

const adminMenuItems = [
  { id: "overview", label: "Overview", description: "Store workspace summary" },
  { id: "catalog", label: "Catalog", description: "Product, pricing, and storefront" },
  { id: "orders", label: "Orders", description: "Review and update customer orders" },
  { id: "accounts", label: "Accounts & roles", description: "Manage administrator access" },
  { id: "audit", label: "Audit history", description: "Review administrator activity" }
];

function AdminMenuIcon({ name }: { name: string }) {
  return (
    <svg className="admin-nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {name === "overview" ? <><path d="m3 10 9-7 9 7" /><path d="M5 9v12h14V9" /><path d="M9 21v-7h6v7" /></>
        : name === "catalog" ? <><path d="M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z" /></>
          : name === "orders" ? <><path d="M6 3h9l4 4v14H6z" /><path d="M14 3v5h5M9 12h7M9 16h7" /></>
            : name === "accounts" ? <><circle cx="9" cy="8" r="3" /><path d="M3.5 20v-1.5a5.5 5.5 0 0 1 11 0V20zM16 5.5a3 3 0 0 1 0 5.8M18 15a4.5 4.5 0 0 1 2.5 4v1" /></>
              : <><path d="M4 5h16M4 12h16M4 19h16" /><circle cx="7" cy="5" r="1" /><circle cx="7" cy="12" r="1" /><circle cx="7" cy="19" r="1" /></>}
    </svg>
  );
}

function formatAuditAction(action: string): string {
  return action.replace(/[._]/g, " ");
}

function formatAuditDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function formatAuditDetails(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}
