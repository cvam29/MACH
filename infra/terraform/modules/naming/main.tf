# Pure-computation module: derives consistent resource names + a base tag map.
# No provider resources are created here, so the module needs no provider block.

locals {
  # base = "<prefix>-<env>-<location_short>" e.g. "mach-dev-weu"
  base    = "${var.prefix}-${var.env}-${var.location_short}"
  base_nd = "${var.prefix}${var.env}${var.location_short}" # no-dash, for storage/maps etc.

  base_tags = {
    workload    = var.prefix
    environment = var.env
    managedBy   = "terraform"
    costCenter  = "mach-demo"
    iac         = "true"
  }
}
