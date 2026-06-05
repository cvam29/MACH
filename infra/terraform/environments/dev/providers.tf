# Provider versions are inherited from ../../versions.tf via the root module's
# required_providers; this file holds the provider *configuration* for the dev environment.

terraform {
  required_version = ">= 1.9"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "azurerm" {
  # resource_provider_registrations = "none" avoids needing subscription perms during
  # plan-only/documentation use; subscription_id is supplied via ARM_SUBSCRIPTION_ID.
  features {}

  subscription_id = var.subscription_id
}

provider "azuread" {}

provider "random" {}
