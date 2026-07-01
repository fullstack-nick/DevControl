# Stage 7 Monitoring, Incidents, and Status Page

Stage 7 adds uptime monitors, scheduled checks, incident automation, incident
updates, public project status pages, and manually published release notes.

## Monitoring

Live app registration creates or updates one managed uptime monitor per app.
The monitor keeps user-edited settings such as pause state and thresholds while
registration refreshes the health URL and display name.

Defaults:

- interval: 5 minutes
- timeout: 5 seconds
- slow threshold: 2 seconds
- incident trigger: 1 failed check
- recovery trigger: 1 successful check

Monitor checks use the shared SSRF-safe outbound HTTP layer with the monitor
policy:

- HTTP and HTTPS allowed
- ports 80 and 443 only
- 5 second default timeout
- 64 KB response read cap
- 4 KB stored response preview
- up to 2 redirects, with every hop revalidated against private/internal
  address ranges

Webhooks keep their stricter HTTPS-only, no-redirect policy.

## Scheduler

The existing Cloud Scheduler request still calls:

```text
POST /internal/scheduler/tick
X-DevControl-Scheduler-Secret: ...
```

The tick now runs a bounded monitor-check batch first, then the existing
webhook retry batch. This keeps Cloud Run request-based billing intact and does
not introduce an always-on worker.

## Incidents

Automated monitor incidents use this lifecycle:

```text
Investigating -> Identified -> Monitoring -> Resolved
```

When a monitor goes down, DevControl opens one active incident for that monitor
and writes a public timeline update. When the monitor recovers, DevControl
resolves the linked active incident and writes another public update. Manual
incident updates can be public or private; the public status page only exposes
public updates.

## Status page and releases

Public status pages are available at:

```text
/status/{organizationSlug}/{projectSlug}
/status/{organizationSlug}/{projectSlug}?environment={environmentSlug}
```

The page exposes current monitor state, public incident history, 24-hour uptime
summary, and published release notes. Draft releases are visible only in the
authenticated dashboard until an Admin publishes them.

## Webhook events

Stage 7 adds:

```text
monitor.down
monitor.recovered
incident.created
incident.updated
incident.resolved
release.published
```

Delivery, signing, retries, and response previews are the same Stage 6 webhook
mechanism.

## Live proof

Stage 7 is complete only after the code is pushed and live on GCP.

Expected proof:

1. GitHub CI and deploy workflow succeed.
2. DevControl Cloud Run `/health/live` and `/health/ready` return success.
3. The separate sample app is live and registered back into DevControl.
4. Enabling the sample app `health.force_down` kill switch makes `/health`
   return `503`.
5. The scheduler detects the failure, records monitor history, opens an
   incident, and updates the public status page.
6. Disabling the kill switch returns `/health` to healthy; the scheduler records
   recovery and resolves the incident.
7. A release note is published and appears on the public status page.
8. PostgreSQL readback confirms monitor, check, incident, update, release, and
   webhook rows.
