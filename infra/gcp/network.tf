resource "google_compute_network" "main" {
  name                    = "${local.app_name}-vpc"
  auto_create_subnetworks = false

  depends_on = [google_project_service.required]
}

resource "google_compute_subnetwork" "main" {
  name          = "${local.app_name}-subnet"
  ip_cidr_range = var.subnet_cidr
  region        = var.region
  network       = google_compute_network.main.id
}

resource "google_compute_firewall" "postgres_from_subnet" {
  name    = "${local.app_name}-postgres-from-subnet"
  network = google_compute_network.main.name

  allow {
    protocol = "tcp"
    ports    = ["5432"]
  }

  source_ranges = [var.subnet_cidr]
  target_tags   = ["${local.app_name}-postgres"]
}

