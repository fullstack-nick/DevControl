# DevControl

DevControl is a developer operations control plane for applications that already
deploy from GitHub. It gives a small team one place to register live apps,
observe health, manage runtime keys, change feature flags, send webhooks, track
incidents, publish status updates, and trigger approved GitHub workflow actions.

The app is built as a .NET API with a React frontend, PostgreSQL persistence,
GitHub Actions delivery, and a lean Cloud Run deployment. Local development can
run the full stack with Docker Compose, including Prometheus and Grafana.

## Live Demo Links

- Live app: <https://devcontrol-nictbzfhga-uc.a.run.app/>
- Sample live app: <https://devcontrol-sample-live-app-nictbzfhga-uc.a.run.app/>
- Live health: <https://devcontrol-nictbzfhga-uc.a.run.app/health/live>
- Live readiness: <https://devcontrol-nictbzfhga-uc.a.run.app/health/ready>
- Live observability: <https://devcontrol-nictbzfhga-uc.a.run.app/observability/>
- Public sample status page: <https://devcontrol-nictbzfhga-uc.a.run.app/status/acme-platform/sample-app?environment=production>
- DevControl repo: <https://github.com/fullstack-nick/DevControl>
- Sample live app repo: <https://github.com/fullstack-nick/devcontrol-sample-live-app>

`/observability/` requires signing in to DevControl and belonging to an
organization in the app. The raw Grafana service is not public; DevControl
proxies it after checking app authorization.

## What It Does

- Google sign-in plus local development sign-in
- Organizations, projects, environments, members, invitations, and RBAC
- Audit logs and control actions for important mutations
- Live app registration through CLI/GitHub Actions
- GitHub App repo onboarding PRs that add DevControl registration to existing workflows
- GitHub `workflow_dispatch` controls for deploy, redeploy, and rollback
- Runtime API keys with show-once secrets, scopes, rotation, revocation, usage, latency, failures, and rate-limit hits
- Feature flags and kill switches with audited change history
- Runtime flag snapshots with ETags for cached SDK/local evaluation
- SSRF-safe outbound HTTP and HMAC-signed webhooks
- Uptime monitors, automatic incidents, recovery tracking, public status pages, and releases
- Local and live-on-demand Prometheus/Grafana observability
- PostgreSQL backup/restore scripts, retention cleanup, and free-tier guard checks

## Live Demo Walkthrough

Use this path to exercise the complete live product. Some GitHub-control steps
require a GitHub repository that you own and that has the DevControl GitHub App
installed.

For a public demo, use the sample app for the stable owner-operated path. Before
presenting, run the fresh throwaway repo path once as the cold-start proof that
repo onboarding is still honest and not dependent on preconfigured sample-app
state.

### 1. Sign In And Create A Tenant

1. Open <https://devcontrol-nictbzfhga-uc.a.run.app/>.
2. Sign in with Google.
3. Create an organization.
4. Create a project.
5. Create a `Production` environment with slug `production`.
6. Confirm the header shows the selected organization, project, and environment.

### 2. Check RBAC, Audit, And Control History

1. Open the organization/member area.
2. Invite a test email if you want to exercise invitation flow.
3. Change a member role or revoke an invitation if you created one.
4. Open the Audit log and Control actions panels.
5. Confirm new entries appear and that both panels scroll internally instead of expanding the whole page.

### 3. Register A Live App

There are two honest ways to demo this:

- Use the sample app repo for a stable, repeatable end-to-end demo. It is already designed to expose health, API-key, feature-flag, deploy, redeploy, and rollback behavior.
- Use a fresh throwaway repo for a first-time onboarding demo. This proves DevControl can add itself to a repo that was not already wired for the product.

DevControl does not turn an arbitrary source repo into a deployable app by
itself. It works with repos that already have, or can be adapted to have, a
normal deployment surface:

- A GitHub Actions workflow that deploys the app.
- A stable workflow job that runs after checkout/build/deploy.
- A deployed service URL that is known after deployment, either as a literal
  URL or as a workflow expression/output.
- A reachable health URL that returns `2xx` when the app is healthy.
- Commit/version/image values that can be supplied from GitHub context,
  workflow outputs, or literals.
- Optional `workflow_dispatch` inputs if you want DevControl buttons for
  deploy, redeploy, or rollback.

Only declare capabilities the repo truly supports. For example, use `health`
and `deployment-events` for passive registration; add `deploy`, `redeploy`, or
`rollback` only when the workflow has matching `workflow_dispatch` behavior.

For a fresh repo that meets those requirements, open DevControl:

1. Open GitHub onboarding.
2. Enter the repo as `owner/name` or a GitHub URL.
3. Resolve the repo.
4. Select the deploy workflow and job.
5. Use expressions or literals for service URL, health URL, version, and image digest.
6. Keep capabilities such as `health,deployment-events,deploy,redeploy,rollback` if the workflow supports them.
7. Create the onboarding PR.
8. Review and merge the PR in GitHub.
9. Run the app's deploy workflow.
10. Return to DevControl and confirm the app appears in Live apps with repo, service URL, health URL, commit, version, image digest, and capabilities.

If you only want passive registration, create a registration token in DevControl
and paste the generated workflow snippet into an app's deploy workflow after the
deploy and health check steps.

### 4. Trigger Live Control

After a live app has registered with `deploy`, `redeploy`, or `rollback`
capabilities:

1. Open the Live apps panel.
2. Click deploy or redeploy with a reason.
3. Watch the GitHub workflow dispatch entry appear in DevControl.
4. Open the linked GitHub run.
5. After multiple deployments exist, trigger rollback and choose the target deployment.
6. Confirm audit log and control action entries record the request and result.

### 5. Create And Use Runtime API Keys

1. Open API keys.
2. Create a key named `Demo sample key` with scope `sample:read`.
3. Copy the show-once secret immediately.
4. Call the protected runtime endpoint:

```bash
curl -H "Authorization: Bearer dck_..." \
  "https://devcontrol-nictbzfhga-uc.a.run.app/api/runtime/sample/echo?delayMs=100"
```

5. Call a failure case:

```bash
curl -H "Authorization: Bearer dck_..." \
  "https://devcontrol-nictbzfhga-uc.a.run.app/api/runtime/sample/echo?status=500"
```

6. Call the endpoint repeatedly to hit the per-minute rate limit.
7. Refresh DevControl and confirm request count, failures, average latency,
   last-used time, and rate-limit hits update.
8. Rotate the key and confirm the old secret stops working.
9. Revoke the key and confirm runtime calls are rejected.

### 6. Create Feature Flags And Read Runtime Snapshots

1. Open Feature flags.
2. Create a feature flag such as `checkout.enabled`.
3. Create a kill switch such as `checkout.kill`.
4. Toggle each value with a reason.
5. Open the change history and confirm the old/new values are recorded.
6. Create a second API key with scope `flags:read`.
7. Read the runtime snapshot:

```bash
curl -i -H "Authorization: Bearer dck_..." \
  "https://devcontrol-nictbzfhga-uc.a.run.app/api/runtime/flags/snapshot"
```

8. Copy the returned `ETag` and call again with `If-None-Match` to confirm
   unchanged snapshots return `304 Not Modified`.

The sample app also consumes DevControl flags through the C# SDK, caches
snapshots, and evaluates flags locally between refreshes.

### 7. Send Webhooks

1. Open Webhooks.
2. Create a webhook endpoint using a temporary public receiver such as webhook.site.
3. Select `webhook.test`.
4. Copy the show-once signing secret.
5. Send a test delivery.
6. Confirm the receiver gets an HMAC-signed request.
7. Open delivery history and inspect status, attempt count, response status, and bounded response preview.
8. Pause and resume the endpoint.
9. Retry a delivery if one fails.

Private, loopback, and metadata URLs are blocked by the safe outbound client.

### 8. Monitor Health, Incidents, And Status Page

1. Open Operations.
2. Create an uptime monitor for a public `200` health URL.
3. Wait for the scheduler check to mark it up.
4. Create another monitor or temporarily change a monitor URL to a failing public endpoint.
5. Wait for the scheduler tick to create an incident.
6. Restore the monitor URL to a healthy endpoint.
7. Wait for recovery and confirm the incident resolves.
8. Open the public status page from the Operations panel.
9. Confirm monitors, incidents, and public updates are visible without signing in.

The sample public status page is available at
<https://devcontrol-nictbzfhga-uc.a.run.app/status/acme-platform/sample-app?environment=production>.

### 9. Publish A Release

1. Open Releases.
2. Create a release note with a title, version, and body.
3. Publish it.
4. Open the public status page and confirm the release appears.

### 10. Open Live Observability

1. Open <https://devcontrol-nictbzfhga-uc.a.run.app/observability/>.
2. If prompted, sign in through DevControl first.
3. Wait at least one Prometheus scrape interval.
4. Generate traffic by refreshing DevControl, calling runtime endpoints, sending a webhook test, or waiting for monitors.
5. Confirm Grafana shows scrape target health, request counts, latency, scheduler work, monitor checks, webhook outcomes, API-key traffic, cleanup activity, and runtime metrics.

Live observability is on-demand. Prometheus storage is ephemeral, so the
dashboard may start empty after Cloud Run scales to zero.

## Local Development

Prerequisites:

- .NET SDK 10
- Node.js 22
- Docker

Run the app with PostgreSQL:

```powershell
docker compose up --build
```

Open:

```text
http://localhost:8080
http://localhost:8080/health/live
http://localhost:8080/health/ready
```

Run the local observability profile:

```powershell
docker compose --profile observability up --build
```

Open:

```text
http://localhost:8080
http://localhost:8080/metrics
http://localhost:9090
http://localhost:3000
```

Run tests:

```powershell
npm ci --prefix src/DevControl.Web
npm run build --prefix src/DevControl.Web
dotnet restore DevControl.sln
dotnet build DevControl.sln --configuration Release
dotnet test DevControl.sln --configuration Release
```

## Deployment And Operator Scripts

The live deployment runs on Cloud Run in `us-central1` with `min-instances=0`
and `max-instances=1`. PostgreSQL runs on an Always Free eligible `e2-micro`
VM, and backups go to a private short-retention Cloud Storage bucket.

Before running GCP scripts, configure the operator account and project:

```powershell
$env:DEVCONTROL_GCP_REQUIRED_ACCOUNT = "<operator-google-account>"
$env:DEVCONTROL_GCP_PROJECT_ID = "<gcp-project-id>"
$env:DEVCONTROL_GITHUB_OWNER = "<github-owner>"
$env:DEVCONTROL_GITHUB_REPO = "DevControl"
```

Useful operator checks:

```powershell
.\scripts\gcp\smoke-test-cloud-run.ps1 -ServiceUrl "https://<cloud-run-url>"
.\scripts\gcp\smoke-test-live-observability.ps1 -BaseUrl "https://<cloud-run-url>"
.\scripts\gcp\assert-free-tier-guards.ps1
.\scripts\gcp\backup-postgres.ps1
.\scripts\gcp\verify-postgres-restore.ps1
```

GitHub Actions deploys through Workload Identity Federation. Do not create or
commit service-account JSON keys.
