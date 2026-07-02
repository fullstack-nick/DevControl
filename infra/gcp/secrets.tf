resource "random_password" "postgres" {
  length           = 32
  special          = true
  override_special = "_-"
}

resource "random_password" "scheduler" {
  length           = 32
  special          = true
  override_special = "_-"
}

resource "random_password" "metrics_scrape_token" {
  length  = 48
  special = false
}

resource "random_password" "grafana_admin_password" {
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

resource "google_secret_manager_secret" "scheduler_secret" {
  secret_id = "${local.app_name}-scheduler-secret"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "scheduler_secret" {
  secret      = google_secret_manager_secret.scheduler_secret.id
  secret_data = random_password.scheduler.result
}

resource "google_secret_manager_secret" "metrics_scrape_token" {
  secret_id = "${local.app_name}-metrics-scrape-token"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "metrics_scrape_token" {
  secret      = google_secret_manager_secret.metrics_scrape_token.id
  secret_data = random_password.metrics_scrape_token.result
}

resource "google_secret_manager_secret" "grafana_admin_password" {
  secret_id = "${local.app_name}-grafana-admin-password"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "grafana_admin_password" {
  secret      = google_secret_manager_secret.grafana_admin_password.id
  secret_data = random_password.grafana_admin_password.result
}

resource "google_secret_manager_secret" "prometheus_config" {
  secret_id = "${local.app_name}-prometheus-config"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "prometheus_config" {
  secret = google_secret_manager_secret.prometheus_config.id
  secret_data = templatefile("${path.module}/../../observability/prometheus/prometheus.gcp.yml.tftpl", {
    devcontrol_metrics_target = trimsuffix(trimprefix(google_cloud_run_v2_service.devcontrol.uri, "https://"), "/")
  })
}

resource "google_secret_manager_secret" "grafana_datasource_config" {
  secret_id = "${local.app_name}-grafana-datasource-config"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "grafana_datasource_config" {
  secret      = google_secret_manager_secret.grafana_datasource_config.id
  secret_data = file("${path.module}/../../observability/grafana/provisioning/datasources/prometheus.gcp.yml")
}

resource "google_secret_manager_secret" "grafana_dashboard_provider" {
  secret_id = "${local.app_name}-grafana-dashboard-provider"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "grafana_dashboard_provider" {
  secret      = google_secret_manager_secret.grafana_dashboard_provider.id
  secret_data = file("${path.module}/../../observability/grafana/provisioning/dashboards/dashboards.yml")
}

resource "google_secret_manager_secret" "grafana_stage_9_dashboard" {
  secret_id = "${local.app_name}-grafana-stage-9-dashboard"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "grafana_stage_9_dashboard" {
  secret      = google_secret_manager_secret.grafana_stage_9_dashboard.id
  secret_data = file("${path.module}/../../observability/grafana/dashboards/devcontrol-stage-9.json")
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

  lifecycle {
    ignore_changes = [secret_data]
  }
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

  lifecycle {
    ignore_changes = [secret_data]
  }
}

resource "google_secret_manager_secret" "operator_bootstrap_secret" {
  count     = var.operator_bootstrap_secret == "" ? 0 : 1
  secret_id = "${local.app_name}-operator-bootstrap-secret"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "operator_bootstrap_secret" {
  count       = var.operator_bootstrap_secret == "" ? 0 : 1
  secret      = google_secret_manager_secret.operator_bootstrap_secret[0].id
  secret_data = var.operator_bootstrap_secret

  lifecycle {
    ignore_changes = [secret_data]
  }
}

resource "google_secret_manager_secret" "github_app_private_key" {
  count     = var.github_app_private_key == "" ? 0 : 1
  secret_id = "${local.app_name}-github-app-private-key"

  labels = local.labels

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "github_app_private_key" {
  count       = var.github_app_private_key == "" ? 0 : 1
  secret      = google_secret_manager_secret.github_app_private_key[0].id
  secret_data = var.github_app_private_key

  lifecycle {
    ignore_changes = [secret_data]
  }
}
