# Stage 8 GitHub App, Repo Onboarding, and Live Control

Stage 8 adds the real GitHub App integration on top of the existing live app
registry and control action model.

## GitHub App permissions

Configure the GitHub App with only these repository permissions:

- Metadata: read
- Contents: read/write
- Workflows: write
- Pull requests: write
- Actions: read/write

Do not grant Administration, Secrets, or Variables. DevControl does not create
repository secrets for managed onboarding.

## Runtime configuration

Terraform accepts:

```text
github_app_id
github_app_private_key
```

The private key is stored in Secret Manager and exposed to Cloud Run as
`DEVCONTROL_GITHUB_APP_PRIVATE_KEY`. Installation access tokens are generated on
demand and are never stored in PostgreSQL.

## Onboarding flow

An Admin can resolve a GitHub repo, select one workflow and job, then open a
DevControl-generated pull request. The PR patches a selected
`.github/workflows/*.yml` file by:

1. adding `id-token: write` if missing,
2. inserting a marker-protected DevControl registration block under the selected
   job's `steps`,
3. installing the DevControl CLI,
4. requesting a GitHub Actions OIDC token with the DevControl registration
   endpoint as audience,
5. running `devcontrol apps register` without a DevControl repo secret.

If the workflow cannot be patched safely, the API returns a manual OIDC snippet
that the UI shows to the Admin.

## Registration compatibility

The Stage 3 bearer-token registration path still works. The Stage 8 managed path
adds `gitHubOidcToken` to `/api/apps/register`; DevControl verifies the token,
matches the trusted repo and workflow claims to an existing repo connection, and
stores the GitHub Actions run id/url on the live app and deployment record.

## Live control

Admins can dispatch repo-owned workflows through:

```text
POST /api/organizations/{organizationId}/apps/{liveAppId}/actions/deploy
POST /api/organizations/{organizationId}/apps/{liveAppId}/actions/redeploy
POST /api/organizations/{organizationId}/apps/{liveAppId}/actions/rollback
```

DevControl gates each action on the live app capabilities declared during
registration. Rollback also requires a deployment id from that live app's
deployment history.

Each dispatch creates a `ControlAction` and a GitHub workflow-dispatch tracking
row. The scheduler tick polls GitHub for onboarding PR state and workflow run
state, then maps successful runs to `Succeeded` and failed, cancelled, or timed
out runs to the corresponding `ControlActionStatus`.

## Live proof

Stage 8 is complete only after:

1. code is pushed to `origin/main`,
2. GitHub Actions deploys DevControl to Cloud Run,
3. `/health/live` and `/health/ready` succeed on the live service,
4. the GitHub App is installed on `fullstack-nick/devcontrol-sample-live-app`,
5. DevControl opens an onboarding PR against the sample repo and that PR is
   reviewed/merged,
6. the next sample deployment registers with GitHub run metadata,
7. an Admin triggers deploy, redeploy, and rollback from DevControl,
8. GitHub run status, `ControlAction`, audit logs, deployment history, and
   PostgreSQL readback confirm the flow.
