variable "project_id" {
  description = "GCP project ID created by scripts/gcp/bootstrap-project.ps1."
  type        = string
}

variable "operator_google_account" {
  description = "The human Google account allowed to bootstrap or mutate DevControl GCP resources locally. Supply through TF_VAR_operator_google_account."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.operator_google_account))
    error_message = "operator_google_account must be a valid email address."
  }
}

variable "region" {
  description = "Strict free-tier region."
  type        = string
  default     = "us-central1"

  validation {
    condition     = var.region == "us-central1"
    error_message = "Stage 1 is locked to us-central1 for strict Always Free eligibility."
  }
}

variable "zone" {
  description = "Strict free-tier zone."
  type        = string
  default     = "us-central1-a"
}

variable "github_owner" {
  description = "GitHub repository owner used by Workload Identity Federation."
  type        = string
}

variable "github_repo" {
  description = "GitHub repository name used by Workload Identity Federation."
  type        = string
}

variable "github_allowed_repositories" {
  description = "Explicit GitHub repositories allowed to impersonate the deployer service account through Workload Identity Federation."
  type        = list(string)
  default     = []
}

variable "subnet_cidr" {
  description = "Primary private subnet CIDR for Cloud Run direct VPC egress and PostgreSQL."
  type        = string
  default     = "10.10.0.0/24"
}

variable "postgres_private_ip" {
  description = "Static private IP for the PostgreSQL VM."
  type        = string
  default     = "10.10.0.10"
}

variable "postgres_database" {
  description = "DevControl PostgreSQL database name."
  type        = string
  default     = "devcontrol"
}

variable "postgres_username" {
  description = "DevControl PostgreSQL login role."
  type        = string
  default     = "devcontrol"
}

variable "initial_cloud_run_image" {
  description = "Placeholder image used before the first GitHub Actions deployment pushes the real image."
  type        = string
  default     = "us-docker.pkg.dev/cloudrun/container/hello"
}

variable "auth_google_client_id" {
  description = "Google OAuth/OIDC client ID for DevControl user sign-in. Leave empty until production auth is configured."
  type        = string
  default     = ""
}

variable "auth_google_client_secret" {
  description = "Google OAuth/OIDC client secret. Leave empty until production auth is configured."
  type        = string
  default     = ""
  sensitive   = true
}

variable "email_mode" {
  description = "Email delivery mode for invitation mail."
  type        = string
  default     = "log"

  validation {
    condition     = contains(["log", "smtp"], var.email_mode)
    error_message = "email_mode must be log or smtp."
  }
}

variable "email_from_address" {
  description = "From address for DevControl invitation email."
  type        = string
  default     = "devcontrol@localhost"
}

variable "email_from_name" {
  description = "From display name for DevControl invitation email."
  type        = string
  default     = "DevControl"
}

variable "smtp_host" {
  description = "SMTP host for invitation email when email_mode is smtp."
  type        = string
  default     = ""
}

variable "smtp_port" {
  description = "SMTP port for invitation email when email_mode is smtp."
  type        = number
  default     = 587
}

variable "smtp_username" {
  description = "SMTP username for invitation email when email_mode is smtp."
  type        = string
  default     = ""
}

variable "smtp_password" {
  description = "SMTP password for invitation email when email_mode is smtp."
  type        = string
  default     = ""
  sensitive   = true
}

variable "operator_bootstrap_secret" {
  description = "Optional operator secret that enables the audited live-proof bootstrap endpoint."
  type        = string
  default     = ""
  sensitive   = true
}

variable "operator_bootstrap_enabled" {
  description = "Explicit break-glass flag for the audited live-proof bootstrap endpoint. Keep false in normal production."
  type        = bool
  default     = false
}

variable "github_app_id" {
  description = "Optional GitHub App ID used for Stage 8 repo onboarding and live control."
  type        = string
  default     = ""
}

variable "github_app_private_key" {
  description = "Optional GitHub App private key PEM. Stored in Secret Manager when configured."
  type        = string
  default     = ""
  sensitive   = true
}

variable "setup_action_ref" {
  description = "GitHub Actions uses reference inserted into generated DevControl CLI setup snippets."
  type        = string
  default     = "fullstack-nick/DevControl/.github/actions/setup-devcontrol@main"

  validation {
    condition     = can(regex("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(/[A-Za-z0-9_.-]+)*@[-A-Za-z0-9_./]+$", var.setup_action_ref)) && !can(regex("\\.\\.", var.setup_action_ref))
    error_message = "setup_action_ref must be a GitHub action reference such as owner/repo/path@ref."
  }
}

variable "public_base_url" {
  description = "Optional canonical public DevControl base URL used for generated snippets and GitHub OIDC audiences. Leave empty to use the request host."
  type        = string
  default     = ""

  validation {
    condition     = var.public_base_url == "" || can(regex("^https?://\\S+$", var.public_base_url))
    error_message = "public_base_url must be empty or an absolute http/https URL without whitespace."
  }
}

variable "smtp_use_starttls" {
  description = "Whether SMTP delivery should use TLS."
  type        = bool
  default     = true
}
