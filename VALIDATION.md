# Validation — 1.0.0

This release retains the current development implementation. The latest Unity verification passes 298 configuration and regression checks covering both dealerships, paint, steering, drivetrain, light signals/materials, presentation offsets and prefab cloning. The actual packaged AssetBundle also reloads successfully.

Controlled HDRP renders of the same lamp implementation verified visible tail, brake, reverse and left/right indicator emission. Exposure-independent emission and double-sided lamp faces address the reproduced rear-light rendering defect.

These checks and controlled renders do not establish real-game driving, collision behaviour, acceleration or live input/rendering results. Power, torque and speed figures are configuration targets. Engine audio uses a native donor car.

Local revision: C-shaped rear light guides, separate dark housings and rear signal inserts. All four tyres align to the zero ground plane within 0.1 mm, with matching configured physical wheel bounds. Verified in controlled Unity HDRP captures; live-game behaviour remains to be confirmed.

Road presentation revision: user-session telemetry confirms native tyre contact at Y=0.050023 on RoadGroundPlane (3). Read-only analysis of the installed Hamptons scene places the visible asphalt under all four wheels at Y approximately 0.000008. Presentation compensates 5 cm only when all four wheels contact a horizontal RoadGroundPlane at Y=0.05; native suspension, wheel carriers and colliders stay unchanged. Other surfaces retain the original presentation. A native Unity suspension simulation reproducing those two heights places all four rendered tyre bottoms at Y=0 while physical bottoms remain at Y=0.05. Rotation, repeated offset application, offset reset and prefab cloning checks pass. Visual confirmation of this correction in the running game remains pending.
