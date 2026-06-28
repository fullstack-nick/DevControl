output "operator_google_account" {
  value       = var.operator_google_account
  description = "The required human Google account for local GCP changes."
}

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

output "cloud_run_service_url" {
  value = google_cloud_run_v2_service.devcontrol.uri
}

output "github_workload_identity_provider" {
  value = google_iam_workload_identity_pool_provider.github.name
}

output "github_deployer_service_account" {
  value = google_service_account.github_deployer.email
}

