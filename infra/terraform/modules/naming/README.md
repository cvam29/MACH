# naming

Pure-computation module. Generates consistent Azure resource names and a base tag map
from a `prefix` / `env` / `location` triple. Creates **no** Azure resources.

## Purpose
- Single source of truth for naming conventions (`<kind>-<prefix>-<env>-<locShort>`).
- Single source of truth for the base tag set applied to every resource.

## Inputs
| Name | Type | Default | Description |
|---|---|---|---|
| `prefix` | string | — | Workload prefix, 2-10 lowercase alphanumerics. |
| `env` | string | — | One of `dev`/`test`/`stage`/`prod`. |
| `location` | string | — | Long-form region (e.g. `westeurope`). |
| `location_short` | string | `weu` | Short region code used in names. |
| `extra_tags` | map(string) | `{}` | Tags merged over the base set. |

## Outputs
| Name | Description |
|---|---|
| `names` | Map of dashed resource names keyed by kind. |
| `names_nodash` | Map of dash-free names (storage base). |
| `tags` | Merged tag map — attach to every resource. |
| `location` | Pass-through region. |
