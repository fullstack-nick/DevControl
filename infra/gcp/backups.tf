resource "google_storage_bucket" "postgres_backups" {
  name                        = "${var.project_id}-${local.app_name}-postgres-backups"
  location                    = var.region
  storage_class               = "STANDARD"
  uniform_bucket_level_access = true
  public_access_prevention    = "enforced"
  labels                      = local.labels

  versioning {
    enabled = false
  }

  lifecycle_rule {
    action {
      type = "Delete"
    }

    condition {
      age = 7
    }
  }

  depends_on = [google_project_service.required]
}
