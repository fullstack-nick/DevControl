import { FormEvent, useEffect, useMemo, useState } from "react";

type User = {
  id: string;
  email: string;
  displayName: string;
};

type Organization = {
  id: string;
  name: string;
  slug: string;
  role: Role;
  createdAt: string;
  updatedAt: string;
};

type Project = {
  id: string;
  organizationId: string;
  name: string;
  slug: string;
  description: string;
  createdAt: string;
  updatedAt: string;
};

type EnvironmentItem = {
  id: string;
  projectId: string;
  name: string;
  slug: string;
  createdAt: string;
  updatedAt: string;
};

type Member = {
  id: string;
  userId: string;
  email: string;
  displayName: string;
  role: Role;
  createdAt: string;
  updatedAt: string;
};

type Invitation = {
  id: string;
  email: string;
  role: Role;
  status: string;
  expiresAt: string;
  lastSentAt: string;
  acceptedAt?: string;
  revokedAt?: string;
};

type AuditLog = {
  id: string;
  actorEmail: string;
  action: string;
  outcome: string;
  targetType: string;
  targetId?: string;
  message: string;
  createdAt: string;
};

type ControlAction = {
  id: string;
  projectId?: string;
  environmentId?: string;
  actionType: string;
  status: string;
  targetType: string;
  targetId?: string;
  correlationId?: string;
  requestedAt: string;
  completedAt?: string;
};

type LiveApp = {
  id: string;
  projectId: string;
  projectName: string;
  projectSlug: string;
  environmentId: string;
  environmentName: string;
  environmentSlug: string;
  repo: string;
  serviceUrl: string;
  healthUrl: string;
  currentCommitSha: string;
  version: string;
  imageDigest: string;
  capabilities: string[];
  createdAt: string;
  lastRegisteredAt: string;
};

type RegistrationToken = {
  id: string;
  name: string;
  tokenPrefix: string;
  scope: string;
  projectId: string;
  projectName: string;
  projectSlug: string;
  environmentId: string;
  environmentName: string;
  environmentSlug: string;
  createdAt: string;
  lastUsedAt?: string;
  revokedAt?: string;
};

type RegistrationTokenCreateResponse = RegistrationToken & {
  secret: string;
  workflowSnippet: string;
};

type MeResponse = {
  user: User;
  organizations: Organization[];
};

type Role = "Owner" | "Admin" | "Developer" | "Viewer";

const roleOrder: Record<Role, number> = {
  Viewer: 1,
  Developer: 2,
  Admin: 3,
  Owner: 4
};

const roles: Role[] = ["Owner", "Admin", "Developer", "Viewer"];

let csrfToken: string | undefined;

async function getCsrfToken() {
  if (csrfToken) {
    return csrfToken;
  }

  const response = await fetch("/api/auth/csrf", { headers: { Accept: "application/json" } });
  if (!response.ok) {
    throw new Error("CSRF token request failed.");
  }

  const payload = (await response.json()) as { token: string };
  csrfToken = payload.token;
  return csrfToken;
}

async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const method = options.method ?? "GET";
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");

  if (method !== "GET" && method !== "HEAD") {
    headers.set("X-CSRF-TOKEN", await getCsrfToken());
    if (options.body && !headers.has("Content-Type")) {
      headers.set("Content-Type", "application/json");
    }
  }

  const response = await fetch(path, {
    ...options,
    headers
  });

  if (response.status === 401) {
    throw new AuthError();
  }

  if (!response.ok) {
    let message = `${method} ${path} failed with ${response.status}`;
    try {
      const payload = (await response.json()) as { detail?: string; title?: string };
      message = payload.detail ?? payload.title ?? message;
    } catch {
      // Keep the status-derived message.
    }
    throw new Error(message);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

class AuthError extends Error {
  constructor() {
    super("Authentication required.");
  }
}

function roleAtLeast(role: Role | undefined, required: Role) {
  return role ? roleOrder[role] >= roleOrder[required] : false;
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : "-";
}

function shortSha(value: string) {
  return value.length > 12 ? value.slice(0, 12) : value;
}

function inviteTokenFromPath() {
  const match = window.location.pathname.match(/^\/invitations\/([^/]+)$/);
  return match ? decodeURIComponent(match[1]) : undefined;
}

export default function App() {
  const [me, setMe] = useState<MeResponse | undefined>();
  const [authenticated, setAuthenticated] = useState<boolean | undefined>();
  const [selectedOrgId, setSelectedOrgId] = useState<string>("");
  const [selectedProjectId, setSelectedProjectId] = useState<string>("");
  const [selectedEnvironmentId, setSelectedEnvironmentId] = useState<string>("");
  const [projects, setProjects] = useState<Project[]>([]);
  const [environments, setEnvironments] = useState<EnvironmentItem[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [invitations, setInvitations] = useState<Invitation[]>([]);
  const [auditLogs, setAuditLogs] = useState<AuditLog[]>([]);
  const [controlActions, setControlActions] = useState<ControlAction[]>([]);
  const [liveApps, setLiveApps] = useState<LiveApp[]>([]);
  const [registrationTokens, setRegistrationTokens] = useState<RegistrationToken[]>([]);
  const [createdToken, setCreatedToken] = useState<RegistrationTokenCreateResponse | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [notice, setNotice] = useState<string | undefined>();
  const [busy, setBusy] = useState(false);
  const [orgForm, setOrgForm] = useState({ name: "", slug: "" });
  const [projectForm, setProjectForm] = useState({ name: "", slug: "", description: "" });
  const [environmentForm, setEnvironmentForm] = useState({ name: "", slug: "" });
  const [inviteForm, setInviteForm] = useState<{ email: string; role: Role }>({ email: "", role: "Developer" });
  const [tokenForm, setTokenForm] = useState({ name: "" });
  const invitationToken = useMemo(inviteTokenFromPath, []);

  const selectedOrg = me?.organizations.find((organization) => organization.id === selectedOrgId);
  const selectedProject = projects.find((project) => project.id === selectedProjectId);
  const selectedEnvironment = environments.find((environment) => environment.id === selectedEnvironmentId);
  const canManageOrg = roleAtLeast(selectedOrg?.role, "Admin");
  const canManageProjects = roleAtLeast(selectedOrg?.role, "Developer");
  const canReadAudit = roleAtLeast(selectedOrg?.role, "Admin");
  const canReadControlActions = roleAtLeast(selectedOrg?.role, "Developer");
  const filteredLiveApps = liveApps.filter((app) => {
    if (selectedEnvironmentId) {
      return app.environmentId === selectedEnvironmentId;
    }

    return selectedProjectId ? app.projectId === selectedProjectId : true;
  });

  async function loadMe(preferredOrganizationId?: string) {
    try {
      const payload = await api<MeResponse>("/api/auth/me");
      setMe(payload);
      setAuthenticated(true);
      const nextOrgId =
        preferredOrganizationId ??
        selectedOrgId ??
        payload.organizations[0]?.id ??
        "";
      setSelectedOrgId(payload.organizations.some((organization) => organization.id === nextOrgId) ? nextOrgId : payload.organizations[0]?.id ?? "");
    } catch (loadError) {
      if (loadError instanceof AuthError) {
        setAuthenticated(false);
        setMe(undefined);
      } else {
        setError(loadError instanceof Error ? loadError.message : "Failed to load session.");
      }
    }
  }

  async function refreshOrgData(organizationId: string) {
    if (!organizationId) {
      setProjects([]);
      setEnvironments([]);
      setMembers([]);
      setInvitations([]);
      setAuditLogs([]);
      setControlActions([]);
      setLiveApps([]);
      setRegistrationTokens([]);
      return;
    }

    const selected = me?.organizations.find((organization) => organization.id === organizationId);
    const [projectPayload, appPayload, memberPayload, invitationPayload, tokenPayload, auditPayload, controlActionPayload] = await Promise.all([
      api<Project[]>(`/api/organizations/${organizationId}/projects`),
      api<LiveApp[]>(`/api/organizations/${organizationId}/apps`),
      roleAtLeast(selected?.role, "Admin") ? api<Member[]>(`/api/organizations/${organizationId}/members`) : Promise.resolve([]),
      roleAtLeast(selected?.role, "Admin") ? api<Invitation[]>(`/api/organizations/${organizationId}/invitations`) : Promise.resolve([]),
      roleAtLeast(selected?.role, "Admin") ? api<RegistrationToken[]>(`/api/organizations/${organizationId}/registration-tokens`) : Promise.resolve([]),
      roleAtLeast(selected?.role, "Admin") ? api<AuditLog[]>(`/api/organizations/${organizationId}/audit-logs`) : Promise.resolve([]),
      roleAtLeast(selected?.role, "Developer") ? api<ControlAction[]>(`/api/organizations/${organizationId}/control-actions`) : Promise.resolve([])
    ]);

    setProjects(projectPayload);
    setLiveApps(appPayload);
    setMembers(memberPayload);
    setInvitations(invitationPayload);
    setRegistrationTokens(tokenPayload);
    setAuditLogs(auditPayload);
    setControlActions(controlActionPayload);
    const nextProjectId = selectedProjectId && projectPayload.some((project) => project.id === selectedProjectId)
      ? selectedProjectId
      : projectPayload[0]?.id ?? "";
    setSelectedProjectId(nextProjectId);
    setCreatedToken(undefined);
  }

  async function refreshEnvironments(organizationId: string, projectId: string) {
    if (!organizationId || !projectId) {
      setEnvironments([]);
      setSelectedEnvironmentId("");
      return;
    }

    const environmentPayload = await api<EnvironmentItem[]>(`/api/organizations/${organizationId}/projects/${projectId}/environments`);
    setEnvironments(environmentPayload);
    const nextEnvironmentId = selectedEnvironmentId && environmentPayload.some((environment) => environment.id === selectedEnvironmentId)
      ? selectedEnvironmentId
      : environmentPayload[0]?.id ?? "";
    setSelectedEnvironmentId(nextEnvironmentId);
  }

  useEffect(() => {
    void loadMe();
  }, []);

  useEffect(() => {
    if (authenticated && selectedOrgId) {
      setError(undefined);
      void refreshOrgData(selectedOrgId).catch((refreshError: unknown) => {
        setError(refreshError instanceof Error ? refreshError.message : "Failed to load organization data.");
      });
    }
  }, [authenticated, selectedOrgId, me?.organizations]);

  useEffect(() => {
    if (authenticated && selectedOrgId && selectedProjectId) {
      void refreshEnvironments(selectedOrgId, selectedProjectId).catch((refreshError: unknown) => {
        setError(refreshError instanceof Error ? refreshError.message : "Failed to load environments.");
      });
    } else {
      setEnvironments([]);
    }
  }, [authenticated, selectedOrgId, selectedProjectId]);

  async function runMutation(action: () => Promise<void>) {
    setBusy(true);
    setError(undefined);
    setNotice(undefined);
    try {
      await action();
    } catch (mutationError) {
      setError(mutationError instanceof Error ? mutationError.message : "Request failed.");
    } finally {
      setBusy(false);
    }
  }

  function login() {
    window.location.href = `/auth/login?returnUrl=${encodeURIComponent(window.location.pathname + window.location.search)}`;
  }

  async function logout() {
    await runMutation(async () => {
      await api<void>("/api/auth/logout", { method: "POST" });
      csrfToken = undefined;
      setAuthenticated(false);
      setMe(undefined);
      setSelectedOrgId("");
      setSelectedProjectId("");
      setSelectedEnvironmentId("");
    });
  }

  async function createOrganization(event: FormEvent) {
    event.preventDefault();
    await runMutation(async () => {
      const organization = await api<Organization>("/api/organizations", {
        method: "POST",
        body: JSON.stringify(orgForm)
      });
      setOrgForm({ name: "", slug: "" });
      await loadMe(organization.id);
      setNotice("Organization created.");
    });
  }

  async function createProject(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      const project = await api<Project>(`/api/organizations/${selectedOrgId}/projects`, {
        method: "POST",
        body: JSON.stringify(projectForm)
      });
      setProjectForm({ name: "", slug: "", description: "" });
      await refreshOrgData(selectedOrgId);
      setSelectedProjectId(project.id);
      setNotice("Project created.");
    });
  }

  async function createEnvironment(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId || !selectedProjectId) {
      return;
    }

    await runMutation(async () => {
      await api<EnvironmentItem>(`/api/organizations/${selectedOrgId}/projects/${selectedProjectId}/environments`, {
        method: "POST",
        body: JSON.stringify(environmentForm)
      });
      setEnvironmentForm({ name: "", slug: "" });
      await refreshEnvironments(selectedOrgId, selectedProjectId);
      setNotice("Environment created.");
    });
  }

  async function createRegistrationToken(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId || !selectedProjectId || !selectedEnvironmentId) {
      return;
    }

    await runMutation(async () => {
      const token = await api<RegistrationTokenCreateResponse>(
        `/api/organizations/${selectedOrgId}/projects/${selectedProjectId}/environments/${selectedEnvironmentId}/registration-tokens`,
        {
          method: "POST",
          body: JSON.stringify(tokenForm)
        });
      setTokenForm({ name: "" });
      await refreshOrgData(selectedOrgId);
      setCreatedToken(token);
      setNotice("Registration token created. Copy it now; it will not be shown again.");
    });
  }

  async function revokeRegistrationToken(token: RegistrationToken) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      await api(`/api/organizations/${selectedOrgId}/registration-tokens/${token.id}/revoke`, { method: "POST" });
      await refreshOrgData(selectedOrgId);
      setNotice("Registration token revoked.");
    });
  }

  async function createInvitation(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      await api<Invitation>(`/api/organizations/${selectedOrgId}/invitations`, {
        method: "POST",
        body: JSON.stringify(inviteForm)
      });
      setInviteForm({ email: "", role: "Developer" });
      await refreshOrgData(selectedOrgId);
      setNotice("Invitation sent.");
    });
  }

  async function changeMemberRole(member: Member, role: Role) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      await api(`/api/organizations/${selectedOrgId}/members/${member.id}`, {
        method: "PATCH",
        body: JSON.stringify({ role })
      });
      await refreshOrgData(selectedOrgId);
    });
  }

  async function removeMember(member: Member) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      await api<void>(`/api/organizations/${selectedOrgId}/members/${member.id}`, { method: "DELETE" });
      await refreshOrgData(selectedOrgId);
    });
  }

  async function revokeInvitation(invitation: Invitation) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      await api(`/api/organizations/${selectedOrgId}/invitations/${invitation.id}/revoke`, { method: "POST" });
      await refreshOrgData(selectedOrgId);
    });
  }

  async function resendInvitation(invitation: Invitation) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      await api(`/api/organizations/${selectedOrgId}/invitations/${invitation.id}/resend`, { method: "POST" });
      await refreshOrgData(selectedOrgId);
      setNotice("Invitation resent.");
    });
  }

  async function acceptInvitation() {
    if (!invitationToken) {
      return;
    }

    await runMutation(async () => {
      await api(`/api/invitations/${encodeURIComponent(invitationToken)}/accept`, { method: "POST" });
      window.history.replaceState(null, "", "/");
      await loadMe();
      setNotice("Invitation accepted.");
    });
  }

  if (authenticated === undefined) {
    return <main className="loading">Loading DevControl...</main>;
  }

  if (!authenticated) {
    return (
      <main className="auth-screen">
        <section className="auth-panel">
          <p className="eyebrow">DevControl Stage 2</p>
          <h1>Developer operations control plane</h1>
          <button className="primary" onClick={login}>Sign in</button>
        </section>
      </main>
    );
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">DevControl</p>
          <h1>Operations</h1>
        </div>
        <div className="identity">
          <span>{me?.user.displayName}</span>
          <span>{me?.user.email}</span>
          <button onClick={logout} disabled={busy}>Sign out</button>
        </div>
      </header>

      {error && <div className="banner error">{error}</div>}
      {notice && <div className="banner notice">{notice}</div>}

      {invitationToken && (
        <section className="invite-accept">
          <strong>Pending invitation</strong>
          <button className="primary" onClick={acceptInvitation} disabled={busy}>Accept</button>
        </section>
      )}

      {me && me.organizations.length === 0 ? (
        <section className="onboarding">
          <h2>Create organization</h2>
          <OrganizationForm form={orgForm} setForm={setOrgForm} onSubmit={createOrganization} disabled={busy} />
        </section>
      ) : (
        <>
          <section className="selectors">
            <label>
              Organization
              <select value={selectedOrgId} onChange={(event) => setSelectedOrgId(event.target.value)}>
                {me?.organizations.map((organization) => (
                  <option key={organization.id} value={organization.id}>
                    {organization.name} ({organization.role})
                  </option>
                ))}
              </select>
            </label>
            <label>
              Project
              <select value={selectedProjectId} onChange={(event) => setSelectedProjectId(event.target.value)} disabled={projects.length === 0}>
                {projects.length === 0 ? <option value="">No projects</option> : null}
                {projects.map((project) => (
                  <option key={project.id} value={project.id}>{project.name}</option>
                ))}
              </select>
            </label>
            <label>
              Environment
              <select value={selectedEnvironmentId} onChange={(event) => setSelectedEnvironmentId(event.target.value)} disabled={environments.length === 0}>
                {environments.length === 0 ? <option>No environments</option> : null}
                {environments.map((environment) => (
                  <option key={environment.id} value={environment.id}>{environment.name}</option>
                ))}
              </select>
            </label>
          </section>

          <section className="workspace-grid">
            <div className="panel">
              <div className="panel-heading">
                <h2>Projects</h2>
                <span>{projects.length}</span>
              </div>
              <ItemList
                empty="No projects"
                items={projects.map((project) => ({
                  id: project.id,
                  title: project.name,
                  meta: project.slug,
                  detail: project.description || "No description"
                }))}
              />
              {canManageProjects && (
                <form className="inline-form" onSubmit={createProject}>
                  <input required placeholder="Name" value={projectForm.name} onChange={(event) => setProjectForm({ ...projectForm, name: event.target.value })} />
                  <input placeholder="Slug" value={projectForm.slug} onChange={(event) => setProjectForm({ ...projectForm, slug: event.target.value })} />
                  <input placeholder="Description" value={projectForm.description} onChange={(event) => setProjectForm({ ...projectForm, description: event.target.value })} />
                  <button className="primary" disabled={busy}>Create</button>
                </form>
              )}
            </div>

            <div className="panel">
              <div className="panel-heading">
                <h2>Environments</h2>
                <span>{selectedProject ? environments.length : "-"}</span>
              </div>
              <ItemList
                empty={selectedProject ? "No environments" : "No project selected"}
                items={environments.map((environment) => ({
                  id: environment.id,
                  title: environment.name,
                  meta: environment.slug,
                  detail: `Updated ${formatDate(environment.updatedAt)}`
                }))}
              />
              {canManageProjects && selectedProject && (
                <form className="inline-form" onSubmit={createEnvironment}>
                  <input required placeholder="Name" value={environmentForm.name} onChange={(event) => setEnvironmentForm({ ...environmentForm, name: event.target.value })} />
                  <input placeholder="Slug" value={environmentForm.slug} onChange={(event) => setEnvironmentForm({ ...environmentForm, slug: event.target.value })} />
                  <button className="primary" disabled={busy}>Create</button>
                </form>
              )}
            </div>

            <div className="panel wide">
              <div className="panel-heading">
                <h2>Live apps</h2>
                <span>{filteredLiveApps.length}</span>
              </div>
              <div className="stack">
                {filteredLiveApps.length === 0 ? <p className="empty">No live apps registered</p> : null}
                {filteredLiveApps.map((app) => (
                  <div className="list-item app-item" key={app.id}>
                    <div>
                      <strong>{app.repo}</strong>
                      <span>{app.projectName} / {app.environmentName}</span>
                      <span>{app.version} / {shortSha(app.currentCommitSha)}</span>
                      <span>{app.imageDigest}</span>
                      <span>{app.capabilities.join(", ")}</span>
                      <span>Registered {formatDate(app.lastRegisteredAt)}</span>
                    </div>
                    <div className="actions">
                      <a href={app.serviceUrl} target="_blank" rel="noreferrer">Service</a>
                      <a href={app.healthUrl} target="_blank" rel="noreferrer">Health</a>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {canManageOrg && (
              <div className="panel wide">
                <div className="panel-heading">
                  <h2>Registration tokens</h2>
                  <span>{registrationTokens.length}</span>
                </div>
                {createdToken && (
                  <div className="secret-box">
                    <strong>Copy this token now</strong>
                    <code>{createdToken.secret}</code>
                    <pre>{createdToken.workflowSnippet}</pre>
                  </div>
                )}
                <div className="table">
                  {registrationTokens.length === 0 ? <p className="empty">No registration tokens</p> : null}
                  {registrationTokens.map((token) => (
                    <div className="table-row token-row" key={token.id}>
                      <div>
                        <strong>{token.name}</strong>
                        <span>{token.projectName} / {token.environmentName}</span>
                        <span>{token.tokenPrefix}... / {token.scope}</span>
                        <span>Last used {formatDate(token.lastUsedAt)}</span>
                      </div>
                      <span>{token.revokedAt ? "Revoked" : "Active"}</span>
                      <button onClick={() => revokeRegistrationToken(token)} disabled={busy || Boolean(token.revokedAt)}>Revoke</button>
                    </div>
                  ))}
                </div>
                {selectedEnvironment && (
                  <form className="inline-form token-form" onSubmit={createRegistrationToken}>
                    <input placeholder={`Name for ${selectedEnvironment.name}`} value={tokenForm.name} onChange={(event) => setTokenForm({ name: event.target.value })} />
                    <button className="primary" disabled={busy}>Create token</button>
                  </form>
                )}
              </div>
            )}

            {canManageOrg && (
              <div className="panel wide">
                <div className="panel-heading">
                  <h2>Members</h2>
                  <span>{members.length}</span>
                </div>
                <div className="table">
                  {members.map((member) => (
                    <div className="table-row" key={member.id}>
                      <div>
                        <strong>{member.displayName}</strong>
                        <span>{member.email}</span>
                      </div>
                      <select value={member.role} onChange={(event) => changeMemberRole(member, event.target.value as Role)} disabled={busy}>
                        {roles.map((role) => <option key={role} value={role}>{role}</option>)}
                      </select>
                      <button onClick={() => removeMember(member)} disabled={busy}>Remove</button>
                    </div>
                  ))}
                </div>
                <form className="inline-form" onSubmit={createInvitation}>
                  <input required type="email" placeholder="Email" value={inviteForm.email} onChange={(event) => setInviteForm({ ...inviteForm, email: event.target.value })} />
                  <select value={inviteForm.role} onChange={(event) => setInviteForm({ ...inviteForm, role: event.target.value as Role })}>
                    {roles.map((role) => <option key={role} value={role}>{role}</option>)}
                  </select>
                  <button className="primary" disabled={busy}>Invite</button>
                </form>
              </div>
            )}

            {canManageOrg && (
              <div className="panel">
                <div className="panel-heading">
                  <h2>Invitations</h2>
                  <span>{invitations.length}</span>
                </div>
                <div className="stack">
                  {invitations.length === 0 ? <p className="empty">No invitations</p> : null}
                  {invitations.map((invitation) => (
                    <div className="list-item" key={invitation.id}>
                      <div>
                        <strong>{invitation.email}</strong>
                        <span>{invitation.role} / {invitation.status}</span>
                        <span>Expires {formatDate(invitation.expiresAt)}</span>
                      </div>
                      {invitation.status === "Pending" && (
                        <div className="actions">
                          <button onClick={() => resendInvitation(invitation)} disabled={busy}>Resend</button>
                          <button onClick={() => revokeInvitation(invitation)} disabled={busy}>Revoke</button>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {canReadAudit && (
              <div className="panel wide">
                <div className="panel-heading">
                  <h2>Audit log</h2>
                  <span>{auditLogs.length}</span>
                </div>
                <div className="audit-list">
                  {auditLogs.length === 0 ? <p className="empty">No audit entries</p> : null}
                  {auditLogs.map((entry) => (
                    <div className="audit-row" key={entry.id}>
                      <time>{formatDate(entry.createdAt)}</time>
                      <strong>{entry.action}</strong>
                      <span>{entry.outcome}</span>
                      <p>{entry.message}</p>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {canReadControlActions && (
              <div className="panel">
                <div className="panel-heading">
                  <h2>Control actions</h2>
                  <span>{controlActions.length}</span>
                </div>
                <ItemList
                  empty="No control actions"
                  items={controlActions.map((action) => ({
                    id: action.id,
                    title: action.actionType,
                    meta: action.status,
                    detail: `${action.targetType} / ${formatDate(action.requestedAt)}`
                  }))}
                />
              </div>
            )}
          </section>
        </>
      )}
    </main>
  );
}

function OrganizationForm({
  form,
  setForm,
  onSubmit,
  disabled
}: {
  form: { name: string; slug: string };
  setForm: (form: { name: string; slug: string }) => void;
  onSubmit: (event: FormEvent) => void;
  disabled: boolean;
}) {
  return (
    <form className="inline-form" onSubmit={onSubmit}>
      <input required placeholder="Name" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
      <input placeholder="Slug" value={form.slug} onChange={(event) => setForm({ ...form, slug: event.target.value })} />
      <button className="primary" disabled={disabled}>Create</button>
    </form>
  );
}

function ItemList({
  empty,
  items
}: {
  empty: string;
  items: Array<{ id: string; title: string; meta: string; detail: string }>;
}) {
  if (items.length === 0) {
    return <p className="empty">{empty}</p>;
  }

  return (
    <div className="stack">
      {items.map((item) => (
        <div className="list-item" key={item.id}>
          <div>
            <strong>{item.title}</strong>
            <span>{item.meta}</span>
            <span>{item.detail}</span>
          </div>
        </div>
      ))}
    </div>
  );
}
