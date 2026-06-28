data "google_compute_image" "ubuntu" {
  family  = "ubuntu-2404-lts-amd64"
  project = "ubuntu-os-cloud"
}

resource "google_compute_disk" "postgres_data" {
  name = "${local.app_name}-postgres-data"
  type = "pd-standard"
  zone = var.zone
  size = 20

  labels = local.labels
}

resource "google_compute_instance" "postgres" {
  name         = "${local.app_name}-postgres"
  machine_type = "e2-micro"
  zone         = var.zone
  tags         = ["${local.app_name}-postgres"]
  labels       = local.labels

  boot_disk {
    auto_delete = true

    initialize_params {
      image = data.google_compute_image.ubuntu.self_link
      size  = 10
      type  = "pd-standard"
    }
  }

  attached_disk {
    source      = google_compute_disk.postgres_data.id
    device_name = "postgres-data"
    mode        = "READ_WRITE"
  }

  network_interface {
    subnetwork = google_compute_subnetwork.main.id
    network_ip = var.postgres_private_ip

    access_config {
    }
  }

  metadata_startup_script = templatefile("${path.module}/templates/postgres-startup.sh.tftpl", {
    project_id               = var.project_id
    postgres_database        = var.postgres_database
    postgres_username        = var.postgres_username
    postgres_private_ip      = var.postgres_private_ip
    postgres_password_secret = google_secret_manager_secret.postgres_password.secret_id
    subnet_cidr              = var.subnet_cidr
  })

  service_account {
    email  = google_service_account.postgres_vm.email
    scopes = ["https://www.googleapis.com/auth/cloud-platform"]
  }

  depends_on = [
    google_secret_manager_secret_version.postgres_password,
    google_secret_manager_secret_iam_member.vm_can_read_postgres_password
  ]
}

