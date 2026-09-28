Update 1.0.7

- Restricted AssetBundle contents to the assets actually loaded by this vehicle and their dependencies, excluding redundant source models and working assets.
- Reduced repeated paint/material work while preserving per-vehicle colors and native rendering overrides.
- Suspended mod visual updates for off-screen AI vehicles, refreshed visuals on visibility return, and preserved the configured native traffic budget.
- Checked vehicle registration success and retained ownership of the mod’s registered entries during cleanup.
