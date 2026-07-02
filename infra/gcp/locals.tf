locals {
  app_name       = "devcontrol"
  operator_label = "nickaccturk-gmail-com"
  github_repo    = "${var.github_owner}/${var.github_repo}"
  github_allowed_repositories = toset(length(var.github_allowed_repositories) > 0
    ? var.github_allowed_repositories
    : [
      local.github_repo,
      "${var.github_owner}/devcontrol-sample-live-app"
  ])
  github_allowed_repository_condition = join(" || ", [
    for repository in local.github_allowed_repositories : "assertion.repository == '${repository}'"
  ])

  labels = {
    app      = local.app_name
    stage    = "stage-2"
    operator = local.operator_label
  }

  required_services = toset([
    "artifactregistry.googleapis.com",
    "cloudbuild.googleapis.com",
    "cloudscheduler.googleapis.com",
    "compute.googleapis.com",
    "iam.googleapis.com",
    "iamcredentials.googleapis.com",
    "run.googleapis.com",
    "secretmanager.googleapis.com",
    "serviceusage.googleapis.com",
    "storage.googleapis.com",
    "sts.googleapis.com"
  ])
}
