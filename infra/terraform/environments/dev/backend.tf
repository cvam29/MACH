# Remote state backend. For the demo this is documentation only — reviewers run
# `terraform init -backend=false` to init offline without touching Azure.
#
# To use the remote backend, fill in a real storage account/container and run
# `terraform init` (omit -backend=false). To init/plan fully offline, leave the azurerm
# backend as-is and pass -backend=false, OR uncomment the local backend below.

terraform {
  backend "azurerm" {
    resource_group_name  = "rg-mach-tfstate"
    storage_account_name = "stmachtfstate"
    container_name       = "tfstate"
    key                  = "dev.terraform.tfstate"
  }

  # --- Offline reviewer alternative (commented) ---
  # Comment out the azurerm backend above and uncomment this to keep state on disk:
  # backend "local" {
  #   path = "dev.local.tfstate"
  # }
}
