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

