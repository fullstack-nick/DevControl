# Stage 2 Tenant and Security

Stage 2 is the authorization foundation for DevControl. It adds authenticated
users, organizations, active organization memberships, email invitations,
projects, environments, audit logs, and the base `ControlActions` table.

## Local sign-in

Development and test environments can sign in without Google credentials:

```text
http://localhost:8080/auth/login?email=developer@devcontrol.local
```

The first signed-in user can create an organization and becomes its `Owner`.

## Production Google sign-in

Production uses Google OAuth/OIDC through app cookies. Configure the Google
OAuth consent screen with this redirect URI:

```text
https://YOUR_CLOUD_RUN_HOST/signin-google
```

Set these Terraform variables when Google auth is ready:

```powershell
$env:TF_VAR_auth_google_client_id = "..."
$env:TF_VAR_auth_google_client_secret = "..."
```

The client secret is stored in Secret Manager and exposed to Cloud Run as
`DEVCONTROL_AUTH_GOOGLE_CLIENT_SECRET`.

## Invitation email

Invitation delivery is provider-agnostic. The default `email_mode` is `log`, so
local and deployed environments stay usable before SMTP is configured.

SMTP variables:

```powershell
$env:TF_VAR_email_mode = "smtp"
$env:TF_VAR_email_from_address = "devcontrol@example.com"
$env:TF_VAR_smtp_host = "smtp.example.com"
$env:TF_VAR_smtp_port = "587"
$env:TF_VAR_smtp_username = "..."
$env:TF_VAR_smtp_password = "..."
```

`smtp_password` is stored in Secret Manager. No service-account JSON key is
used.

## RBAC

Roles are ordered:

```text
Owner > Admin > Developer > Viewer
```

- `Owner`: can manage owner roles and all organization settings.
- `Admin`: can manage organization settings, members, invitations, and audit
  logs.
- `Developer`: can manage projects and environments and view control actions.
- `Viewer`: can read organization, project, and environment data.

DevControl prevents removing or demoting the last active owner.

## Tenant scoping

All organization, project, environment, audit, and control-action endpoints are
scoped through the authenticated user's active organization membership.

Expected authorization behavior:

```text
Unauthenticated request -> 401
Known member without enough role -> 403
Missing resource or cross-tenant resource -> 404
```

Important successful mutations write audit rows in the same database save as the
mutation. Denied mutating attempts are audited when the organization can be
identified.
