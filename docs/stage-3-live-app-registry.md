# Stage 3 Live App Registry

Stage 3 adds passive live-app registration. DevControl does not deploy user
apps in this stage. An app keeps its own deploy workflow, then reports the
deployed runtime facts back to DevControl with a scoped registration token.

## User flow

1. Sign in to DevControl.
2. Select an organization, project, and environment.
3. Create a registration token.
4. Copy the generated GitHub Actions snippet into the app repo's existing
   deploy workflow after the deploy and health-smoke-test steps.
5. The next app deployment runs `devcontrol apps register` and the app appears
   in the Live apps panel.

The token is shown once. Store it as a GitHub Actions secret named
`DEVCONTROL_TOKEN`.

## CLI registration

The CLI can read `DEVCONTROL_SERVER`, `DEVCONTROL_TOKEN`,
`GITHUB_REPOSITORY`, and `GITHUB_SHA`, so workflows only need to pass the
runtime values that come from their deploy step:

```bash
devcontrol apps register \
  --environment production \
  --service-url https://my-app.example.com \
  --health-url https://my-app.example.com/health \
  --version "$VERSION" \
  --image-digest "$IMAGE_DIGEST" \
  --capabilities health,deployment-events \
  --json
```

## Non-goals

Stage 3 intentionally does not include GitHub App installation, automatic PR
creation, `workflow_dispatch`, deploy/redeploy/rollback controls, monitors, or
health polling. GitHub App PR onboarding should come later, followed by
workflow-dispatch live control after the registry contract is proven.
