resource "google_cloud_run_v2_service" "devcontrol" {
  name                = local.app_name
  location            = var.region
  ingress             = "INGRESS_TRAFFIC_ALL"
  deletion_protection = false
  labels              = local.labels

  template {
    service_account = google_service_account.runtime.email

    scaling {
      min_instance_count = 0
      max_instance_count = 1
    }

    vpc_access {
      egress = "PRIVATE_RANGES_ONLY"

      network_interfaces {
        network    = google_compute_network.main.name
        subnetwork = google_compute_subnetwork.main.name
      }
    }

    containers {
      image = var.initial_cloud_run_image

      ports {
        container_port = 8080
      }

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      env {
        name  = "DEVCONTROL_RUN_MIGRATIONS_ON_STARTUP"
        value = "true"
      }

      env {
        name  = "ASPNETCORE_FORWARDEDHEADERS_ENABLED"
        value = "true"
      }

      env {
        name  = "DEVCONTROL_POSTGRES_HOST"
        value = var.postgres_private_ip
      }

      env {
        name  = "DEVCONTROL_POSTGRES_PORT"
        value = "5432"
      }

      env {
        name  = "DEVCONTROL_POSTGRES_DATABASE"
        value = var.postgres_database
      }

      env {
        name  = "DEVCONTROL_POSTGRES_USERNAME"
        value = var.postgres_username
      }

      env {
        name = "DEVCONTROL_POSTGRES_PASSWORD"

        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.postgres_password.secret_id
            version = "latest"
          }
        }
      }

      env {
        name = "DEVCONTROL_SCHEDULER_SECRET"

        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.scheduler_secret.secret_id
            version = "latest"
          }
        }
      }

      env {
        name  = "DEVCONTROL_METRICS_ENABLED"
        value = "true"
      }

      env {
        name = "DEVCONTROL_METRICS_SCRAPE_TOKEN"

        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.metrics_scrape_token.secret_id
            version = "latest"
          }
        }
      }

      env {
        name  = "DEVCONTROL_EMAIL_MODE"
        value = var.email_mode
      }

      env {
        name  = "DEVCONTROL_EMAIL_FROM_ADDRESS"
        value = var.email_from_address
      }

      env {
        name  = "DEVCONTROL_EMAIL_FROM_NAME"
        value = var.email_from_name
      }

      env {
        name  = "DEVCONTROL_SMTP_PORT"
        value = tostring(var.smtp_port)
      }

      env {
        name  = "DEVCONTROL_SMTP_USE_STARTTLS"
        value = tostring(var.smtp_use_starttls)
      }

      dynamic "env" {
        for_each = var.auth_google_client_id == "" ? [] : [var.auth_google_client_id]

        content {
          name  = "DEVCONTROL_AUTH_GOOGLE_CLIENT_ID"
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.auth_google_client_secret == "" ? [] : [1]

        content {
          name = "DEVCONTROL_AUTH_GOOGLE_CLIENT_SECRET"

          value_source {
            secret_key_ref {
              secret  = google_secret_manager_secret.google_oauth_client_secret[0].secret_id
              version = "latest"
            }
          }
        }
      }

      dynamic "env" {
        for_each = var.smtp_host == "" ? [] : [var.smtp_host]

        content {
          name  = "DEVCONTROL_SMTP_HOST"
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.smtp_username == "" ? [] : [var.smtp_username]

        content {
          name  = "DEVCONTROL_SMTP_USERNAME"
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.smtp_password == "" ? [] : [1]

        content {
          name = "DEVCONTROL_SMTP_PASSWORD"

          value_source {
            secret_key_ref {
              secret  = google_secret_manager_secret.smtp_password[0].secret_id
              version = "latest"
            }
          }
        }
      }

      dynamic "env" {
        for_each = var.operator_bootstrap_secret == "" ? [] : [1]

        content {
          name = "DEVCONTROL_OPERATOR_BOOTSTRAP_SECRET"

          value_source {
            secret_key_ref {
              secret  = google_secret_manager_secret.operator_bootstrap_secret[0].secret_id
              version = "latest"
            }
          }
        }
      }

      dynamic "env" {
        for_each = var.github_app_id == "" ? [] : [var.github_app_id]

        content {
          name  = "DEVCONTROL_GITHUB_APP_ID"
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.github_app_private_key == "" ? [] : [1]

        content {
          name = "DEVCONTROL_GITHUB_APP_PRIVATE_KEY"

          value_source {
            secret_key_ref {
              secret  = google_secret_manager_secret.github_app_private_key[0].secret_id
              version = "latest"
            }
          }
        }
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
  }

  depends_on = [
    google_compute_instance.postgres,
    google_secret_manager_secret_iam_member.runtime_can_read_postgres_password,
    google_secret_manager_secret_iam_member.runtime_can_read_scheduler_secret,
    google_secret_manager_secret_iam_member.runtime_can_read_metrics_scrape_token,
    google_secret_manager_secret_iam_member.runtime_can_read_google_oauth_client_secret,
    google_secret_manager_secret_iam_member.runtime_can_read_smtp_password,
    google_secret_manager_secret_iam_member.runtime_can_read_operator_bootstrap_secret,
    google_secret_manager_secret_iam_member.runtime_can_read_github_app_private_key
  ]

  lifecycle {
    ignore_changes = [
      client,
      client_version,
      labels["commit-sha"],
      labels["managed-by"],
      scaling,
      template[0].labels,
      template[0].containers[0].image
    ]
  }
}

resource "google_cloud_run_v2_service_iam_member" "public_invoker" {
  project  = var.project_id
  location = google_cloud_run_v2_service.devcontrol.location
  name     = google_cloud_run_v2_service.devcontrol.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}
