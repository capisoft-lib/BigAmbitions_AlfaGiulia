# Validation — 1.0.4

## Body repair — 2026-09-17

Unity 2022.3.62f2, using the installed game's current BigAmbitions.dll and the real
Giulia body mesh: 34 targeted checks pass. The native controller reproduces a queued
collision reapplying a dent after Reset. The earlier hypothesis that two ordinary
repair cycles corrupt the original mesh was NOT reproduced in this fixture.

The Giulia-only Start/Reset patches pass three deformation/repair cycles, independent
template clones, exact vertex/normal/tangent/bounds restoration, pending-queue and
saved-damage clearing, native damage-payload serialization and replay, unaffected
unmarked cars, patch unload/reload, and owned-mesh cleanup. The broader configuration
suite passes 305 checks. Existing bundle reload passes CRC 2691216581; geometry is unchanged.

Tests call native Start, Reset, DeformMesh and LateUpdate in an isolated Editor fixture.
They do not reproduce a repair-shop visit or a complete save reload in the actual game,
and do not establish that queued collisions explain the reported screenshot.
No ModsLocal deployment or Steam publication is part of this revision's validation.

## Earlier validation

This release retains the current development implementation. The latest Unity verification passes 298 configuration and regression checks covering both dealerships, paint, steering, drivetrain, light signals/materials, presentation offsets and prefab cloning. The actual packaged AssetBundle also reloads successfully.

Controlled HDRP renders of the same lamp implementation verified visible tail, brake, reverse and left/right indicator emission. Exposure-independent emission and double-sided lamp faces address the reproduced rear-light rendering defect.

These checks and controlled renders do not establish real-game driving, collision behaviour, acceleration or live input/rendering results. Power, torque and speed figures are configuration targets. Engine audio uses a native donor car.

Local revision: C-shaped rear light guides, separate dark housings and rear signal inserts. All four tyres align to the zero ground plane within 0.1 mm, with matching configured physical wheel bounds. Verified in controlled Unity HDRP captures; live-game behaviour remains to be confirmed.

Road presentation revision: user-session telemetry confirms native tyre contact at Y=0.050023 on RoadGroundPlane (3). Read-only analysis of the installed Hamptons scene places the visible asphalt under all four wheels at Y approximately 0.000008. Presentation compensates 5 cm only when all four wheels contact a horizontal RoadGroundPlane at Y=0.05; native suspension, wheel carriers and colliders stay unchanged. Other surfaces retain the original presentation. A native Unity suspension simulation reproducing those two heights places all four rendered tyre bottoms at Y=0 while physical bottoms remain at Y=0.05. Rotation, repeated offset application, offset reset and prefab cloning checks pass. Visual confirmation of this correction in the running game remains pending.

## Private drivers and ambient traffic — 2026-09-14
304 regression checks PASS; official package build and actual bundle reload PASS. Native installed-game AI donor initialization, wheel/visibility/collider wiring, head/brake/reverse on/off, patched AI lookup, three native contracts, traffic insertion/idempotence/unload PASS. Combined three-mod tests pass in both load orders, including middle-mod unload. Logs: .analysis/private-drivers. No saved-game journey or ambient sighting verified. Installed in ModsLocal after explicit user selection; matching Steam copy disabled in the local activation manifest to prevent duplicate loading. Steam publication unchanged.

## Release 1.0.1 — 2026-09-14
User confirmed the local correction works before authorizing publication. This release increments the semantic/package version to 1.0.1 and assembly/file versions to 1.0.1.0, and updates EN/FR descriptions for private drivers and ambient traffic. Runtime behavior is unchanged from the validated local correction. Workshop native metadata modVersion remains 0, distinct from the semantic version. Publication evidence is recorded outside the package.
