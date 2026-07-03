import { FormEvent, useEffect, useMemo, useState } from "react";

type User = {
  id: string;
  email: string;
  displayName: string;
};

type PublicConfig = {
  observabilityUrl?: string;
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
  gitHubRunId?: number;
  gitHubRunUrl: string;
  createdAt: string;
  lastRegisteredAt: string;
};

type LiveAppDeployment = {
  id: string;
  liveAppId: string;
  repo: string;
  serviceUrl: string;
  healthUrl: string;
  commitSha: string;
  version: string;
  imageDigest: string;
  capabilities: string[];
  gitHubRunId?: number;
  gitHubRunUrl: string;
  registeredAt: string;
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

type GitHubWorkflowInfo = {
  id: number;
  name: string;
  path: string;
  state: string;
};

type GitHubRepositoryResolution = {
  fullName: string;
  defaultBranch: string;
  htmlUrl: string;
  installationId: number;
  installationAccount: string;
  workflows: GitHubWorkflowInfo[];
};

type GitHubRepoConnection = {
  id: string;
  liveAppId?: string;
  projectId: string;
  projectName: string;
  projectSlug: string;
  environmentId: string;
  environmentName: string;
  environmentSlug: string;
  repo: string;
  defaultBranch: string;
  workflowPath: string;
  workflowName: string;
  jobId: string;
  capabilities: string[];
  createdAt: string;
  updatedAt: string;
};

type GitHubOnboardingPullRequest = {
  id: string;
  repoConnectionId: string;
  projectId: string;
  projectName: string;
  projectSlug: string;
  environmentId: string;
  environmentName: string;
  environmentSlug: string;
  repo: string;
  workflowPath: string;
  baseBranch: string;
  headBranch: string;
  pullRequestNumber: number;
  pullRequestUrl: string;
  status: string;
  error: string;
  createdAt: string;
  updatedAt: string;
  mergedAt?: string;
  closedAt?: string;
};

type GitHubWorkflowDispatch = {
  id: string;
  controlActionId: string;
  controlActionStatus: string;
  liveAppId: string;
  liveAppRepo: string;
  action: string;
  repo: string;
  workflowPath: string;
  ref: string;
  gitHubRunId?: number;
  runUrl: string;
  status: string;
  conclusion: string;
  requestedAt: string;
  updatedAt: string;
  completedAt?: string;
};

type ApiKey = {
  id: string;
  name: string;
  keyPrefix: string;
  scopes: string[];
  rateLimitPerMinute: number;
  projectId: string;
  projectName: string;
  projectSlug: string;
  environmentId: string;
  environmentName: string;
  environmentSlug: string;
  createdAt: string;
  lastUsedAt?: string;
  revokedAt?: string;
  rotatedAt?: string;
  rotatedFromApiKeyId?: string;
  rotatedToApiKeyId?: string;
  totalRequestCount: number;
  failureCount: number;
  averageLatencyMilliseconds: number;
  rateLimitHitCount: number;
};

type ApiKeyCreateResponse = ApiKey & {
  secret: string;
};

type FeatureFlag = {
  id: string;
  key: string;
  name: string;
  description: string;
  kind: "FeatureFlag" | "KillSwitch";
  enabled: boolean;
  projectId: string;
  projectName: string;
  projectSlug: string;
  environmentId: string;
  environmentName: string;
  environmentSlug: string;
  createdAt: string;
  updatedAt: string;
  lastChangedAt: string;
};

type FeatureFlagChange = {
  id: string;
  featureFlagId: string;
  oldValue: boolean;
  newValue: boolean;
  reason: string;
  changedByEmail: string;
  changedAt: string;
};

type WebhookEndpoint = {
  id: string;
  name: string;
  url: string;
  secretPrefix: string;
  eventTypes: string[];
  projectId: string;
  projectName: string;
  projectSlug: string;
  environmentId: string;
  environmentName: string;
  environmentSlug: string;
  isPaused: boolean;
  createdAt: string;
  updatedAt: string;
  pausedAt?: string;
  lastDeliveryAt?: string;
  lastSuccessAt?: string;
  lastFailureAt?: string;
};

type WebhookEndpointCreateResponse = WebhookEndpoint & {
  secret: string;
};

type WebhookDelivery = {
  id: string;
  endpointId: string;
  eventId: string;
  eventType: string;
  resourceType: string;
  resourceId?: string;
  status: string;
  attemptCount: number;
  maxAttempts: number;
  nextAttemptAt?: string;
  lastAttemptAt?: string;
  completedAt?: string;
  lastStatusCode?: number;
  lastError: string;
  lastResponsePreview: string;
  lastResponseTruncated: boolean;
  createdAt: string;
};

type Monitor = {
  id: string;
  liveAppId?: string;
  name: string;
  url: string;
  isManagedFromLiveApp: boolean;
  isPaused: boolean;
  currentStatus: string;
  intervalSeconds: number;
  timeoutSeconds: number;
  slowThresholdMilliseconds: number;
  failureThreshold: number;
  recoveryThreshold: number;
  consecutiveFailures: number;
  consecutiveRecoveries: number;
  projectId: string;
  projectName: string;
  projectSlug: string;
  environmentId: string;
  environmentName: string;
  environmentSlug: string;
  nextCheckAt: string;
  lastCheckedAt?: string;
  lastSuccessAt?: string;
  lastFailureAt?: string;
  createdAt: string;
  updatedAt: string;
};

type MonitorCheck = {
  id: string;
  monitorId: string;
  status: string;
  succeeded: boolean;
  statusCode?: number;
  resultKind: string;
  durationMilliseconds: number;
  error: string;
  responsePreview: string;
  responseTruncated: boolean;
  checkedAt: string;
};

type Incident = {
  id: string;
  title: string;
  status: string;
  summary: string;
  rootCauseSummary: string;
  postmortemDraft: string;
  projectId: string;
  projectName: string;
  projectSlug: string;
  environmentId: string;
  environmentName: string;
  environmentSlug: string;
  createdAt: string;
  updatedAt: string;
  resolvedAt?: string;
};

type IncidentUpdate = {
  id: string;
  incidentId: string;
  status: string;
  visibility: string;
  message: string;
  createdByEmail: string;
  createdAt: string;
};

type StatusRelease = {
  id: string;
  title: string;
  version: string;
  body: string;
  status: string;
  projectId: string;
  projectName: string;
  projectSlug: string;
  environmentId: string;
  environmentName: string;
  environmentSlug: string;
  createdAt: string;
  updatedAt: string;
  publishedAt?: string;
};

type PublicStatusPage = {
  organizationName: string;
  organizationSlug: string;
  projectName: string;
  projectSlug: string;
  overallStatus: string;
  environments: Array<{ name: string; slug: string }>;
  monitors: Array<{
    id: string;
    name: string;
    environmentName: string;
    environmentSlug: string;
    status: string;
    lastCheckedAt?: string;
    uptimePercentLast24Hours: number;
  }>;
  incidents: Array<{
    id: string;
    title: string;
    status: string;
    summary: string;
    environmentName: string;
    environmentSlug: string;
    createdAt: string;
    resolvedAt?: string;
    updates: IncidentUpdate[];
  }>;
  releases: Array<{
    id: string;
    title: string;
    version: string;
    body: string;
    environmentName: string;
    environmentSlug: string;
    publishedAt: string;
  }>;
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

const webhookEventTypes = [
  "webhook.test",
  "app.registered",
  "api_key.created",
  "api_key.revoked",
  "api_key.rotated",
  "feature_flag.created",
  "feature_flag.updated",
  "monitor.down",
  "monitor.recovered",
  "incident.created",
  "incident.updated",
  "incident.resolved",
  "release.published"
];

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
      const payload = (await response.json()) as { detail?: string; title?: string; errors?: string[] };
      message = payload.errors?.join(" ") ?? payload.detail ?? payload.title ?? message;
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

function formatLatency(value: number) {
  return `${value.toFixed(value >= 10 ? 0 : 1)} ms`;
}

function inviteTokenFromPath() {
  const match = window.location.pathname.match(/^\/invitations\/([^/]+)$/);
  return match ? decodeURIComponent(match[1]) : undefined;
}

function statusPageFromPath() {
  const match = window.location.pathname.match(/^\/status\/([^/]+)\/([^/]+)$/);
  return match
    ? {
        organizationSlug: decodeURIComponent(match[1]),
        projectSlug: decodeURIComponent(match[2])
      }
    : undefined;
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
  const [liveAppDeployments, setLiveAppDeployments] = useState<Record<string, LiveAppDeployment[]>>({});
  const [registrationTokens, setRegistrationTokens] = useState<RegistrationToken[]>([]);
  const [gitHubRepoConnections, setGitHubRepoConnections] = useState<GitHubRepoConnection[]>([]);
  const [gitHubOnboardingPullRequests, setGitHubOnboardingPullRequests] = useState<GitHubOnboardingPullRequest[]>([]);
  const [gitHubWorkflowDispatches, setGitHubWorkflowDispatches] = useState<GitHubWorkflowDispatch[]>([]);
  const [gitHubResolution, setGitHubResolution] = useState<GitHubRepositoryResolution | undefined>();
  const [gitHubManualSnippet, setGitHubManualSnippet] = useState<string | undefined>();
  const [apiKeys, setApiKeys] = useState<ApiKey[]>([]);
  const [featureFlags, setFeatureFlags] = useState<FeatureFlag[]>([]);
  const [featureFlagChanges, setFeatureFlagChanges] = useState<FeatureFlagChange[]>([]);
  const [webhookEndpoints, setWebhookEndpoints] = useState<WebhookEndpoint[]>([]);
  const [webhookDeliveries, setWebhookDeliveries] = useState<WebhookDelivery[]>([]);
  const [monitors, setMonitors] = useState<Monitor[]>([]);
  const [monitorChecks, setMonitorChecks] = useState<MonitorCheck[]>([]);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [incidentUpdates, setIncidentUpdates] = useState<IncidentUpdate[]>([]);
  const [releases, setReleases] = useState<StatusRelease[]>([]);
  const [publicStatus, setPublicStatus] = useState<PublicStatusPage | undefined>();
  const [publicStatusError, setPublicStatusError] = useState<string | undefined>();
  const [observabilityUrl, setObservabilityUrl] = useState<string | undefined>();
  const [historyFlagId, setHistoryFlagId] = useState<string>("");
  const [selectedWebhookEndpointId, setSelectedWebhookEndpointId] = useState<string>("");
  const [selectedMonitorId, setSelectedMonitorId] = useState<string>("");
  const [selectedIncidentId, setSelectedIncidentId] = useState<string>("");
  const [createdToken, setCreatedToken] = useState<RegistrationTokenCreateResponse | undefined>();
  const [createdApiKey, setCreatedApiKey] = useState<ApiKeyCreateResponse | undefined>();
  const [createdWebhookEndpoint, setCreatedWebhookEndpoint] = useState<WebhookEndpointCreateResponse | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [notice, setNotice] = useState<string | undefined>();
  const [busy, setBusy] = useState(false);
  const [orgForm, setOrgForm] = useState({ name: "", slug: "" });
  const [projectForm, setProjectForm] = useState({ name: "", slug: "", description: "" });
  const [environmentForm, setEnvironmentForm] = useState({ name: "", slug: "" });
  const [inviteForm, setInviteForm] = useState<{ email: string; role: Role }>({ email: "", role: "Developer" });
  const [tokenForm, setTokenForm] = useState({ name: "" });
  const [gitHubForm, setGitHubForm] = useState({
    repo: "",
    workflowPath: "",
    jobId: "deploy",
    serviceUrlExpression: "$SERVICE_URL",
    healthUrlExpression: "$SERVICE_URL/health",
    versionExpression: "$REGISTER_VERSION",
    imageDigestExpression: "$REGISTER_IMAGE_DIGEST",
    capabilities: "health,deployment-events,deploy,redeploy,rollback"
  });
  const [appActionReasons, setAppActionReasons] = useState<Record<string, string>>({});
  const [rollbackTargets, setRollbackTargets] = useState<Record<string, string>>({});
  const [apiKeyForm, setApiKeyForm] = useState({ name: "", scope: "sample:read", rateLimitPerMinute: "10" });
  const [flagForm, setFlagForm] = useState({ key: "", name: "", description: "", kind: "FeatureFlag", enabled: false, reason: "" });
  const [flagReasons, setFlagReasons] = useState<Record<string, string>>({});
  const [monitorForm, setMonitorForm] = useState({
    name: "",
    url: "",
    intervalSeconds: "300",
    timeoutSeconds: "5",
    slowThresholdMilliseconds: "2000",
    failureThreshold: "1",
    recoveryThreshold: "1"
  });
  const [incidentForm, setIncidentForm] = useState({ title: "", summary: "", message: "", private: false });
  const [incidentUpdateForm, setIncidentUpdateForm] = useState({ message: "", status: "Investigating", private: false });
  const [releaseForm, setReleaseForm] = useState({ title: "", version: "", body: "" });
  const [webhookForm, setWebhookForm] = useState({
    name: "",
    url: "",
    eventTypes: Object.fromEntries(webhookEventTypes.map((eventType) => [eventType, eventType === "webhook.test"])) as Record<string, boolean>
  });
  const invitationToken = useMemo(inviteTokenFromPath, []);
  const statusPage = useMemo(statusPageFromPath, []);

  const selectedOrg = me?.organizations.find((organization) => organization.id === selectedOrgId);
  const selectedProject = projects.find((project) => project.id === selectedProjectId);
  const selectedEnvironment = environments.find((environment) => environment.id === selectedEnvironmentId);
  const canManageOrg = roleAtLeast(selectedOrg?.role, "Admin");
  const canManageProjects = roleAtLeast(selectedOrg?.role, "Developer");
  const canReadAudit = roleAtLeast(selectedOrg?.role, "Admin");
  const canReadControlActions = roleAtLeast(selectedOrg?.role, "Developer");
  const selectedEnvironmentIsProduction = selectedEnvironment?.slug.toLowerCase() === "production";
  const canManageFlags = roleAtLeast(selectedOrg?.role, selectedEnvironmentIsProduction ? "Admin" : "Developer");
  const selectedHistoryFlag = featureFlags.find((flag) => flag.id === historyFlagId);
  const filteredLiveApps = liveApps.filter((app) => {
    if (selectedEnvironmentId) {
      return app.environmentId === selectedEnvironmentId;
    }

    return selectedProjectId ? app.projectId === selectedProjectId : true;
  });
  const filteredGitHubRepoConnections = gitHubRepoConnections.filter((connection) => {
    if (selectedEnvironmentId) {
      return connection.environmentId === selectedEnvironmentId;
    }

    return selectedProjectId ? connection.projectId === selectedProjectId : true;
  });
  const filteredGitHubOnboardingPullRequests = gitHubOnboardingPullRequests.filter((pullRequest) => {
    if (selectedEnvironmentId) {
      return pullRequest.environmentId === selectedEnvironmentId;
    }

    return selectedProjectId ? pullRequest.projectId === selectedProjectId : true;
  });
  const filteredGitHubWorkflowDispatches = gitHubWorkflowDispatches.filter((dispatch) => {
    const app = liveApps.find((candidate) => candidate.id === dispatch.liveAppId);
    if (!app) {
      return true;
    }

    if (selectedEnvironmentId) {
      return app.environmentId === selectedEnvironmentId;
    }

    return selectedProjectId ? app.projectId === selectedProjectId : true;
  });
  const filteredApiKeys = apiKeys.filter((apiKey) => {
    if (selectedEnvironmentId) {
      return apiKey.environmentId === selectedEnvironmentId;
    }

    return selectedProjectId ? apiKey.projectId === selectedProjectId : true;
  });
  const filteredWebhookEndpoints = webhookEndpoints.filter((endpoint) => {
    if (selectedEnvironmentId) {
      return endpoint.environmentId === selectedEnvironmentId;
    }

    return selectedProjectId ? endpoint.projectId === selectedProjectId : true;
  });
  const filteredMonitors = monitors.filter((monitor) => {
    if (selectedEnvironmentId) {
      return monitor.environmentId === selectedEnvironmentId;
    }

    return selectedProjectId ? monitor.projectId === selectedProjectId : true;
  });
  const filteredIncidents = incidents.filter((incident) => {
    if (selectedEnvironmentId) {
      return incident.environmentId === selectedEnvironmentId;
    }

    return selectedProjectId ? incident.projectId === selectedProjectId : true;
  });
  const filteredReleases = releases.filter((release) => {
    if (selectedEnvironmentId) {
      return release.environmentId === selectedEnvironmentId;
    }

    return selectedProjectId ? release.projectId === selectedProjectId : true;
  });
  const selectedWebhookEndpoint = webhookEndpoints.find((endpoint) => endpoint.id === selectedWebhookEndpointId);
  const selectedMonitor = monitors.find((monitor) => monitor.id === selectedMonitorId);
  const selectedIncident = incidents.find((incident) => incident.id === selectedIncidentId);
  const canManageMonitors = roleAtLeast(selectedOrg?.role, selectedEnvironmentIsProduction ? "Admin" : "Developer");
  const canManageIncidents = roleAtLeast(selectedOrg?.role, "Developer");
  const canPublishReleases = roleAtLeast(selectedOrg?.role, "Admin");
  const statusPagePath = selectedOrg && selectedProject ? `/status/${selectedOrg.slug}/${selectedProject.slug}${selectedEnvironment ? `?environment=${selectedEnvironment.slug}` : ""}` : "";

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
      setLiveAppDeployments({});
      setRegistrationTokens([]);
      setGitHubRepoConnections([]);
      setGitHubOnboardingPullRequests([]);
      setGitHubWorkflowDispatches([]);
      setGitHubResolution(undefined);
      setGitHubManualSnippet(undefined);
      setApiKeys([]);
      setWebhookEndpoints([]);
      setWebhookDeliveries([]);
      setMonitors([]);
      setMonitorChecks([]);
      setIncidents([]);
      setIncidentUpdates([]);
      setReleases([]);
      setFeatureFlags([]);
      setFeatureFlagChanges([]);
      setHistoryFlagId("");
      setSelectedWebhookEndpointId("");
      setSelectedMonitorId("");
      setSelectedIncidentId("");
      return;
    }

    const selected = me?.organizations.find((organization) => organization.id === organizationId);
    const [projectPayload, appPayload, repoConnectionPayload, onboardingPullRequestPayload, workflowDispatchPayload, memberPayload, invitationPayload, tokenPayload, apiKeyPayload, webhookPayload, monitorPayload, incidentPayload, releasePayload, auditPayload, controlActionPayload] = await Promise.all([
      api<Project[]>(`/api/organizations/${organizationId}/projects`),
      api<LiveApp[]>(`/api/organizations/${organizationId}/apps`),
      api<GitHubRepoConnection[]>(`/api/organizations/${organizationId}/github/repo-connections`),
      api<GitHubOnboardingPullRequest[]>(`/api/organizations/${organizationId}/github/onboarding-prs`),
      api<GitHubWorkflowDispatch[]>(`/api/organizations/${organizationId}/github/workflow-dispatches`),
      roleAtLeast(selected?.role, "Admin") ? api<Member[]>(`/api/organizations/${organizationId}/members`) : Promise.resolve([]),
      roleAtLeast(selected?.role, "Admin") ? api<Invitation[]>(`/api/organizations/${organizationId}/invitations`) : Promise.resolve([]),
      roleAtLeast(selected?.role, "Admin") ? api<RegistrationToken[]>(`/api/organizations/${organizationId}/registration-tokens`) : Promise.resolve([]),
      roleAtLeast(selected?.role, "Admin") ? api<ApiKey[]>(`/api/organizations/${organizationId}/api-keys`) : Promise.resolve([]),
      api<WebhookEndpoint[]>(`/api/organizations/${organizationId}/webhook-endpoints`),
      api<Monitor[]>(`/api/organizations/${organizationId}/monitors`),
      api<Incident[]>(`/api/organizations/${organizationId}/incidents`),
      api<StatusRelease[]>(`/api/organizations/${organizationId}/releases`),
      roleAtLeast(selected?.role, "Admin") ? api<AuditLog[]>(`/api/organizations/${organizationId}/audit-logs`) : Promise.resolve([]),
      roleAtLeast(selected?.role, "Developer") ? api<ControlAction[]>(`/api/organizations/${organizationId}/control-actions`) : Promise.resolve([])
    ]);

    setProjects(projectPayload);
    setLiveApps(appPayload);
    setGitHubRepoConnections(repoConnectionPayload);
    setGitHubOnboardingPullRequests(onboardingPullRequestPayload);
    setGitHubWorkflowDispatches(workflowDispatchPayload);
    setMembers(memberPayload);
    setInvitations(invitationPayload);
    setRegistrationTokens(tokenPayload);
    setApiKeys(apiKeyPayload);
    setWebhookEndpoints(webhookPayload);
    setMonitors(monitorPayload);
    setIncidents(incidentPayload);
    setReleases(releasePayload);
    setAuditLogs(auditPayload);
    setControlActions(controlActionPayload);
    const nextProjectId = selectedProjectId && projectPayload.some((project) => project.id === selectedProjectId)
      ? selectedProjectId
      : projectPayload[0]?.id ?? "";
    setSelectedProjectId(nextProjectId);
    setCreatedToken(undefined);
    setCreatedApiKey(undefined);
    setCreatedWebhookEndpoint(undefined);
    if (selectedWebhookEndpointId && !webhookPayload.some((endpoint) => endpoint.id === selectedWebhookEndpointId)) {
      setSelectedWebhookEndpointId("");
      setWebhookDeliveries([]);
    }
    if (selectedMonitorId && !monitorPayload.some((monitor) => monitor.id === selectedMonitorId)) {
      setSelectedMonitorId("");
      setMonitorChecks([]);
    }
    if (selectedIncidentId && !incidentPayload.some((incident) => incident.id === selectedIncidentId)) {
      setSelectedIncidentId("");
      setIncidentUpdates([]);
    }
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

  async function refreshFeatureFlags(organizationId: string, projectId: string, environmentId: string) {
    if (!organizationId || !projectId || !environmentId) {
      setFeatureFlags([]);
      setFeatureFlagChanges([]);
      setHistoryFlagId("");
      return;
    }

    const flagPayload = await api<FeatureFlag[]>(
      `/api/organizations/${organizationId}/projects/${projectId}/environments/${environmentId}/feature-flags`);
    setFeatureFlags(flagPayload);
    if (historyFlagId && !flagPayload.some((flag) => flag.id === historyFlagId)) {
      setHistoryFlagId("");
      setFeatureFlagChanges([]);
    }
  }

  async function loadWebhookDeliveries(endpointId: string) {
    if (!selectedOrgId) {
      return;
    }

    const deliveries = await api<WebhookDelivery[]>(`/api/organizations/${selectedOrgId}/webhook-endpoints/${endpointId}/deliveries`);
    setSelectedWebhookEndpointId(endpointId);
    setWebhookDeliveries(deliveries);
  }

  async function loadMonitorChecks(monitor: Monitor) {
    if (!selectedOrgId) {
      return;
    }

    const checks = await api<MonitorCheck[]>(`/api/organizations/${selectedOrgId}/monitors/${monitor.id}/checks`);
    setSelectedMonitorId(monitor.id);
    setMonitorChecks(checks);
    setMonitorForm({
      name: monitor.name,
      url: monitor.url,
      intervalSeconds: String(monitor.intervalSeconds),
      timeoutSeconds: String(monitor.timeoutSeconds),
      slowThresholdMilliseconds: String(monitor.slowThresholdMilliseconds),
      failureThreshold: String(monitor.failureThreshold),
      recoveryThreshold: String(monitor.recoveryThreshold)
    });
  }

  async function loadIncidentUpdates(incidentId: string) {
    if (!selectedOrgId) {
      return;
    }

    const updates = await api<IncidentUpdate[]>(`/api/organizations/${selectedOrgId}/incidents/${incidentId}/updates`);
    const incident = incidents.find((candidate) => candidate.id === incidentId);
    setSelectedIncidentId(incidentId);
    setIncidentUpdates(updates);
    setIncidentUpdateForm({
      message: "",
      status: incident?.status ?? "Investigating",
      private: false
    });
  }

  useEffect(() => {
    void loadMe();
  }, []);

  useEffect(() => {
    api<PublicConfig>("/api/public/config")
      .then((payload) => setObservabilityUrl(payload.observabilityUrl))
      .catch(() => setObservabilityUrl(undefined));
  }, []);

  useEffect(() => {
    if (!statusPage) {
      return;
    }

    const environment = new URLSearchParams(window.location.search).get("environment");
    const path = `/api/public/status/${statusPage.organizationSlug}/${statusPage.projectSlug}${environment ? `?environment=${encodeURIComponent(environment)}` : ""}`;
    api<PublicStatusPage>(path)
      .then((payload) => {
        setPublicStatus(payload);
        setPublicStatusError(undefined);
      })
      .catch((loadError: unknown) => {
        setPublicStatus(undefined);
        setPublicStatusError(loadError instanceof Error ? loadError.message : "Failed to load status page.");
      });
  }, [statusPage]);

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

  useEffect(() => {
    if (authenticated && selectedOrgId && selectedProjectId && selectedEnvironmentId) {
      void refreshFeatureFlags(selectedOrgId, selectedProjectId, selectedEnvironmentId).catch((refreshError: unknown) => {
        setError(refreshError instanceof Error ? refreshError.message : "Failed to load feature flags.");
      });
    } else {
      setFeatureFlags([]);
      setFeatureFlagChanges([]);
      setHistoryFlagId("");
    }
  }, [authenticated, selectedOrgId, selectedProjectId, selectedEnvironmentId]);

  useEffect(() => {
    if (authenticated && selectedOrgId && selectedWebhookEndpointId) {
      void loadWebhookDeliveries(selectedWebhookEndpointId).catch((refreshError: unknown) => {
        setError(refreshError instanceof Error ? refreshError.message : "Failed to load webhook deliveries.");
      });
    } else {
      setWebhookDeliveries([]);
    }
  }, [authenticated, selectedOrgId, selectedWebhookEndpointId]);

  useEffect(() => {
    if (authenticated && selectedOrgId && selectedMonitor) {
      void loadMonitorChecks(selectedMonitor).catch((refreshError: unknown) => {
        setError(refreshError instanceof Error ? refreshError.message : "Failed to load monitor checks.");
      });
    } else {
      setMonitorChecks([]);
    }
  }, [authenticated, selectedOrgId, selectedMonitorId]);

  useEffect(() => {
    if (authenticated && selectedOrgId && selectedIncidentId) {
      void loadIncidentUpdates(selectedIncidentId).catch((refreshError: unknown) => {
        setError(refreshError instanceof Error ? refreshError.message : "Failed to load incident updates.");
      });
    } else {
      setIncidentUpdates([]);
    }
  }, [authenticated, selectedOrgId, selectedIncidentId]);

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
    const returnUrl = new URLSearchParams(window.location.search).get("returnUrl");
    const safeReturnUrl = returnUrl?.startsWith("/") && !returnUrl.startsWith("//")
      ? returnUrl
      : window.location.pathname + window.location.search;
    window.location.href = `/auth/login?returnUrl=${encodeURIComponent(safeReturnUrl)}`;
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

  async function resolveGitHubRepo(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      const resolution = await api<GitHubRepositoryResolution>(
        `/api/organizations/${selectedOrgId}/github/repositories?repo=${encodeURIComponent(gitHubForm.repo)}`);
      setGitHubResolution(resolution);
      setGitHubForm((current) => ({
        ...current,
        repo: resolution.fullName,
        workflowPath: current.workflowPath || resolution.workflows.find((workflow) => workflow.path.endsWith("deploy.yml"))?.path || resolution.workflows[0]?.path || ""
      }));
      setNotice("GitHub repository resolved.");
    });
  }

  async function createGitHubOnboardingPullRequest(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId || !selectedProjectId || !selectedEnvironmentId) {
      return;
    }

    await runMutation(async () => {
      const response = await fetch(`/api/organizations/${selectedOrgId}/github/onboarding-prs`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": await getCsrfToken()
        },
        body: JSON.stringify({
          projectId: selectedProjectId,
          environmentId: selectedEnvironmentId,
          repo: gitHubForm.repo,
          workflowPath: gitHubForm.workflowPath,
          jobId: gitHubForm.jobId,
          serviceUrlExpression: gitHubForm.serviceUrlExpression,
          healthUrlExpression: gitHubForm.healthUrlExpression,
          versionExpression: gitHubForm.versionExpression,
          imageDigestExpression: gitHubForm.imageDigestExpression,
          capabilities: gitHubForm.capabilities.split(",").map((capability) => capability.trim()).filter(Boolean)
        })
      });

      if (!response.ok) {
        const payload = (await response.json().catch(() => undefined)) as { errors?: string[]; manualSnippet?: string; detail?: string; title?: string } | undefined;
        setGitHubManualSnippet(payload?.manualSnippet);
        throw new Error(payload?.errors?.join(" ") ?? payload?.detail ?? payload?.title ?? `GitHub onboarding PR failed with ${response.status}.`);
      }

      const pullRequest = (await response.json()) as GitHubOnboardingPullRequest;
      setGitHubManualSnippet(undefined);
      await refreshOrgData(selectedOrgId);
      setNotice(`Onboarding PR #${pullRequest.pullRequestNumber} opened.`);
    });
  }

  async function syncGitHubOnboardingPullRequest(pullRequest: GitHubOnboardingPullRequest) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      await api<GitHubOnboardingPullRequest>(`/api/organizations/${selectedOrgId}/github/onboarding-prs/${pullRequest.id}/sync`, { method: "POST" });
      await refreshOrgData(selectedOrgId);
      setNotice("Onboarding PR synced.");
    });
  }

  async function loadAppDeployments(app: LiveApp) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      const deployments = await api<LiveAppDeployment[]>(`/api/organizations/${selectedOrgId}/apps/${app.id}/deployments`);
      setLiveAppDeployments((current) => ({ ...current, [app.id]: deployments }));
      setRollbackTargets((current) => ({
        ...current,
        [app.id]: current[app.id] || deployments.find((deployment) => deployment.id !== deployments[0]?.id)?.id || deployments[0]?.id || ""
      }));
      setNotice("Deployment history loaded.");
    });
  }

  async function dispatchLiveAppAction(app: LiveApp, action: "deploy" | "redeploy" | "rollback") {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      const reason = appActionReasons[app.id] ?? "";
      const targetDeploymentId = action === "rollback" ? rollbackTargets[app.id] || undefined : undefined;
      await api<GitHubWorkflowDispatch>(`/api/organizations/${selectedOrgId}/apps/${app.id}/actions/${action}`, {
        method: "POST",
        body: JSON.stringify({ reason, targetDeploymentId })
      });
      await refreshOrgData(selectedOrgId);
      setNotice(`${action} workflow dispatched.`);
    });
  }

  async function createApiKey(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId || !selectedProjectId || !selectedEnvironmentId) {
      return;
    }

    await runMutation(async () => {
      const rateLimitPerMinute = Number.parseInt(apiKeyForm.rateLimitPerMinute, 10);
      const apiKey = await api<ApiKeyCreateResponse>(
        `/api/organizations/${selectedOrgId}/projects/${selectedProjectId}/environments/${selectedEnvironmentId}/api-keys`,
        {
          method: "POST",
          body: JSON.stringify({
            name: apiKeyForm.name,
            scopes: [apiKeyForm.scope],
            rateLimitPerMinute
          })
        });
      setApiKeyForm({ name: "", scope: "sample:read", rateLimitPerMinute: "10" });
      await refreshOrgData(selectedOrgId);
      setCreatedApiKey(apiKey);
      setNotice("API key created. Copy it now; it will not be shown again.");
    });
  }

  async function createWebhookEndpoint(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId || !selectedProjectId || !selectedEnvironmentId) {
      return;
    }

    await runMutation(async () => {
      const eventTypes = webhookEventTypes.filter((eventType) => webhookForm.eventTypes[eventType]);
      const endpoint = await api<WebhookEndpointCreateResponse>(
        `/api/organizations/${selectedOrgId}/projects/${selectedProjectId}/environments/${selectedEnvironmentId}/webhook-endpoints`,
        {
          method: "POST",
          body: JSON.stringify({
            name: webhookForm.name,
            url: webhookForm.url,
            eventTypes
          })
        });
      setWebhookForm({
        name: "",
        url: "",
        eventTypes: Object.fromEntries(webhookEventTypes.map((eventType) => [eventType, eventType === "webhook.test"])) as Record<string, boolean>
      });
      await refreshOrgData(selectedOrgId);
      setCreatedWebhookEndpoint(endpoint);
      setSelectedWebhookEndpointId(endpoint.id);
      setNotice("Webhook endpoint created. Copy the signing secret now; it will not be shown again.");
    });
  }

  async function changeWebhookPause(endpoint: WebhookEndpoint) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      const action = endpoint.isPaused ? "resume" : "pause";
      await api<WebhookEndpoint>(`/api/organizations/${selectedOrgId}/webhook-endpoints/${endpoint.id}/${action}`, { method: "POST" });
      await refreshOrgData(selectedOrgId);
      setNotice(endpoint.isPaused ? "Webhook endpoint resumed." : "Webhook endpoint paused.");
    });
  }

  async function testWebhookEndpoint(endpoint: WebhookEndpoint) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      const delivery = await api<WebhookDelivery>(
        `/api/organizations/${selectedOrgId}/webhook-endpoints/${endpoint.id}/test-deliveries`,
        { method: "POST" });
      setSelectedWebhookEndpointId(endpoint.id);
      await loadWebhookDeliveries(endpoint.id);
      await refreshOrgData(selectedOrgId);
      setNotice(`Webhook test delivery ${delivery.status}.`);
    });
  }

  async function retryWebhookDelivery(delivery: WebhookDelivery) {
    if (!selectedOrgId || !selectedWebhookEndpointId) {
      return;
    }

    await runMutation(async () => {
      const retried = await api<WebhookDelivery>(
        `/api/organizations/${selectedOrgId}/webhook-deliveries/${delivery.id}/retry`,
        { method: "POST" });
      await loadWebhookDeliveries(selectedWebhookEndpointId);
      await refreshOrgData(selectedOrgId);
      setNotice(`Webhook retry ${retried.status}.`);
    });
  }

  async function saveMonitor(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId || !selectedMonitor) {
      return;
    }

    await runMutation(async () => {
      await api<Monitor>(`/api/organizations/${selectedOrgId}/monitors/${selectedMonitor.id}`, {
        method: "PATCH",
        body: JSON.stringify({
          name: monitorForm.name,
          url: monitorForm.url,
          intervalSeconds: Number(monitorForm.intervalSeconds),
          timeoutSeconds: Number(monitorForm.timeoutSeconds),
          slowThresholdMilliseconds: Number(monitorForm.slowThresholdMilliseconds),
          failureThreshold: Number(monitorForm.failureThreshold),
          recoveryThreshold: Number(monitorForm.recoveryThreshold)
        })
      });
      await refreshOrgData(selectedOrgId);
      setNotice("Monitor updated.");
    });
  }

  async function changeMonitorPause(monitor: Monitor) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      const action = monitor.isPaused ? "resume" : "pause";
      await api<Monitor>(`/api/organizations/${selectedOrgId}/monitors/${monitor.id}/${action}`, { method: "POST" });
      await refreshOrgData(selectedOrgId);
      setNotice(monitor.isPaused ? "Monitor resumed." : "Monitor paused.");
    });
  }

  async function createIncident(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId || !selectedProjectId || !selectedEnvironmentId) {
      return;
    }

    await runMutation(async () => {
      const incident = await api<Incident>(
        `/api/organizations/${selectedOrgId}/projects/${selectedProjectId}/environments/${selectedEnvironmentId}/incidents`,
        {
          method: "POST",
          body: JSON.stringify(incidentForm)
        });
      setIncidentForm({ title: "", summary: "", message: "", private: false });
      await refreshOrgData(selectedOrgId);
      setSelectedIncidentId(incident.id);
      setNotice("Incident created.");
    });
  }

  async function addIncidentUpdate(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId || !selectedIncident) {
      return;
    }

    await runMutation(async () => {
      await api<IncidentUpdate>(`/api/organizations/${selectedOrgId}/incidents/${selectedIncident.id}/updates`, {
        method: "POST",
        body: JSON.stringify(incidentUpdateForm)
      });
      setIncidentUpdateForm({ message: "", status: selectedIncident.status, private: false });
      await refreshOrgData(selectedOrgId);
      await loadIncidentUpdates(selectedIncident.id);
      setNotice("Incident update added.");
    });
  }

  async function createRelease(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId || !selectedProjectId || !selectedEnvironmentId) {
      return;
    }

    await runMutation(async () => {
      await api<StatusRelease>(
        `/api/organizations/${selectedOrgId}/projects/${selectedProjectId}/environments/${selectedEnvironmentId}/releases`,
        {
          method: "POST",
          body: JSON.stringify(releaseForm)
        });
      setReleaseForm({ title: "", version: "", body: "" });
      await refreshOrgData(selectedOrgId);
      setNotice("Release draft created.");
    });
  }

  async function publishRelease(release: StatusRelease) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      await api<StatusRelease>(`/api/organizations/${selectedOrgId}/releases/${release.id}/publish`, { method: "POST" });
      await refreshOrgData(selectedOrgId);
      setNotice("Release published.");
    });
  }

  async function createFeatureFlag(event: FormEvent) {
    event.preventDefault();
    if (!selectedOrgId || !selectedProjectId || !selectedEnvironmentId) {
      return;
    }

    await runMutation(async () => {
      await api<FeatureFlag>(
        `/api/organizations/${selectedOrgId}/projects/${selectedProjectId}/environments/${selectedEnvironmentId}/feature-flags`,
        {
          method: "POST",
          body: JSON.stringify(flagForm)
        });
      setFlagForm({ key: "", name: "", description: "", kind: "FeatureFlag", enabled: false, reason: "" });
      await refreshFeatureFlags(selectedOrgId, selectedProjectId, selectedEnvironmentId);
      await refreshOrgData(selectedOrgId);
      setNotice("Feature flag created.");
    });
  }

  async function toggleFeatureFlag(flag: FeatureFlag) {
    if (!selectedOrgId || !selectedProjectId || !selectedEnvironmentId) {
      return;
    }

    await runMutation(async () => {
      await api<FeatureFlag>(`/api/organizations/${selectedOrgId}/feature-flags/${flag.id}`, {
        method: "PATCH",
        body: JSON.stringify({
          enabled: !flag.enabled,
          reason: flagReasons[flag.id] ?? ""
        })
      });
      setFlagReasons(({ [flag.id]: _removed, ...rest }) => rest);
      await refreshFeatureFlags(selectedOrgId, selectedProjectId, selectedEnvironmentId);
      if (historyFlagId === flag.id) {
        await loadFeatureFlagChanges(flag.id);
      }
      setNotice(flag.kind === "KillSwitch" ? "Kill switch updated." : "Feature flag updated.");
    });
  }

  async function loadFeatureFlagChanges(featureFlagId: string) {
    if (!selectedOrgId) {
      return;
    }

    const changes = await api<FeatureFlagChange[]>(`/api/organizations/${selectedOrgId}/feature-flags/${featureFlagId}/changes`);
    setHistoryFlagId(featureFlagId);
    setFeatureFlagChanges(changes);
  }

  async function revokeApiKey(apiKeyItem: ApiKey) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      await api(`/api/organizations/${selectedOrgId}/api-keys/${apiKeyItem.id}/revoke`, { method: "POST" });
      await refreshOrgData(selectedOrgId);
      setNotice("API key revoked.");
    });
  }

  async function rotateApiKey(apiKeyItem: ApiKey) {
    if (!selectedOrgId) {
      return;
    }

    await runMutation(async () => {
      const rotated = await api<ApiKeyCreateResponse>(`/api/organizations/${selectedOrgId}/api-keys/${apiKeyItem.id}/rotate`, { method: "POST" });
      await refreshOrgData(selectedOrgId);
      setCreatedApiKey(rotated);
      setNotice("API key rotated. Copy the new key now; it will not be shown again.");
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

  if (statusPage) {
    if (publicStatusError) {
      return <main className="loading">Status page unavailable: {publicStatusError}</main>;
    }

    if (!publicStatus) {
      return <main className="loading">Loading status...</main>;
    }

    return (
      <main className="app-shell status-page">
        <header className="topbar">
          <div>
            <p className="eyebrow">DevControl Status</p>
            <h1>{publicStatus.projectName}</h1>
          </div>
          <strong className={`status-pill status-${publicStatus.overallStatus}`}>{publicStatus.overallStatus}</strong>
        </header>
        <section className="workspace-grid">
          <div className="panel wide">
            <div className="panel-heading">
              <h2>Monitors</h2>
              <span>{publicStatus.monitors.length}</span>
            </div>
            <div className="table">
              {publicStatus.monitors.length === 0 ? <p className="empty">No monitors</p> : null}
              {publicStatus.monitors.map((monitor) => (
                <div className="table-row monitor-row" key={monitor.id}>
                  <div>
                    <strong>{monitor.name}</strong>
                    <span>{monitor.environmentName}</span>
                    <span>Last checked {formatDate(monitor.lastCheckedAt)}</span>
                  </div>
                  <span className={monitor.status === "Up" ? "status-on" : "status-off"}>{monitor.status}</span>
                  <span>{monitor.uptimePercentLast24Hours.toFixed(2)}% 24h</span>
                </div>
              ))}
            </div>
          </div>
          <div className="panel wide">
            <div className="panel-heading">
              <h2>Incidents</h2>
              <span>{publicStatus.incidents.length}</span>
            </div>
            <div className="table">
              {publicStatus.incidents.length === 0 ? <p className="empty">No incidents</p> : null}
              {publicStatus.incidents.map((incident) => (
                <div className="status-incident" key={incident.id}>
                  <div className="table-row incident-row">
                    <div>
                      <strong>{incident.title}</strong>
                      <span>{incident.environmentName} / {incident.status}</span>
                      <span>{formatDate(incident.createdAt)}</span>
                    </div>
                    <p>{incident.summary}</p>
                  </div>
                  {incident.updates.map((update) => (
                    <div className="audit-row" key={update.id}>
                      <time>{formatDate(update.createdAt)}</time>
                      <strong>{update.status}</strong>
                      <span>{update.createdByEmail}</span>
                      <p>{update.message}</p>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </div>
          <div className="panel wide">
            <div className="panel-heading">
              <h2>Releases</h2>
              <span>{publicStatus.releases.length}</span>
            </div>
            <div className="table">
              {publicStatus.releases.length === 0 ? <p className="empty">No releases</p> : null}
              {publicStatus.releases.map((release) => (
                <div className="table-row release-row" key={release.id}>
                  <div>
                    <strong>{release.title}</strong>
                    <span>{release.environmentName} / {release.version}</span>
                    <span>Published {formatDate(release.publishedAt)}</span>
                  </div>
                  <p>{release.body}</p>
                </div>
              ))}
            </div>
          </div>
        </section>
      </main>
    );
  }

  if (authenticated === undefined) {
    return <main className="loading">Loading DevControl...</main>;
  }

  if (!authenticated) {
    return (
      <main className="auth-screen">
        <section className="auth-panel">
          <p className="eyebrow">DevControl</p>
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
          <div className="topbar-actions">
            {observabilityUrl ? (
              <a className="button-link" href={observabilityUrl} target="_blank" rel="noreferrer">
                Observability
              </a>
            ) : null}
            <button onClick={logout} disabled={busy}>Sign out</button>
          </div>
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
                      {app.gitHubRunUrl ? <a href={app.gitHubRunUrl} target="_blank" rel="noreferrer">Last GitHub run</a> : null}
                      <span>Registered {formatDate(app.lastRegisteredAt)}</span>
                    </div>
                    <div className="actions">
                      <a href={app.serviceUrl} target="_blank" rel="noreferrer">Service</a>
                      <a href={app.healthUrl} target="_blank" rel="noreferrer">Health</a>
                      {canManageOrg && app.capabilities.some((capability) => ["deploy", "redeploy", "rollback"].includes(capability)) && (
                        <div className="live-control">
                          <input placeholder="Action reason" value={appActionReasons[app.id] ?? ""} onChange={(event) => setAppActionReasons({ ...appActionReasons, [app.id]: event.target.value })} />
                          <div className="actions">
                            {app.capabilities.includes("deploy") && <button onClick={() => dispatchLiveAppAction(app, "deploy")} disabled={busy}>Deploy</button>}
                            {app.capabilities.includes("redeploy") && <button onClick={() => dispatchLiveAppAction(app, "redeploy")} disabled={busy}>Redeploy</button>}
                            {app.capabilities.includes("rollback") && <button onClick={() => loadAppDeployments(app)} disabled={busy}>History</button>}
                          </div>
                          {app.capabilities.includes("rollback") && liveAppDeployments[app.id]?.length ? (
                            <div className="rollback-row">
                              <select value={rollbackTargets[app.id] ?? ""} onChange={(event) => setRollbackTargets({ ...rollbackTargets, [app.id]: event.target.value })}>
                                {liveAppDeployments[app.id].map((deployment) => (
                                  <option value={deployment.id} key={deployment.id}>
                                    {deployment.version} / {shortSha(deployment.commitSha)} / {formatDate(deployment.registeredAt)}
                                  </option>
                                ))}
                              </select>
                              <button onClick={() => dispatchLiveAppAction(app, "rollback")} disabled={busy || !rollbackTargets[app.id]}>Rollback</button>
                            </div>
                          ) : null}
                        </div>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {canManageOrg && (
              <div className="panel wide">
                <div className="panel-heading">
                  <h2>GitHub onboarding</h2>
                  <span>{filteredGitHubRepoConnections.length}</span>
                </div>
                <form className="inline-form github-resolve-form" onSubmit={resolveGitHubRepo}>
                  <input required placeholder="owner/repo or GitHub URL" value={gitHubForm.repo} onChange={(event) => setGitHubForm({ ...gitHubForm, repo: event.target.value })} />
                  <button className="primary" disabled={busy}>Resolve</button>
                </form>
                {gitHubResolution && (
                  <div className="github-resolution">
                    <span>{gitHubResolution.fullName}</span>
                    <span>{gitHubResolution.defaultBranch}</span>
                    <a href={gitHubResolution.htmlUrl} target="_blank" rel="noreferrer">Repository</a>
                  </div>
                )}
                {selectedEnvironment && (
                  <form className="inline-form github-onboarding-form" onSubmit={createGitHubOnboardingPullRequest}>
                    <p className="form-help">Use literals, GitHub expressions, or shell variables that exist before the registration step. The defaults match workflows that set SERVICE_URL, REGISTER_VERSION, and REGISTER_IMAGE_DIGEST earlier in the job.</p>
                    <select required value={gitHubForm.workflowPath} onChange={(event) => setGitHubForm({ ...gitHubForm, workflowPath: event.target.value })}>
                      <option value="">Workflow</option>
                      {gitHubResolution?.workflows.map((workflow) => (
                        <option value={workflow.path} key={workflow.id}>{workflow.name} / {workflow.path}</option>
                      ))}
                    </select>
                    <input required placeholder="Job id" value={gitHubForm.jobId} onChange={(event) => setGitHubForm({ ...gitHubForm, jobId: event.target.value })} />
                    <input required placeholder="Service URL expression" value={gitHubForm.serviceUrlExpression} onChange={(event) => setGitHubForm({ ...gitHubForm, serviceUrlExpression: event.target.value })} />
                    <input required placeholder="Health URL expression" value={gitHubForm.healthUrlExpression} onChange={(event) => setGitHubForm({ ...gitHubForm, healthUrlExpression: event.target.value })} />
                    <input required placeholder="Version expression" value={gitHubForm.versionExpression} onChange={(event) => setGitHubForm({ ...gitHubForm, versionExpression: event.target.value })} />
                    <input required placeholder="Image digest expression" value={gitHubForm.imageDigestExpression} onChange={(event) => setGitHubForm({ ...gitHubForm, imageDigestExpression: event.target.value })} />
                    <input required placeholder="Capabilities" value={gitHubForm.capabilities} onChange={(event) => setGitHubForm({ ...gitHubForm, capabilities: event.target.value })} />
                    <button className="primary" disabled={busy || !gitHubForm.workflowPath}>Open PR</button>
                  </form>
                )}
                {gitHubManualSnippet && (
                  <div className="secret-box">
                    <strong>Manual registration snippet</strong>
                    <pre>{gitHubManualSnippet}</pre>
                  </div>
                )}
                <div className="table">
                  {filteredGitHubOnboardingPullRequests.length === 0 ? <p className="empty">No onboarding PRs</p> : null}
                  {filteredGitHubOnboardingPullRequests.map((pullRequest) => (
                    <div className="table-row github-pr-row" key={pullRequest.id}>
                      <div>
                        <strong>{pullRequest.repo}</strong>
                        <span>{pullRequest.projectName} / {pullRequest.environmentName}</span>
                        <span>{pullRequest.workflowPath}</span>
                        <span>{pullRequest.status}{pullRequest.error ? ` / ${pullRequest.error}` : ""}</span>
                      </div>
                      <a href={pullRequest.pullRequestUrl} target="_blank" rel="noreferrer">PR #{pullRequest.pullRequestNumber}</a>
                      <button onClick={() => syncGitHubOnboardingPullRequest(pullRequest)} disabled={busy}>Sync</button>
                    </div>
                  ))}
                </div>
                <div className="table">
                  {filteredGitHubWorkflowDispatches.length === 0 ? <p className="empty">No workflow dispatches</p> : null}
                  {filteredGitHubWorkflowDispatches.map((dispatch) => (
                    <div className="table-row github-dispatch-row" key={dispatch.id}>
                      <div>
                        <strong>{dispatch.action} / {dispatch.controlActionStatus}</strong>
                        <span>{dispatch.repo}</span>
                        <span>{dispatch.workflowPath} @ {dispatch.ref}</span>
                        <span>{dispatch.status}{dispatch.conclusion ? ` / ${dispatch.conclusion}` : ""}</span>
                      </div>
                      <span>{formatDate(dispatch.requestedAt)}</span>
                      {dispatch.runUrl ? <a href={dispatch.runUrl} target="_blank" rel="noreferrer">Run</a> : <span>-</span>}
                    </div>
                  ))}
                </div>
              </div>
            )}

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
                  <h2>API keys</h2>
                  <span>{filteredApiKeys.length}</span>
                </div>
                {createdApiKey && (
                  <div className="secret-box">
                    <strong>Copy this API key now</strong>
                    <code>{createdApiKey.secret}</code>
                    <span>Use it as a bearer token or X-DevControl-Api-Key value.</span>
                  </div>
                )}
                <div className="table">
                  {filteredApiKeys.length === 0 ? <p className="empty">No API keys</p> : null}
                  {filteredApiKeys.map((apiKeyItem) => (
                    <div className="table-row api-key-row" key={apiKeyItem.id}>
                      <div>
                        <strong>{apiKeyItem.name}</strong>
                        <span>{apiKeyItem.projectName} / {apiKeyItem.environmentName}</span>
                        <span>{apiKeyItem.keyPrefix}... / {apiKeyItem.scopes.join(", ")} / {apiKeyItem.rateLimitPerMinute}/min</span>
                        <span>Last used {formatDate(apiKeyItem.lastUsedAt)}</span>
                      </div>
                      <div className="usage-metrics">
                        <span>{apiKeyItem.totalRequestCount} requests</span>
                        <span>{apiKeyItem.failureCount} failures</span>
                        <span>{formatLatency(apiKeyItem.averageLatencyMilliseconds)} avg</span>
                        <span>{apiKeyItem.rateLimitHitCount} limited</span>
                      </div>
                      <span>{apiKeyItem.revokedAt ? "Revoked" : "Active"}</span>
                      <div className="actions">
                        <button onClick={() => rotateApiKey(apiKeyItem)} disabled={busy || Boolean(apiKeyItem.revokedAt)}>Rotate</button>
                        <button onClick={() => revokeApiKey(apiKeyItem)} disabled={busy || Boolean(apiKeyItem.revokedAt)}>Revoke</button>
                      </div>
                    </div>
                  ))}
                </div>
                {selectedEnvironment && (
                  <form className="inline-form api-key-form" onSubmit={createApiKey}>
                    <input placeholder={`Name for ${selectedEnvironment.name}`} value={apiKeyForm.name} onChange={(event) => setApiKeyForm({ ...apiKeyForm, name: event.target.value })} />
                    <select value={apiKeyForm.scope} onChange={(event) => setApiKeyForm({ ...apiKeyForm, scope: event.target.value })}>
                      <option value="sample:read">sample:read</option>
                      <option value="flags:read">flags:read</option>
                    </select>
                    <input type="number" min="1" max="600" value={apiKeyForm.rateLimitPerMinute} onChange={(event) => setApiKeyForm({ ...apiKeyForm, rateLimitPerMinute: event.target.value })} />
                    <button className="primary" disabled={busy}>Create API key</button>
                  </form>
                )}
              </div>
            )}

            {canManageOrg && (
              <div className="panel wide">
                <div className="panel-heading">
                  <h2>Webhooks</h2>
                  <span>{filteredWebhookEndpoints.length}</span>
                </div>
                {createdWebhookEndpoint && (
                  <div className="secret-box">
                    <strong>Copy this webhook signing secret now</strong>
                    <code>{createdWebhookEndpoint.secret}</code>
                    <span>DevControl signs deliveries with HMAC-SHA256 in X-DevControl-Signature.</span>
                  </div>
                )}
                <div className="table">
                  {filteredWebhookEndpoints.length === 0 ? <p className="empty">No webhook endpoints</p> : null}
                  {filteredWebhookEndpoints.map((endpoint) => (
                    <div className="table-row webhook-row" key={endpoint.id}>
                      <div>
                        <strong>{endpoint.name}</strong>
                        <span>{endpoint.projectName} / {endpoint.environmentName}</span>
                        <span>{endpoint.url}</span>
                        <span>{endpoint.secretPrefix}... / {endpoint.eventTypes.join(", ")}</span>
                        <span>Last delivery {formatDate(endpoint.lastDeliveryAt)}</span>
                      </div>
                      <div className="usage-metrics">
                        <span className={endpoint.isPaused ? "status-off" : "status-on"}>{endpoint.isPaused ? "Paused" : "Active"}</span>
                        <span>Last success {formatDate(endpoint.lastSuccessAt)}</span>
                        <span>Last failure {formatDate(endpoint.lastFailureAt)}</span>
                      </div>
                      <div className="actions">
                        <button onClick={() => void loadWebhookDeliveries(endpoint.id)} disabled={busy}>Deliveries</button>
                        <button onClick={() => testWebhookEndpoint(endpoint)} disabled={busy || endpoint.isPaused}>Test</button>
                        <button onClick={() => changeWebhookPause(endpoint)} disabled={busy}>{endpoint.isPaused ? "Resume" : "Pause"}</button>
                      </div>
                    </div>
                  ))}
                </div>
                {selectedWebhookEndpoint && (
                  <div className="delivery-history">
                    <strong>{selectedWebhookEndpoint.name} deliveries</strong>
                    {webhookDeliveries.length === 0 ? <p className="empty">No deliveries</p> : null}
                    {webhookDeliveries.map((delivery) => (
                      <div className="table-row webhook-delivery-row" key={delivery.id}>
                        <div>
                          <strong>{delivery.eventType}</strong>
                          <span>{delivery.status} / {delivery.attemptCount} of {delivery.maxAttempts}</span>
                          <span>{delivery.resourceType}{delivery.resourceId ? ` / ${delivery.resourceId}` : ""}</span>
                          <span>Created {formatDate(delivery.createdAt)}</span>
                        </div>
                        <div className="usage-metrics">
                          <span>HTTP {delivery.lastStatusCode ?? "-"}</span>
                          <span>Next {formatDate(delivery.nextAttemptAt)}</span>
                          <span>Last {formatDate(delivery.lastAttemptAt)}</span>
                        </div>
                        <div>
                          <span>{delivery.lastError || "No error"}</span>
                          <span>{delivery.lastResponsePreview || "No response preview"}{delivery.lastResponseTruncated ? "..." : ""}</span>
                        </div>
                        <button
                          onClick={() => retryWebhookDelivery(delivery)}
                          disabled={busy || delivery.status === "Succeeded" || delivery.status === "SkippedPaused"}
                        >
                          Retry
                        </button>
                      </div>
                    ))}
                  </div>
                )}
                {selectedEnvironment && (
                  <form className="inline-form webhook-form" onSubmit={createWebhookEndpoint}>
                    <input required placeholder={`Name for ${selectedEnvironment.name}`} value={webhookForm.name} onChange={(event) => setWebhookForm({ ...webhookForm, name: event.target.value })} />
                    <input required placeholder="https://example.com/webhooks/devcontrol" value={webhookForm.url} onChange={(event) => setWebhookForm({ ...webhookForm, url: event.target.value })} />
                    <div className="event-grid">
                      {webhookEventTypes.map((eventType) => (
                        <label className="checkbox-row" key={eventType}>
                          <input
                            type="checkbox"
                            checked={Boolean(webhookForm.eventTypes[eventType])}
                            onChange={(event) => setWebhookForm({
                              ...webhookForm,
                              eventTypes: { ...webhookForm.eventTypes, [eventType]: event.target.checked }
                            })}
                          />
                          {eventType}
                        </label>
                      ))}
                    </div>
                    <button className="primary" disabled={busy}>Create webhook</button>
                  </form>
                )}
              </div>
            )}

            <div className="panel wide">
              <div className="panel-heading">
                <h2>Monitors</h2>
                <span>{filteredMonitors.length}</span>
              </div>
              {statusPagePath && (
                <p className="status-link"><a href={statusPagePath} target="_blank" rel="noreferrer">Public status page</a></p>
              )}
              <div className="table">
                {filteredMonitors.length === 0 ? <p className="empty">No monitors</p> : null}
                {filteredMonitors.map((monitor) => (
                  <div className="table-row monitor-row" key={monitor.id}>
                    <div>
                      <strong>{monitor.name}</strong>
                      <span>{monitor.projectName} / {monitor.environmentName}</span>
                      <span>{monitor.url}</span>
                      <span>Last checked {formatDate(monitor.lastCheckedAt)} / next {formatDate(monitor.nextCheckAt)}</span>
                    </div>
                    <div className="usage-metrics">
                      <span className={monitor.currentStatus === "Up" || monitor.currentStatus === "Slow" ? "status-on" : "status-off"}>{monitor.isPaused ? "Paused" : monitor.currentStatus}</span>
                      <span>{monitor.consecutiveFailures} failures</span>
                      <span>{monitor.consecutiveRecoveries} recoveries</span>
                    </div>
                    <div className="actions">
                      <button onClick={() => void loadMonitorChecks(monitor)} disabled={busy}>Checks</button>
                      {canManageMonitors && <button onClick={() => changeMonitorPause(monitor)} disabled={busy}>{monitor.isPaused ? "Resume" : "Pause"}</button>}
                    </div>
                  </div>
                ))}
              </div>
              {selectedMonitor && (
                <div className="delivery-history">
                  <strong>{selectedMonitor.name} checks</strong>
                  {canManageMonitors && (
                    <form className="inline-form monitor-form" onSubmit={saveMonitor}>
                      <input required placeholder="Name" value={monitorForm.name} onChange={(event) => setMonitorForm({ ...monitorForm, name: event.target.value })} />
                      <input required placeholder="https://app.example.com/health" value={monitorForm.url} onChange={(event) => setMonitorForm({ ...monitorForm, url: event.target.value })} />
                      <input type="number" min="60" max="86400" value={monitorForm.intervalSeconds} onChange={(event) => setMonitorForm({ ...monitorForm, intervalSeconds: event.target.value })} />
                      <input type="number" min="1" max="30" value={monitorForm.timeoutSeconds} onChange={(event) => setMonitorForm({ ...monitorForm, timeoutSeconds: event.target.value })} />
                      <input type="number" min="100" max="30000" value={monitorForm.slowThresholdMilliseconds} onChange={(event) => setMonitorForm({ ...monitorForm, slowThresholdMilliseconds: event.target.value })} />
                      <input type="number" min="1" max="10" value={monitorForm.failureThreshold} onChange={(event) => setMonitorForm({ ...monitorForm, failureThreshold: event.target.value })} />
                      <input type="number" min="1" max="10" value={monitorForm.recoveryThreshold} onChange={(event) => setMonitorForm({ ...monitorForm, recoveryThreshold: event.target.value })} />
                      <button className="primary" disabled={busy}>Save</button>
                    </form>
                  )}
                  {monitorChecks.length === 0 ? <p className="empty">No checks</p> : null}
                  {monitorChecks.map((check) => (
                    <div className="table-row monitor-check-row" key={check.id}>
                      <div>
                        <strong>{check.status}</strong>
                        <span>{check.resultKind} / HTTP {check.statusCode ?? "-"}</span>
                        <span>{formatDate(check.checkedAt)}</span>
                      </div>
                      <span>{check.durationMilliseconds} ms</span>
                      <div>
                        <span>{check.error || "No error"}</span>
                        <span>{check.responsePreview || "No response preview"}{check.responseTruncated ? "..." : ""}</span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="panel wide">
              <div className="panel-heading">
                <h2>Incidents</h2>
                <span>{filteredIncidents.length}</span>
              </div>
              <div className="table">
                {filteredIncidents.length === 0 ? <p className="empty">No incidents</p> : null}
                {filteredIncidents.map((incident) => (
                  <div className="table-row incident-dashboard-row" key={incident.id}>
                    <div>
                      <strong>{incident.title}</strong>
                      <span>{incident.projectName} / {incident.environmentName}</span>
                      <span>{incident.summary || "No summary"}</span>
                      <span>Created {formatDate(incident.createdAt)}</span>
                    </div>
                    <span className={incident.status === "Resolved" ? "status-on" : "status-off"}>{incident.status}</span>
                    <button onClick={() => void loadIncidentUpdates(incident.id)} disabled={busy}>Updates</button>
                  </div>
                ))}
              </div>
              {selectedIncident && (
                <div className="flag-history">
                  <strong>{selectedIncident.title} timeline</strong>
                  {incidentUpdates.length === 0 ? <p className="empty">No updates</p> : null}
                  {incidentUpdates.map((update) => (
                    <div className="audit-row" key={update.id}>
                      <time>{formatDate(update.createdAt)}</time>
                      <strong>{update.status}</strong>
                      <span>{update.visibility}</span>
                      <p>{update.message}</p>
                    </div>
                  ))}
                  {canManageIncidents && (
                    <form className="inline-form incident-update-form" onSubmit={addIncidentUpdate}>
                      <input required placeholder="Update" value={incidentUpdateForm.message} onChange={(event) => setIncidentUpdateForm({ ...incidentUpdateForm, message: event.target.value })} />
                      <select value={incidentUpdateForm.status} onChange={(event) => setIncidentUpdateForm({ ...incidentUpdateForm, status: event.target.value })}>
                        <option value="Investigating">Investigating</option>
                        <option value="Identified">Identified</option>
                        <option value="Monitoring">Monitoring</option>
                        <option value="Resolved">Resolved</option>
                      </select>
                      <label className="checkbox-row">
                        <input type="checkbox" checked={incidentUpdateForm.private} onChange={(event) => setIncidentUpdateForm({ ...incidentUpdateForm, private: event.target.checked })} />
                        Private
                      </label>
                      <button className="primary" disabled={busy}>Add update</button>
                    </form>
                  )}
                </div>
              )}
              {selectedEnvironment && canManageIncidents && (
                <form className="inline-form incident-form" onSubmit={createIncident}>
                  <input required placeholder={`Incident title for ${selectedEnvironment.name}`} value={incidentForm.title} onChange={(event) => setIncidentForm({ ...incidentForm, title: event.target.value })} />
                  <input placeholder="Summary" value={incidentForm.summary} onChange={(event) => setIncidentForm({ ...incidentForm, summary: event.target.value })} />
                  <input placeholder="Initial update" value={incidentForm.message} onChange={(event) => setIncidentForm({ ...incidentForm, message: event.target.value })} />
                  <label className="checkbox-row">
                    <input type="checkbox" checked={incidentForm.private} onChange={(event) => setIncidentForm({ ...incidentForm, private: event.target.checked })} />
                    Private
                  </label>
                  <button className="primary" disabled={busy}>Create incident</button>
                </form>
              )}
            </div>

            <div className="panel wide">
              <div className="panel-heading">
                <h2>Releases</h2>
                <span>{filteredReleases.length}</span>
              </div>
              <div className="table">
                {filteredReleases.length === 0 ? <p className="empty">No releases</p> : null}
                {filteredReleases.map((release) => (
                  <div className="table-row release-dashboard-row" key={release.id}>
                    <div>
                      <strong>{release.title}</strong>
                      <span>{release.projectName} / {release.environmentName} / {release.version}</span>
                      <span>{release.body}</span>
                      <span>{release.status === "Published" ? `Published ${formatDate(release.publishedAt)}` : `Drafted ${formatDate(release.createdAt)}`}</span>
                    </div>
                    <span className={release.status === "Published" ? "status-on" : "status-off"}>{release.status}</span>
                    {canPublishReleases && <button onClick={() => publishRelease(release)} disabled={busy || release.status === "Published"}>Publish</button>}
                  </div>
                ))}
              </div>
              {selectedEnvironment && canManageIncidents && (
                <form className="inline-form release-form" onSubmit={createRelease}>
                  <input required placeholder="Release title" value={releaseForm.title} onChange={(event) => setReleaseForm({ ...releaseForm, title: event.target.value })} />
                  <input required placeholder="Version" value={releaseForm.version} onChange={(event) => setReleaseForm({ ...releaseForm, version: event.target.value })} />
                  <input required placeholder="Release notes" value={releaseForm.body} onChange={(event) => setReleaseForm({ ...releaseForm, body: event.target.value })} />
                  <button className="primary" disabled={busy}>Create draft</button>
                </form>
              )}
            </div>

            <div className="panel wide">
              <div className="panel-heading">
                <h2>Feature flags</h2>
                <span>{featureFlags.length}</span>
              </div>
              <div className="table">
                {featureFlags.length === 0 ? <p className="empty">No feature flags</p> : null}
                {featureFlags.map((flag) => (
                  <div className="table-row flag-row" key={flag.id}>
                    <div>
                      <strong>{flag.name}</strong>
                      <span>{flag.key} / {flag.kind}</span>
                      <span>{flag.description || "No description"}</span>
                      <span>Changed {formatDate(flag.lastChangedAt)}</span>
                    </div>
                    <span className={flag.enabled ? "status-on" : "status-off"}>{flag.enabled ? "Enabled" : "Disabled"}</span>
                    <div className="flag-controls">
                      {canManageFlags && (
                        <>
                          <input
                            placeholder={selectedEnvironmentIsProduction ? "Reason required for production" : "Reason optional"}
                            value={flagReasons[flag.id] ?? ""}
                            onChange={(event) => setFlagReasons({ ...flagReasons, [flag.id]: event.target.value })}
                          />
                          <button onClick={() => toggleFeatureFlag(flag)} disabled={busy}>
                            {flag.enabled ? "Disable" : "Enable"}
                          </button>
                        </>
                      )}
                      {canReadControlActions && (
                        <button onClick={() => void loadFeatureFlagChanges(flag.id)} disabled={busy}>History</button>
                      )}
                    </div>
                  </div>
                ))}
              </div>
              {selectedHistoryFlag && (
                <div className="flag-history">
                  <strong>{selectedHistoryFlag.key} history</strong>
                  {featureFlagChanges.length === 0 ? <p className="empty">No history entries</p> : null}
                  {featureFlagChanges.map((change) => (
                    <div className="audit-row" key={change.id}>
                      <time>{formatDate(change.changedAt)}</time>
                      <strong>{change.oldValue ? "Enabled" : "Disabled"} {">"} {change.newValue ? "Enabled" : "Disabled"}</strong>
                      <span>{change.changedByEmail}</span>
                      <p>{change.reason || "No reason"}</p>
                    </div>
                  ))}
                </div>
              )}
              {selectedEnvironment && canManageFlags && (
                <form className="inline-form flag-form" onSubmit={createFeatureFlag}>
                  <input required placeholder="flag.key" value={flagForm.key} onChange={(event) => setFlagForm({ ...flagForm, key: event.target.value })} />
                  <input placeholder="Name" value={flagForm.name} onChange={(event) => setFlagForm({ ...flagForm, name: event.target.value })} />
                  <input placeholder="Description" value={flagForm.description} onChange={(event) => setFlagForm({ ...flagForm, description: event.target.value })} />
                  <select value={flagForm.kind} onChange={(event) => setFlagForm({ ...flagForm, kind: event.target.value })}>
                    <option value="FeatureFlag">Feature flag</option>
                    <option value="KillSwitch">Kill switch</option>
                  </select>
                  <label className="checkbox-row">
                    <input type="checkbox" checked={flagForm.enabled} onChange={(event) => setFlagForm({ ...flagForm, enabled: event.target.checked })} />
                    Enabled
                  </label>
                  <input
                    placeholder={selectedEnvironmentIsProduction ? "Reason required for production" : "Reason optional"}
                    value={flagForm.reason}
                    onChange={(event) => setFlagForm({ ...flagForm, reason: event.target.value })}
                  />
                  <button className="primary" disabled={busy}>Create flag</button>
                </form>
              )}
            </div>

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
                <div className="scroll-section">
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
              </div>
            )}

            {canReadControlActions && (
              <div className="panel">
                <div className="panel-heading">
                  <h2>Control actions</h2>
                  <span>{controlActions.length}</span>
                </div>
                <div className="scroll-section">
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
