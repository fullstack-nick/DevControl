output "region" {
  value = var.region
}

output "zone" {
  value = var.zone
}

output "postgres_machine_type" {
  value = google_compute_instance.postgres.machine_type
}

output "postgres_private_ip" {
  value = google_compute_instance.postgres.network_interface[0].network_ip
}

output "postgres_boot_disk_gb" {
  value = google_compute_instance.postgres.boot_disk[0].initialize_params[0].size
}

output "postgres_data_disk_gb" {
  value = google_compute_disk.postgres_data.size
}

output "artifact_registry_repository" {
  value = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.docker.repository_id}"
}

output "postgres_backup_bucket_url" {
  value       = "gs://${google_storage_bucket.postgres_backups.name}"
  description = "Private short-retention PostgreSQL backup bucket."
}

output "cloud_run_service_url" {
  value = google_cloud_run_v2_service.devcontrol.uri
}

output "live_observability_grafana_url" {
  value       = "${google_cloud_run_v2_service.devcontrol.uri}/observability/"
  description = "DevControl-authenticated on-demand live Grafana URL."
}

output "metrics_scrape_secret_id" {
  value       = google_secret_manager_secret.metrics_scrape_token.secret_id
  description = "Secret Manager secret ID containing the live /metrics scrape token."
}

output "grafana_admin_secret_id" {
  value       = google_secret_manager_secret.grafana_admin_password.secret_id
  description = "Secret Manager secret ID containing the live Grafana admin password."
}

output "github_workload_identity_provider" {
  value = google_iam_workload_identity_pool_provider.github.name
}

output "github_deployer_service_account" {
  value = google_service_account.github_deployer.email
}

output "github_allowed_repositories" {
  value = sort(tolist(local.github_allowed_repositories))
}
