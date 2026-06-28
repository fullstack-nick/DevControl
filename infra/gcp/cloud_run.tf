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
    google_secret_manager_secret_iam_member.runtime_can_read_postgres_password
  ]
}

resource "google_cloud_run_v2_service_iam_member" "public_invoker" {
  project  = var.project_id
  location = google_cloud_run_v2_service.devcontrol.location
  name     = google_cloud_run_v2_service.devcontrol.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}

