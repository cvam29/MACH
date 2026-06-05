# API Management gateway fronting the BFF. Ships a sample API + an API-level policy
# (rate-limit, CORS, and presence validation of the session cookie/header) and Key Vault
# -backed named values. A system-assigned identity lets APIM resolve KV secrets passwordlessly.

resource "azurerm_api_management" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  publisher_name      = var.publisher_name
  publisher_email     = var.publisher_email
  sku_name            = var.sku_name

  identity {
    type = "SystemAssigned"
  }

  tags = var.tags
}

# Let APIM read Key Vault named-value secrets via its managed identity.
resource "azurerm_role_assignment" "apim_kv_secrets_user" {
  count                = var.key_vault_id == null ? 0 : 1
  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_api_management.this.identity[0].principal_id
}

# Named values sourced from Key Vault (no plaintext in APIM config/state).
resource "azurerm_api_management_named_value" "kv" {
  for_each            = var.key_vault_named_values
  name                = each.key
  resource_group_name = var.resource_group_name
  api_management_name = azurerm_api_management.this.name
  display_name        = each.key
  secret              = true

  value_from_key_vault {
    secret_id = each.value
  }

  depends_on = [azurerm_role_assignment.apim_kv_secrets_user]
}

# Sample API: the storefront BFF surface behind /api.
resource "azurerm_api_management_api" "bff" {
  name                  = "mach-bff"
  resource_group_name   = var.resource_group_name
  api_management_name   = azurerm_api_management.this.name
  revision              = "1"
  display_name          = "MACH BFF API"
  path                  = "api"
  protocols             = ["https"]
  service_url           = var.bff_backend_url
  subscription_required = false
}

# The full BFF surface, modeled declaratively. `template_parameter` blocks are emitted per
# {param} in the route. This documents the whole contract APIM fronts (terraform plan = the
# deliverable); the storefront calls these behind /api.
locals {
  bff_operations = {
    "get-categories"         = { method = "GET", url = "/catalog/categories", display = "List catalog categories", params = [] }
    "search"                 = { method = "GET", url = "/search", display = "Search products (Algolia)", params = [] }
    "search-suggest"         = { method = "GET", url = "/search/suggest", display = "Search autocomplete", params = [] }
    "get-product"            = { method = "GET", url = "/products/{slug}", display = "Get product (commerce + content)", params = ["slug"] }
    "create-cart"            = { method = "POST", url = "/carts", display = "Create cart", params = [] }
    "get-cart"               = { method = "GET", url = "/carts/{id}", display = "Get cart", params = ["id"] }
    "add-line-item"          = { method = "POST", url = "/carts/{id}/line-items", display = "Add line item", params = ["id"] }
    "update-line-item"       = { method = "PATCH", url = "/carts/{id}/line-items/{lineItemId}", display = "Update line-item quantity", params = ["id", "lineItemId"] }
    "remove-line-item"       = { method = "DELETE", url = "/carts/{id}/line-items/{lineItemId}", display = "Remove line item", params = ["id", "lineItemId"] }
    "set-shipping-address"   = { method = "POST", url = "/carts/{id}/shipping", display = "Set shipping address", params = ["id"] }
    "set-billing-address"    = { method = "POST", url = "/carts/{id}/billing", display = "Set billing address", params = ["id"] }
    "get-delivery-options"   = { method = "POST", url = "/carts/{id}/delivery-options", display = "Quote delivery options (Azure Maps distance)", params = ["id"] }
    "select-delivery"        = { method = "PUT", url = "/carts/{id}/delivery", display = "Select delivery type", params = ["id"] }
    "get-stores"             = { method = "GET", url = "/stores", display = "Nearby pickup stores", params = [] }
    "create-payment-session" = { method = "POST", url = "/checkout/{cartId}/payment-session", display = "Create Adyen payment session", params = ["cartId"] }
    "place-order"            = { method = "POST", url = "/checkout/{cartId}/order", display = "Place order", params = ["cartId"] }
    "get-my-orders"          = { method = "GET", url = "/orders/me", display = "My orders (SQL projection)", params = [] }
    "get-order"              = { method = "GET", url = "/orders/{id}", display = "Get order", params = ["id"] }
    "get-content"            = { method = "GET", url = "/content/{type}/{slug}", display = "Get CMS content", params = ["type", "slug"] }
    "get-navigation"         = { method = "GET", url = "/content/navigation", display = "Get navigation", params = [] }
    "health"                 = { method = "GET", url = "/health", display = "Health probe", params = [] }
  }
}

resource "azurerm_api_management_api_operation" "bff" {
  for_each            = local.bff_operations
  operation_id        = each.key
  api_name            = azurerm_api_management_api.bff.name
  api_management_name = azurerm_api_management.this.name
  resource_group_name = var.resource_group_name
  display_name        = each.value.display
  method              = each.value.method
  url_template        = each.value.url
  description         = each.value.display

  dynamic "template_parameter" {
    for_each = each.value.params
    content {
      name     = template_parameter.value
      type     = "string"
      required = true
    }
  }
}

# Operation-level policy example: placing an order is the one mutation we want idempotent and
# more tightly throttled than the API-wide limit. Demonstrates per-operation policy layering.
resource "azurerm_api_management_api_operation_policy" "place_order" {
  api_name            = azurerm_api_management_api.bff.name
  api_management_name = azurerm_api_management.this.name
  resource_group_name = var.resource_group_name
  operation_id        = azurerm_api_management_api_operation.bff["place-order"].operation_id

  xml_content = <<XML
<policies>
  <inbound>
    <base />
    <rate-limit-by-key calls="10" renewal-period="60"
      counter-key="@(context.Request.IpAddress)" />
    <check-header name="Idempotency-Key" failed-check-httpcode="400"
      failed-check-error-message="Idempotency-Key header is required for order placement."
      ignore-case="true" />
  </inbound>
  <backend><base /></backend>
  <outbound><base /></outbound>
  <on-error><base /></on-error>
</policies>
XML
}

# API-level inbound policy: rate-limit + CORS + session presence check.
# Cookie/header presence is validated with a choose/when on the auth cookie and header;
# state-changing customer routes require the session before reaching the backend.
resource "azurerm_api_management_api_policy" "bff" {
  api_name            = azurerm_api_management_api.bff.name
  api_management_name = azurerm_api_management.this.name
  resource_group_name = var.resource_group_name

  xml_content = <<XML
<policies>
  <inbound>
    <base />
    <rate-limit calls="${var.rate_limit_calls}" renewal-period="${var.rate_limit_period_seconds}" />
    <cors allow-credentials="true">
      <allowed-origins>
        <origin>https://localhost:3000</origin>
      </allowed-origins>
      <allowed-methods>
        <method>GET</method>
        <method>POST</method>
        <method>PATCH</method>
        <method>PUT</method>
        <method>DELETE</method>
        <method>OPTIONS</method>
      </allowed-methods>
      <allowed-headers>
        <header>*</header>
      </allowed-headers>
    </cors>
    <!-- Presence check: customer-scoped writes must carry the session (httpOnly cookie
         set by the Auth service) OR an Authorization header. Reads stay anonymous. -->
    <choose>
      <when condition="@(context.Request.Method != &quot;GET&quot; &amp;&amp; context.Request.Method != &quot;OPTIONS&quot; &amp;&amp; !context.Request.Headers.GetValueOrDefault(&quot;Cookie&quot;,&quot;&quot;).Contains(&quot;mach_session&quot;) &amp;&amp; !context.Request.Headers.ContainsKey(&quot;Authorization&quot;))">
        <return-response>
          <set-status code="401" reason="Missing session" />
          <set-body>{ "error": "missing_session" }</set-body>
        </return-response>
      </when>
    </choose>
  </inbound>
  <backend>
    <base />
  </backend>
  <outbound>
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
XML
}
