resource "google_service_account" "runtime" {
  account_id   = "${local.app_name}-runtime"
  display_name = "DevControl Cloud Run runtime"

  depends_on = [google_project_service.required]
}

resource "google_service_account" "postgres_vm" {
  account_id   = "${local.app_name}-postgres-vm"
  display_name = "DevControl PostgreSQL VM"

  depends_on = [google_project_service.required]
}

resource "google_service_account" "github_deployer" {
  account_id   = "${local.app_name}-github-deployer"
  display_name = "DevControl GitHub Actions deployer"

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_iam_member" "runtime_can_read_postgres_password" {
  secret_id = google_secret_manager_secret.postgres_password.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.runtime.email}"
}

resource "google_secret_manager_secret_iam_member" "vm_can_read_postgres_password" {
  secret_id = google_secret_manager_secret.postgres_password.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.postgres_vm.email}"
}

resource "google_secret_manager_secret_iam_member" "runtime_can_read_google_oauth_client_secret" {
  count     = var.auth_google_client_secret == "" ? 0 : 1
  secret_id = google_secret_manager_secret.google_oauth_client_secret[0].id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.runtime.email}"
}

resource "google_secret_manager_secret_iam_member" "runtime_can_read_smtp_password" {
  count     = var.smtp_password == "" ? 0 : 1
  secret_id = google_secret_manager_secret.smtp_password[0].id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.runtime.email}"
}

resource "google_project_iam_member" "github_deployer_run_admin" {
  project = var.project_id
  role    = "roles/run.admin"
  member  = "serviceAccount:${google_service_account.github_deployer.email}"
}

resource "google_project_iam_member" "github_deployer_artifact_writer" {
  project = var.project_id
  role    = "roles/artifactregistry.writer"
  member  = "serviceAccount:${google_service_account.github_deployer.email}"
}

resource "google_service_account_iam_member" "github_deployer_can_act_as_runtime" {
  service_account_id = google_service_account.runtime.name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${google_service_account.github_deployer.email}"
}

resource "google_iam_workload_identity_pool" "github" {
  workload_identity_pool_id = "${local.app_name}-github"
  display_name              = "DevControl GitHub Actions"
  description               = "OIDC pool for DevControl GitHub repositories"

  depends_on = [google_project_service.required]
}

resource "google_iam_workload_identity_pool_provider" "github" {
  workload_identity_pool_id          = google_iam_workload_identity_pool.github.workload_identity_pool_id
  workload_identity_pool_provider_id = "github-actions"
  display_name                       = "GitHub Actions"

  attribute_mapping = {
    "google.subject"       = "assertion.sub"
    "attribute.actor"      = "assertion.actor"
    "attribute.repository" = "assertion.repository"
    "attribute.ref"        = "assertion.ref"
  }

  attribute_condition = local.github_allowed_repository_condition

  oidc {
    issuer_uri = "https://token.actions.githubusercontent.com"
  }
}

resource "google_service_account_iam_member" "github_wif_user" {
  for_each = local.github_allowed_repositories

  service_account_id = google_service_account.github_deployer.name
  role               = "roles/iam.workloadIdentityUser"
  member             = "principalSet://iam.googleapis.com/${google_iam_workload_identity_pool.github.name}/attribute.repository/${each.value}"
}
