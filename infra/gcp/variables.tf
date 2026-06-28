variable "project_id" {
  description = "GCP project ID created by scripts/gcp/bootstrap-project.ps1."
  type        = string
}

variable "operator_google_account" {
  description = "The only user account allowed to bootstrap or mutate DevControl GCP resources locally."
  type        = string
  default     = "nickaccturk@gmail.com"

  validation {
    condition     = var.operator_google_account == "nickaccturk@gmail.com"
    error_message = "DevControl GCP operations must use nickaccturk@gmail.com."
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

