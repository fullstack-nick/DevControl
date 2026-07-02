resource "google_cloud_run_v2_service" "devcontrol_observability" {
  name                = "${local.app_name}-observability"
  location            = var.region
  ingress             = "INGRESS_TRAFFIC_ALL"
  deletion_protection = false
  labels              = merge(local.labels, { component = "observability" })

  template {
    service_account = google_service_account.observability.email

    scaling {
      min_instance_count = 0
      max_instance_count = 1
    }

    volumes {
      name = "prometheus-config"

      secret {
        secret = google_secret_manager_secret.prometheus_config.secret_id

        items {
          version = "latest"
          path    = "prometheus.yml"
        }
      }
    }

    volumes {
      name = "prometheus-token"

      secret {
        secret = google_secret_manager_secret.metrics_scrape_token.secret_id

        items {
          version = "latest"
          path    = "metrics-scrape-token"
        }
      }
    }

    volumes {
      name = "grafana-datasources"

      secret {
        secret = google_secret_manager_secret.grafana_datasource_config.secret_id

        items {
          version = "latest"
          path    = "prometheus.yml"
        }
      }
    }

    volumes {
      name = "grafana-dashboard-providers"

      secret {
        secret = google_secret_manager_secret.grafana_dashboard_provider.secret_id

        items {
          version = "latest"
          path    = "dashboards.yml"
        }
      }
    }

    volumes {
      name = "grafana-dashboards"

      secret {
        secret = google_secret_manager_secret.grafana_stage_9_dashboard.secret_id

        items {
          version = "latest"
          path    = "devcontrol-stage-9.json"
        }
      }
    }

    containers {
      name       = "grafana"
      image      = "grafana/grafana:13.1.0"
      depends_on = ["prometheus"]

      ports {
        container_port = 3000
      }

      env {
        name  = "GF_SECURITY_ADMIN_USER"
        value = "admin"
      }

      env {
        name = "GF_SECURITY_ADMIN_PASSWORD"

        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.grafana_admin_password.secret_id
            version = "latest"
          }
        }
      }

      env {
        name  = "GF_AUTH_ANONYMOUS_ENABLED"
        value = "false"
      }

      env {
        name  = "GF_AUTH_BASIC_ENABLED"
        value = "false"
      }

      env {
        name  = "GF_AUTH_DISABLE_LOGIN_FORM"
        value = "true"
      }

      env {
        name  = "GF_AUTH_PROXY_ENABLED"
        value = "true"
      }

      env {
        name  = "GF_AUTH_PROXY_HEADER_NAME"
        value = "X-WEBAUTH-USER"
      }

      env {
        name  = "GF_AUTH_PROXY_HEADER_PROPERTY"
        value = "username"
      }

      env {
        name  = "GF_AUTH_PROXY_HEADERS"
        value = "Name:X-WEBAUTH-NAME Email:X-WEBAUTH-EMAIL"
      }

      env {
        name  = "GF_AUTH_PROXY_AUTO_SIGN_UP"
        value = "true"
      }

      env {
        name  = "GF_USERS_ALLOW_SIGN_UP"
        value = "false"
      }

      env {
        name  = "GF_USERS_AUTO_ASSIGN_ORG_ROLE"
        value = "Viewer"
      }

      env {
        name  = "GF_SERVER_ROOT_URL"
        value = "${google_cloud_run_v2_service.devcontrol.uri}/observability/"
      }

      env {
        name  = "GF_SERVER_SERVE_FROM_SUB_PATH"
        value = "true"
      }

      env {
        name  = "GF_SECURITY_COOKIE_SECURE"
        value = "true"
      }

      env {
        name  = "GF_DASHBOARDS_DEFAULT_HOME_DASHBOARD_PATH"
        value = "/var/lib/grafana/dashboards/devcontrol-stage-9.json"
      }

      volume_mounts {
        name       = "grafana-datasources"
        mount_path = "/etc/grafana/provisioning/datasources"
      }

      volume_mounts {
        name       = "grafana-dashboard-providers"
        mount_path = "/etc/grafana/provisioning/dashboards"
      }

      volume_mounts {
        name       = "grafana-dashboards"
        mount_path = "/var/lib/grafana/dashboards"
      }

      resources {
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }

        cpu_idle          = true
        startup_cpu_boost = true
      }
    }

    containers {
      name  = "prometheus"
      image = "prom/prometheus:v3.13.0"
      args = [
        "--config.file=/etc/prometheus/prometheus.yml",
        "--storage.tsdb.path=/tmp/prometheus",
        "--storage.tsdb.retention.time=6h",
        "--web.listen-address=0.0.0.0:9090",
        "--web.enable-lifecycle"
      ]

      volume_mounts {
        name       = "prometheus-config"
        mount_path = "/etc/prometheus"
      }

      volume_mounts {
        name       = "prometheus-token"
        mount_path = "/etc/prometheus/secrets"
      }

      resources {
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }

        cpu_idle          = true
        startup_cpu_boost = true
      }

      startup_probe {
        initial_delay_seconds = 0
        period_seconds        = 5
        timeout_seconds       = 2
        failure_threshold     = 12

        http_get {
          path = "/-/ready"
          port = 9090
        }
      }
    }
  }

  depends_on = [
    google_cloud_run_v2_service.devcontrol,
    google_secret_manager_secret_iam_member.observability_can_read_metrics_scrape_token,
    google_secret_manager_secret_iam_member.observability_can_read_grafana_admin_password,
    google_secret_manager_secret_iam_member.observability_can_read_prometheus_config,
    google_secret_manager_secret_iam_member.observability_can_read_grafana_datasource_config,
    google_secret_manager_secret_iam_member.observability_can_read_grafana_dashboard_provider,
    google_secret_manager_secret_iam_member.observability_can_read_grafana_stage_9_dashboard
  ]

  lifecycle {
    ignore_changes = [
      scaling
    ]
  }
}

resource "google_cloud_run_v2_service_iam_member" "observability_runtime_invoker" {
  project  = var.project_id
  location = google_cloud_run_v2_service.devcontrol_observability.location
  name     = google_cloud_run_v2_service.devcontrol_observability.name
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.runtime.email}"
}
