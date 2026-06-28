resource "random_password" "postgres" {
  length           = 32
  special          = true
  override_special = "_-"
}

resource "google_secret_manager_secret" "postgres_password" {
  secret_id = "${local.app_name}-postgres-password"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "postgres_password" {
  secret      = google_secret_manager_secret.postgres_password.id
  secret_data = random_password.postgres.result
}

resource "google_secret_manager_secret" "google_oauth_client_secret" {
  count     = var.auth_google_client_secret == "" ? 0 : 1
  secret_id = "${local.app_name}-google-oauth-client-secret"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "google_oauth_client_secret" {
  count       = var.auth_google_client_secret == "" ? 0 : 1
  secret      = google_secret_manager_secret.google_oauth_client_secret[0].id
  secret_data = var.auth_google_client_secret
}

resource "google_secret_manager_secret" "smtp_password" {
  count     = var.smtp_password == "" ? 0 : 1
  secret_id = "${local.app_name}-smtp-password"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "smtp_password" {
  count       = var.smtp_password == "" ? 0 : 1
  secret      = google_secret_manager_secret.smtp_password[0].id
  secret_data = var.smtp_password
}
