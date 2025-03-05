provider "azurerm" {
  features {}
}

variable "sessionid" {
  description = "Session ID"
  type        = string
}

data "azurerm_resource_group" "rg" {
  name = "RG-pt-azure-workshop"
}

resource "azurerm_storage_account" "sa" {
  name                     = substr("ssa${var.sessionid}", 0, 23)
  resource_group_name      = data.azurerm_resource_group.rg.name
  location                 = data.azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_windows_web_app" "web_app" {
  name                = "session-wa-${var.sessionid}"
  location            = data.azurerm_resource_group.rg.location
  resource_group_name = data.azurerm_resource_group.rg.name
  service_plan_id     = "/subscriptions/48ee300d-8738-496a-9366-1271ebefc1e6/resourceGroups/rg-pt-azure-workshop/providers/Microsoft.Web/serverFarms/ASP-RGptazureworkshop-bfe7"

  site_config {
    application_stack {
      dotnet_version = "8.0"
    }
  }

  app_settings = {
    "WEBSITE_RUN_FROM_PACKAGE" = "1"
  }
}
