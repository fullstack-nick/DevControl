# Stage 4 API Keys and Usage Metering

Stage 4 adds project/environment-scoped runtime API keys and the first metered
protected API path. DevControl remains the source of key authentication and
usage aggregation; sample apps forward caller-provided keys rather than storing
them.

## User flow

1. Sign in to DevControl.
2. Select an organization, project, and environment.
3. Create an API key in the API keys panel.
4. Copy the key immediately. It is shown once and only its prefix is stored.
5. Call the protected sample path directly:

```bash
curl -H "Authorization: Bearer dck_..." \
  "https://devcontrol.example.com/api/runtime/sample/echo?delayMs=100"
```

Or through the sample live app:

```bash
curl -H "Authorization: Bearer dck_..." \
  "https://sample-app.example.com/devcontrol-api-demo?status=500"
```

## Metered behavior

DevControl records attributed runtime calls per API key:

- request count
- failures
- average latency
- last-used time
- rate-limit hits

The V1 runtime scope is:

```text
sample:read
```

The default rate limit is 10 requests per minute per API key and endpoint.

## Non-goals

Stage 4 does not add billing, Redis, a generic analytics warehouse, GitHub App
onboarding, workflow dispatch, or a public SDK. Those stay in later stages.

## Operator live proof

Production live proof should use the audited operator bootstrap path instead
of direct database inserts. Configure `TF_VAR_operator_bootstrap_secret` and set
`TF_VAR_operator_bootstrap_enabled=true` only for the proof window, deploy
DevControl, then run:

```bash
devcontrol admin bootstrap-live-proof \
  --server https://devcontrol.example.com \
  --operator-secret "$DEVCONTROL_OPERATOR_BOOTSTRAP_SECRET" \
  --owner-email owner@example.com \
  --json
```

The endpoint is disabled unless the explicit enable flag and operator secret are
both configured. Turn `TF_VAR_operator_bootstrap_enabled` back to `false` after
the proof window. The returned registration token and API key are show-once
secrets.
