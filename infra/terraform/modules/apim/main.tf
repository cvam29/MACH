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

# A representative operation so the sample API is non-empty.
resource "azurerm_api_management_api_operation" "get_categories" {
  operation_id        = "get-categories"
  api_name            = azurerm_api_management_api.bff.name
  api_management_name = azurerm_api_management.this.name
  resource_group_name = var.resource_group_name
  display_name        = "List catalog categories"
  method              = "GET"
  url_template        = "/catalog/categories"
  description         = "Returns the commercetools category tree."
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
