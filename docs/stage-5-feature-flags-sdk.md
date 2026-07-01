# Stage 5 Feature Flags and Kill Switch SDK

Stage 5 adds environment-scoped boolean feature flags, boolean kill switches,
runtime snapshots, and the first C# SDK.

## Runtime flow

1. Create an API key with the `flags:read` scope.
2. Create feature flags or kill switches for the target environment.
3. Configure an app with `DEVCONTROL_SERVER` and `DEVCONTROL_API_KEY`.
4. The app uses `DevControl.Sdk` to refresh snapshots.
5. The app evaluates flags locally from the cached snapshot.

Runtime snapshot endpoint:

```text
GET /api/runtime/flags/snapshot
Authorization: Bearer dck_...
```

The endpoint returns only the flags for the API key's organization, project, and
environment. It supports `ETag` and `If-None-Match`; unchanged snapshots return
`304 Not Modified`.

## Governance

Non-production flag and kill-switch changes require `Developer` or higher.

Production is detected by environment slug:

```text
production
```

Production changes require `Admin` or `Owner` plus a non-empty reason. Successful
mutations write:

- `feature_flag_changes`
- `audit_logs`
- `control_actions`

Denied production mutations are audited.

## SDK behavior

The SDK targets `net8.0`.

Local evaluation methods do not make network calls:

```csharp
client.IsEnabled("checkout.enabled", defaultValue: false);
client.IsKilled("checkout.kill", defaultValue: true);
```

Network access happens only through refresh methods:

```csharp
await client.RefreshIfStaleAsync();
await client.RefreshKillSwitchesIfStaleAsync();
```

Defaults:

- request timeout: 2 seconds
- full refresh interval: 60 seconds
- kill-switch refresh interval: 20 seconds
- failed refresh keeps the last usable snapshot
- missing feature flags default to caller-provided values
- missing kill switches default to `true` unless the caller overrides it

## Live proof

The external `devcontrol-sample-live-app` is converted to a .NET Minimal API and
uses `DevControl.Sdk`.

Expected proof route:

```text
/devcontrol-flags-demo
```

The route returns SDK-evaluated flag and kill-switch values plus counters showing
that repeated evaluations are local while snapshot refreshes are bounded.
