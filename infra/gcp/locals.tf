locals {
  app_name       = "devcontrol"
  operator_label = "nickaccturk-gmail-com"
  github_repo    = "${var.github_owner}/${var.github_repo}"

  labels = {
    app      = local.app_name
    stage    = "stage-2"
    operator = local.operator_label
  }

  required_services = toset([
    "artifactregistry.googleapis.com",
    "cloudbuild.googleapis.com",
    "compute.googleapis.com",
    "iam.googleapis.com",
    "iamcredentials.googleapis.com",
    "run.googleapis.com",
    "secretmanager.googleapis.com",
    "serviceusage.googleapis.com",
    "sts.googleapis.com"
  ])
}
