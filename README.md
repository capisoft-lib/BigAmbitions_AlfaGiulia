# Alfa Romeo Giulia Quadrifoglio (2016) — 1.0.7
Drive the Italian sports saloon in Big Ambitions for Windows.

# Where to buy
Purchase or order at General US Trucks in Industry City or The Hamptons Axis. Price: $79,900. Cargo capacity: 4.

# Features

- 2.9-litre twin-turbo V6: 375 kW / 510 PS and 600 Nm target torque curve.
- Rear-wheel drive, eight-speed automatic gearbox and 307 km/h limiter.
- 1620 kg, 58 L fuel tank and selectable body colour.
- Fuel consumption multiplier 20 and idle consumption floor 4.5%.
- Native steering, suspension, braking, fuel and damage.
- Headlights, tail lights, brake lights, reverse lights and left/right indicators using the native blink phase.

Version 1.0.1 retains all current development fixes for paint, steering, engine response and rear-light rendering. Performance figures are configuration targets, not measured in-game results. Engine sound comes from the native donor vehicle. Price and cargo capacity are gameplay choices.

# Installation
Subscribe, let Steam finish downloading and enable the mod in the game. If AlfaGiulia is already installed manually, keep a single installation to avoid duplicates. No additional Workshop item is required; Harmony is bundled.

# Credits
Mod adaptation: capisoft-lib.
3D model: Ddiaz Design, [2016 Alfa Romeo Giulia Quadrifoglio](https://sketchfab.com/3d-models/2016-alfa-romeo-giulia-quadrifoglio-8985b52ac8a84aaf90ccaa5669697001).
Model and adaptations: [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/). Adaptations include scale, separate wheels and calipers, light channels and HDRP materials. Noncommercial sharing under the same licence.

For manual installation, copy this package to ModsLocal with the game closed. See SOURCE_ASSET.md and VALIDATION.md.

### Private drivers and ambient traffic
The car is available to all three native private-driver contracts. Contract prices,
capacity limits, ownership checks and the normal drop-off/summon flow are retained.
A dedicated AI template uses the car's own body, wheels, paint and lights on the
native Honza Mimic traffic chassis. One ambient traffic pool entry is registered
at city initialization. Random parked spawns are not enabled. Reload the city after
installing this update. Each mod removes only its own registrations on unload.

### Traffic count fix (1.0.3)
Synchronizes the traffic vehicle count after mod registrations, preserving native entries and avoiding repeated increments.

### Body repair (1.0.4)
Each player Giulia owns its deformable body mesh. Repair restores the original vertices,
normals, tangents and bounds, clears saved dents and cancels queued collisions that could
otherwise deform the body again after repair. Other vehicle mods and native cars keep
their existing repair behavior. Native collision strength and saved-damage loading are retained.

<!-- optimization-release-1.0.7 -->
## 1.0.7

- Restricted AssetBundle contents to the assets actually loaded by this vehicle and their dependencies, excluding redundant source models and working assets.
- Reduced repeated paint/material work while preserving per-vehicle colors and native rendering overrides.
- Suspended mod visual updates for off-screen AI vehicles, refreshed visuals on visibility return, and preserved the configured native traffic budget.
- Checked vehicle registration success and retained ownership of the mod’s registered entries during cleanup.

Release descriptions and changelogs: `releases/1.0.7/`.

Release reconstruction: [`tools~/optimization/build.ps1`](tools~/optimization/README.md).
