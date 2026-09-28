using System;
using System.Linq;
using HarmonyLib;
using NWH.VehiclePhysics2.Modules.Fuel;
using NWH.VehiclePhysics2.Modules.SpeedLimiter;
using NWH.WheelController3D;
using UnityEngine;
using UnityEngine.AI;
using Vehicles.VehicleTypes;
using Object = UnityEngine.Object;

namespace AlfaGiulia
{
    public static class AlfaGiuliaVehicle
    {
        public const float Mass = 1620f;
        public const float MaximumKph = 307f;
        public const float MaximumMetersPerSecond = MaximumKph / 3.6f;

        // An inactive parent is essential: native Awake must see all references already rewired.
        public static GameObject Create(GameObject donor, GameObject visualPrefab, VehicleType type, Transform inactiveParent)
        {
            if (inactiveParent == null || inactiveParent.gameObject.activeInHierarchy)
                throw new ArgumentException("An inactive template parent is required.");
            GameObject root = null;
            try
            {
                root = Object.Instantiate(donor, inactiveParent, false); root.name = "AlfaGiulia";
                root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one; root.SetActive(true);
                var car = root.GetComponent<CarController>();
                if (car == null || car.vehicleController == null) throw new InvalidOperationException("Native driving controller missing.");
                car.vehicleType = type; car.vehicleInstance = new VehicleInstance(type.vehicleTypeName);
                var oldRenderers = root.GetComponentsInChildren<Renderer>(true);
                var visual = Object.Instantiate(visualPrefab, root.transform, false); visual.name = "AlfaGiuliaVisual";
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
                
                var renderers = visual.GetComponentsInChildren<MeshRenderer>(true)
                    .ToArray();
                if (renderers.Length < 5) throw new InvalidOperationException("AlfaGiulia visual is incomplete.");
                var rigidbody = root.GetComponent<Rigidbody>(); rigidbody.mass = Mass;
                rigidbody.centerOfMass = new Vector3(0, .32f, 0);
                rigidbody.angularDrag=.35f;
                rigidbody.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                rigidbody.maxDepenetrationVelocity=3f;
                rigidbody.solverIterations=12;rigidbody.solverVelocityIterations=4;
                var mass = root.GetComponent<NWH.Common.CoM.VariableCenterOfMass>();
                if(mass!=null){mass.useDefaultMass=false;mass.useMassAffectors=false;mass.baseMass=Mass;
                    mass.useDefaultCenterOfMass=false;mass.centerOfMass=rigidbody.centerOfMass;
                    mass.dimensions=new Vector3(1.873f,1.426f,4.639f);
                    mass.useDefaultInertia=false;mass.inertiaTensor=new Vector3(2400,2600,850);
                    rigidbody.inertiaTensor=mass.inertiaTensor;}
                ConfigureWheels(root, visual.transform);
                ConfigureBody(car, renderers);
                var limiter = car.GetComponent<SpeedLimiterModuleWrapper>()?.module;
                if (limiter == null) throw new InvalidOperationException("Native speed limiter missing.");
                limiter.speedUnits = SpeedLimiterModule.SpeedUnits.kmh;
                limiter.speedLimit = MaximumKph;
                limiter.active = true;

                ConfigurePowertrain(car);
                var fuelModule = car.GetComponent<FuelModuleWrapper>()?.GetModule() as FuelModule;
                if (fuelModule == null) throw new InvalidOperationException("Native fuel module missing.");
                fuelModule.consumptionMultiplier = 20f;
                fuelModule.idleConsumption = .045f;
                foreach (var renderer in oldRenderers)
                {
                    if (renderer is SkinnedMeshRenderer || renderer.GetComponentInParent<AppearanceSetter>(true) != null) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) { filter.sharedMesh = null; renderer.enabled = false; }
                    // Preserve exhaust/smoke particles and physical headlight sources.
                }
                foreach (var lod in root.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
                var driver = AccessTools.Field(typeof(CarController), "driverAppearanceSetter").GetValue(car) as AppearanceSetter;
                if (driver != null) driver.transform.localPosition = new Vector3(-.36f, .43f, .08f);
                AlfaGiuliaMaterials.Repair(visual);
                visual.AddComponent<AlfaGiuliaPaint>();
                root.AddComponent<AlfaGiuliaRoadPresentation>().Configure(car,visual.transform,driver==null?null:driver.transform);
                root.AddComponent<AlfaGiuliaGroundDiagnostics>().Car = car;
                return root;
            }
            catch { if (root != null) Object.DestroyImmediate(root); throw; }
        }

        private static void ConfigurePowertrain(CarController car)
        {
            var vc=car.vehicleController; var engine=vc.powertrain.engine;
            engine.maxPower=375f; engine.idleRPM=800; engine.revLimiterRPM=7400;
            engine.inertia=.32f;
            // 600 Nm plateau 2500-5500 RPM; peak 375 kW at 6500 RPM.
            // NWH uses normalized RPM and normalized power, not torque values.
            engine.powerCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(800f/7400,.045f),
                new Keyframe(2500f/7400,.419f),new Keyframe(5500f/7400,.922f),
                new Keyframe(6500f/7400,1),new Keyframe(1,.88f));
            var keys=engine.powerCurve.keys;
            for(int i=0;i<keys.Length;i++){
                if(i>0)keys[i].inTangent=(keys[i].value-keys[i-1].value)/(keys[i].time-keys[i-1].time);
                if(i+1<keys.Length)keys[i].outTangent=(keys[i+1].value-keys[i].value)/(keys[i+1].time-keys[i].time);
            }
            engine.powerCurve.keys=keys;
            engine.forcedInduction.useForcedInduction=false; // Rated power already includes both turbos.
            var transmission=vc.powertrain.transmission;
            var clutch=vc.powertrain.clutch;
            engine.Output=clutch;clutch.Output=transmission;
            clutch.slipTorque=950;clutch.engagementRPM=1100;
            clutch.throttleEngagementOffsetRPM=150;clutch.engagementRange=400;
            clutch.inertia=.025f;
            transmission.gears=new System.Collections.Generic.List<float>{-3.295f,0,4.714f,3.143f,2.106f,1.667f,1.285f,1,.839f,.667f};
            transmission.finalGearRatio=3.09f; transmission.shiftDuration=.15f;
            transmission.DownshiftRPM=1700; transmission.UpshiftRPM=4600;
            transmission.variableShiftPoint=true;transmission.variableShiftIntensity=.38f;
            transmission.postShiftBan=.35f;transmission.allowDownshiftGearSkipping=false;
            var rear=vc.powertrain.differentials.Single(d=>d.name.IndexOf("Rear",StringComparison.OrdinalIgnoreCase)>=0);
            transmission.Output=rear;
            rear.DifferentialType=NWH.VehiclePhysics2.Powertrain.DifferentialComponent.Type.LimitedSlip;
            rear.stiffness=.2f;
            foreach(var axle in vc.powertrain.wheelGroups){
                bool front=axle.name.IndexOf("Front",StringComparison.OrdinalIgnoreCase)>=0;
                axle.antiRollBarForce=front?6500:5000;axle.brakeCoefficient=front?1f:.65f;
            }
            vc.brakes.maxTorque=car.vehicleType.brakeForce; vc.steering.maximumSteerAngle=42;
            vc.steering.degreesPerSecondLimit=95;
            // Actual NWH implementation normalizes speed by 50 m/s, despite its tooltip.
            // Keep usable steering at road speeds, like the native curve retained
            // by the inspected Audi RS6-R / BMW M4 / Porsche GT3 RS prefabs.
            // Tire grip limits lateral acceleration; a no-slip bicycle estimate
            // must not be used to reduce available steering almost to zero.
            vc.steering.speedSensitiveSteeringCurve=new AnimationCurve(
                new Keyframe(0,1,0,0),new Keyframe(.3f,.4f,-.6f,-.6f),new Keyframe(1,.2f,-.1f,.1f));
            vc.steering.speedSensitiveSmoothingCurve=new AnimationCurve(
                new Keyframe(0,.10f),new Keyframe(.3f,.17f),new Keyframe(1,.22f));
            vc.enginePosition=new Vector3(0,.65f,1.3f);vc.exhaustPosition=new Vector3(0,.3f,-2.2f);
        }

        public static Bounds BoundsOf(Renderer[] renderers, Transform space)
        {
            var bounds = new Bounds(); bool first = true;
            foreach (var renderer in renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var local = mesh.bounds;
                var matrix = space.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
            if (first) throw new InvalidOperationException("No visual geometry.");
            return bounds;
        }

        private static void ConfigureWheels(GameObject root, Transform visual)
        {
            var wheels = root.GetComponentsInChildren<WheelController>(true);
            if (wheels.Length != 4) throw new InvalidOperationException("Expected four native suspension units.");
            foreach (var wheel in wheels)
            {
                var p = root.transform.InverseTransformPoint(wheel.transform.position);
                var front = wheel.name.IndexOf("Front", StringComparison.OrdinalIgnoreCase) >= 0;
                string name = "Wheel" + (front ? "F" : "R") + (p.x < 0 ? "L" : "R");
                var part = visual.Find(name);
                if (part == null) throw new InvalidOperationException("Missing " + name);
                var bounds = BoundsOf(part.GetComponentsInChildren<Renderer>(true), root.transform);
                wheel.wheel.visual = part.gameObject; wheel.wheel.nonRotatingVisual = visual.Find(name.Replace("Wheel", "Caliper"))?.gameObject;
                wheel.wheel.radius = bounds.extents.y; wheel.wheel.width = bounds.size.x;
                wheel.spring.maxLength = .22f;
                wheel.spring.maxForce = Mass * 9.81f * .65f;
                wheel.damper.bumpRate=3200;wheel.damper.reboundRate=4400;
                wheel.sideFriction.grip=1.05f;wheel.forwardFriction.grip=1.15f;
                wheel.forceApplicationPointDistance=.8f;
                wheel.layerMask |= LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls", "ParkingArea");
                wheel.transform.position = root.transform.TransformPoint(bounds.center + Vector3.up * wheel.spring.maxLength * (2f / 3f));
                AccessTools.Field(typeof(WheelController), "targetRigidbody").SetValue(wheel, root.GetComponent<Rigidbody>());
                AccessTools.Field(typeof(WheelController), "loadRating").SetValue(wheel, Mass * 9.81f * .5f);
            }
        }

        private static void ConfigureBody(CarController car, MeshRenderer[] renderers)
        {
            var root = car.transform; var bodyRenderer = renderers.Single(r => r.name == "Body");
            var bounds = BoundsOf(renderers, root);
            var bodyBounds = BoundsOf(renderers.Where(r => !r.name.StartsWith("Wheel") && !r.name.StartsWith("Caliper") && !r.name.StartsWith("Light")).Cast<Renderer>().ToArray(), root);
            // The underbody must not touch the road at tire contact height.
            var min = bodyBounds.min; min.y = Mathf.Max(min.y, .20f); bodyBounds.SetMinMax(min, bodyBounds.max);
            var oldHolder = root.Find("CarHolder/Colliders/BodyCollider");
            if (oldHolder == null) throw new InvalidOperationException("Native body collider path unavailable.");
            var oldCollider = oldHolder.GetComponent<Collider>();
            if (oldCollider == null) throw new InvalidOperationException("Native body collider unavailable.");
            foreach (var collider in oldHolder.GetComponents<Collider>()) collider.enabled = false;
            oldHolder.name = "AlfaGiuliaDonorCollider";
            var holder = new GameObject("BodyCollider"); holder.layer = oldHolder.gameObject.layer; holder.tag = oldHolder.tag;
            holder.transform.SetParent(oldHolder.parent, false); holder.transform.SetPositionAndRotation(root.position, root.rotation);
            var box = holder.AddComponent<BoxCollider>(); box.center = bodyBounds.center; box.size = bodyBounds.size;
            // A full-height rectangular collider catches poles at roof height and
            // treats the empty space beside the windscreen as solid bodywork.
            box.center=new Vector3(0,.49f,bodyBounds.center.z);
            box.size=new Vector3(1.80f,.56f,4.48f);
            var bodyMaterial=new PhysicMaterial("AlfaGiulia body"){
                bounciness=0,staticFriction=.2f,dynamicFriction=.15f,
                bounceCombine=PhysicMaterialCombine.Minimum,frictionCombine=PhysicMaterialCombine.Minimum};
            box.sharedMaterial = bodyMaterial; car.vehicleCollider = box;
            var cabin=holder.AddComponent<BoxCollider>();cabin.center=new Vector3(0,1.0f,-.20f);
            cabin.size=new Vector3(1.48f,.62f,2.12f);cabin.sharedMaterial=bodyMaterial;
            if (car.additionalCollider == oldCollider) car.additionalCollider = box;
            car.obstacleToggler.enabled = false; car.navMeshObstacle.enabled = false;
            var obstacle = bodyRenderer.gameObject.AddComponent<NavMeshObstacle>();
            var local = BoundsOf(renderers, bodyRenderer.transform);
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = local.center; obstacle.size = local.size;
            obstacle.carving = true; obstacle.carveOnlyStationary = true;
            car.navMeshObstacle = obstacle; car.obstacleToggler = bodyRenderer.gameObject.AddComponent<VehicleNavMeshObstacleToggler>();
            AccessTools.Field(typeof(EntityController), "renderers").SetValue(car, renderers.Cast<Renderer>().ToArray());
            var features = car.GetComponent<CarFeatures>();
            features.bodyMeshes = new Renderer[] { bodyRenderer }.Concat(renderers.Where(r => r != bodyRenderer)).ToArray(); features.bodyLOD = null;
            if (car.vehicleDeformationController != null)
            {
                car.vehicleDeformationController.meshFilters = new[] { bodyRenderer.GetComponent<MeshFilter>() };
                car.vehicleDeformationController.originalMeshes = Array.Empty<Mesh>();
                car.vehicleDeformationController.gameObject.AddComponent<AlfaGiuliaBodyRepair>()
                    .Configure(car, car.vehicleDeformationController, bodyRenderer.GetComponent<MeshFilter>());
                // Keep native deformation strength; only replace the owned body mesh.
            }
            var targets = (Transform[])AccessTools.Field(typeof(EntityController), "navMeshTargets").GetValue(car);
            for (int i = 0; i < targets.Length; i++) if (targets[i] != null)
                targets[i].position = root.TransformPoint(new Vector3((i % 2 == 0 ? -1 : 1) * (bounds.extents.x + .55f), 0, .2f));
            if (car.vehicleLoadingPosition != null) car.vehicleLoadingPosition.position = root.TransformPoint(new Vector3(0, .5f, bounds.min.z - .5f));
            var fuel = AccessTools.Field(typeof(CarController), "vehicleRefuelingPosition").GetValue(car) as Transform;
            if (fuel != null) fuel.position = root.TransformPoint(new Vector3(bounds.min.x - .3f, .65f, -.8f));
            var spotlights = root.Find("Spotlights"); if (spotlights != null) spotlights.localPosition = new Vector3(0, .68f, 2.0f);
            var brokenSmoke = root.Find("SmokeBrokenCar"); if (brokenSmoke != null) brokenSmoke.localPosition = new Vector3(0, .65f, -1.3f);
            ConfigureLights(car, renderers);
        }

        private static void ConfigureLights(CarController car, MeshRenderer[] renderers)
        {
            var lights = car.vehicleController.effectsManager.lightsManager;
            // Remap each native emissive channel to dedicated surfaces, retaining native logic.
            var channels = new[] { lights.lowBeamLights, lights.highBeamLights, lights.tailLights, lights.brakeLights,
                lights.reverseLights, lights.leftBlinkers, lights.rightBlinkers, lights.extraLights };
            string[] surfaces = { "LightFront", "LightFront", "LightRear", "LightRear", "LightReverse", "LightLeft", "LightRight", "LightFront" };
            for (int i = 0; i < channels.Length; i++)
            {
                var target = renderers.FirstOrDefault(r => r.name == surfaces[i]);
                if (target == null) throw new InvalidOperationException("Missing light surface " + surfaces[i]);
                foreach (var source in channels[i].lightSources)
                    if (source.meshRenderer != null) { source.meshRenderer = target; source.rendererMaterialIndex = 0; }
            }
            var bridge = car.gameObject.AddComponent<AlfaGiuliaLighting>(); bridge.Car = car;
            bridge.Front = renderers.Single(r => r.name == "LightFront"); bridge.Rear = renderers.Single(r => r.name == "LightRear");
            bridge.Reverse = renderers.Single(r => r.name == "LightReverse"); bridge.Left = renderers.Single(r => r.name == "LightLeft");
            bridge.Right = renderers.Single(r => r.name == "LightRight");
        }

    }
}
