resource "google_cloud_scheduler_job" "devcontrol_tick" {
  name             = "${local.app_name}-scheduler-tick"
  description      = "Bounded DevControl scheduler tick for webhook retry batches."
  region           = var.region
  schedule         = "*/5 * * * *"
  time_zone        = "Etc/UTC"
  attempt_deadline = "60s"

  retry_config {
    retry_count = 1
  }

  http_target {
    http_method = "POST"
    uri         = "${google_cloud_run_v2_service.devcontrol.uri}/internal/scheduler/tick"

    headers = {
      "X-DevControl-Scheduler-Secret" = random_password.scheduler.result
    }
  }

  depends_on = [
    google_project_service.required,
    google_cloud_run_v2_service.devcontrol
  ]
}
