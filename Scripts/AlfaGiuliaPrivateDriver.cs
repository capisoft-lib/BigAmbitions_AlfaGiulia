using System;
using System.Collections.Generic;
using System.Linq;
using Buildings.BuildingTypes.Special.PrivateDriverService;
using GleyTrafficSystem;
using HarmonyLib;
using Helpers;
using UnityEngine;
using Vehicles.VehicleTypes;
using Object = UnityEngine.Object;

namespace AlfaGiulia
{
    // Each independent mod owns only its contract entries and its inactive AI template.
    internal static class AlfaGiuliaPrivateDriver
    {
        private static readonly List<List<string>> AddedTo = new List<List<string>>();
        private static GameObject storage;
        private static VehicleComponent template;
        private static GameObject visual;

        internal static void Install(ModPatcher patcher, GameObject visualPrefab)
        {
            visual = visualPrefab;
            patcher.Patch(AccessTools.Method(typeof(TrafficManager), nameof(TrafficManager.Initialize)),
                prefix: new HarmonyMethod(typeof(AlfaGiuliaPrivateDriver), nameof(RegisterTraffic)));
            patcher.Patch(AccessTools.Method(typeof(TrafficManager), nameof(TrafficManager.Initialize)),
                prefix: new HarmonyMethod(typeof(AlfaGiuliaPrivateDriver), nameof(SynchronizeCount)) { priority = Priority.Last });
            patcher.Patch(AccessTools.Method(typeof(PrivateDriverHelpers), nameof(PrivateDriverHelpers.GetContracts)),
                postfix: new HarmonyMethod(typeof(AlfaGiuliaPrivateDriver), nameof(Contracts)));
            patcher.Patch(AccessTools.Method(typeof(PrivateDriverHelpers), nameof(PrivateDriverHelpers.GetAiVehiclePrefab)),
                prefix: new HarmonyMethod(typeof(AlfaGiuliaPrivateDriver), nameof(AiPrefab)));
        }

        // Runs after normal-priority registrations, including other independent vehicle mods.
        internal static void SynchronizeCount(VehiclePool vehiclePool, ref int nrOfVehicles)
        {
            if (vehiclePool?.trafficCars == null) return;
            nrOfVehicles = vehiclePool.trafficCars
                .Where(c => c != null && c.canBeAiDriven && c.nrOfVehicles > 0)
                .Sum(c => c.nrOfVehicles);
        }

        private static readonly Dictionary<VehiclePool, CarType> TrafficEntries = new Dictionary<VehiclePool, CarType>();
        private static readonly Dictionary<VehiclePool, CarType> TrafficWeightDonors = new Dictionary<VehiclePool, CarType>();
        internal static void RegisterTraffic(VehiclePool vehiclePool)
        {
            if (vehiclePool == null || vehiclePool.trafficCars == null) return;
            if (TrafficEntries.TryGetValue(vehiclePool, out var existing) && vehiclePool.trafficCars.Contains(existing)) return;
            var donor = vehiclePool.trafficCars.FirstOrDefault(c => c != null && c.canBeAiDriven && c.nrOfVehicles > 1 && c.vehiclePrefab != null &&
                string.Equals(c.vehiclePrefab.name, AlfaGiuliaMod.DonorName, StringComparison.OrdinalIgnoreCase));
            if (donor == null)
            { Debug.LogWarning("[AlfaGiulia] No replaceable native traffic weight; ambient entry skipped to keep population stable."); return; }
            var weightDonor = donor;
            if (template == null)
            {
                storage = new GameObject("AlfaGiulia.PrivateDriverTemplates"); storage.SetActive(false); if (Application.isPlaying) Object.DontDestroyOnLoad(storage);
                template = Create(donor.vehiclePrefab, visual, storage.transform);
            }
            var entry = new CarType { name = "AlfaGiulia Traffic", vehiclePrefab = template.gameObject,
                nrOfVehicles = 1, canBeAiDriven = true, canBeRandomlyParked = false, hasParkedVersion = false };
            var expanded = new CarType[vehiclePool.trafficCars.Length + 1];
            Array.Copy(vehiclePool.trafficCars, expanded, vehiclePool.trafficCars.Length);
            expanded[expanded.Length - 1] = entry;
            weightDonor.nrOfVehicles--;
            vehiclePool.trafficCars = expanded;
            TrafficEntries[vehiclePool] = entry;
            TrafficWeightDonors[vehiclePool] = weightDonor;
            Debug.Log("[AlfaGiulia] Registered ambient traffic and private driver AI template.");
        }

        internal static void Contracts(Dictionary<string, PrivateDriverContract> __result)
        {
            if (__result == null) return;
            foreach (var contract in __result.Values)
            {
                if (contract == null || contract.usableVehicleTypes == null) continue;
                var list = contract.usableVehicleTypes;
                if (list.Contains(AlfaGiuliaMod.VehicleTypeName)) continue;
                list.Add(AlfaGiuliaMod.VehicleTypeName);
                AddedTo.Add(list);
            }
        }

        private static bool AiPrefab(VehicleType vehicleType, ref VehicleComponent __result)
        {
            if (vehicleType == null || vehicleType.vehicleTypeName != AlfaGiuliaMod.VehicleTypeName) return true;
            if (template == null)
            {
                var donor = PrefabHelper.LoadPrefabAssetByName("Vehicles/" + AlfaGiuliaMod.DonorName);
                if (donor == null) throw new InvalidOperationException("AlfaGiulia private driver: native AI chassis missing.");
                storage = new GameObject("AlfaGiulia.PrivateDriverTemplates");
                storage.SetActive(false);
                if (Application.isPlaying) Object.DontDestroyOnLoad(storage);
                try { template = Create(donor, visual, storage.transform); }
                catch { DestroyStorage(); storage = null; throw; }
            }
            __result = template;
            return false;
        }

        internal static VehicleComponent Create(GameObject donor, GameObject visualPrefab, Transform parent)
        {
            if (parent == null || parent.gameObject.activeInHierarchy) throw new ArgumentException("Inactive parent required.");
            var root = Object.Instantiate(donor, parent, false);
            try
            {
                root.name = "AlfaGiulia.PrivateDriver";
                var ai = root.GetComponent<VehicleComponent>();
                if (ai == null || ai.carHolder == null || ai.allWheels == null || ai.allWheels.Length != 4)
                    throw new InvalidOperationException("AlfaGiulia private driver: invalid native AI chassis.");
                var nativeRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
                var model = Object.Instantiate(visualPrefab, ai.carHolder, false);
                model.name = "AlfaGiuliaVisual";
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
                foreach (var renderer in nativeRenderers) renderer.enabled = false;
                foreach (var lod in root.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
                // Retain the native physics, collider, navigation and traffic-light receivers.
                // Gley rotates wheelGraphics about its origin: introduce a hub at mesh centre.
                foreach (var wheel in ai.allWheels)
                {
                    bool front = wheel.wheelPosition == Wheel.WheelPosition.Front;
                    bool left = root.transform.InverseTransformPoint(wheel.wheelGraphics.position).x < 0;
                    var part = model.transform.Find("Wheel" + (front ? "F" : "R") + (left ? "L" : "R"));
                    if (part == null) throw new InvalidOperationException("AlfaGiulia private driver: missing wheel.");
                    var bounds = AlfaGiuliaVehicle.BoundsOf(part.GetComponentsInChildren<Renderer>(true), root.transform);
                    var hub = new GameObject(part.name + "Hub").transform;
                    hub.SetParent(model.transform, false);
                    hub.position = root.transform.TransformPoint(bounds.center);
                    part.SetParent(hub, true);
                    wheel.wheelGraphics = hub;
                    var p = root.transform.InverseTransformPoint(wheel.wheelTransform.position);
                    wheel.wheelTransform.position = root.transform.TransformPoint(new Vector3(bounds.center.x, p.y, bounds.center.z));
                    wheel.wheelRadius = bounds.extents.y;
                    wheel.wheelCircumference = 2 * Mathf.PI * wheel.wheelRadius;
                    wheel.raycastLength = wheel.wheelRadius + wheel.maxSuspension;
                }
                var fronts = ai.allWheels.Where(w => w.wheelPosition == Wheel.WheelPosition.Front).ToArray();
                var rears = ai.allWheels.Where(w => w.wheelPosition == Wheel.WheelPosition.Back).ToArray();
                ai.wheelDistance = Mathf.Abs(fronts.Average(w => root.transform.InverseTransformPoint(w.wheelGraphics.position).z)
                    - rears.Average(w => root.transform.InverseTransformPoint(w.wheelGraphics.position).z));
                AlfaGiuliaMaterials.Repair(model);
                model.AddComponent<AlfaGiuliaPaint>();
                var features = root.GetComponent<CarFeatures>();
                if (features != null) { features.bodyMeshes = model.GetComponentsInChildren<Renderer>(true); features.bodyLOD = null; }
                var body = model.transform.Find("Body").GetComponent<MeshRenderer>();
                ai.visibilityScript = body.GetComponent<GleyUrbanAssets.VisibilityScript>() ?? body.gameObject.AddComponent<GleyUrbanAssets.VisibilityScript>();
                var nativeLights = root.GetComponent<VehicleLightsComponent>();
                if (nativeLights != null) nativeLights.meshRenderer = body;
                var bridge = root.AddComponent<AlfaGiuliaTrafficLights>();
                bridge.Configure(model.transform, nativeLights);
                root.AddComponent<AlfaGiuliaTrafficVisualGate>().Configure(model);
                root.SetActive(true);
                return ai;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        private static void DestroyStorage()
        {
            if (storage == null) return;
            if (Application.isPlaying) Object.Destroy(storage); else Object.DestroyImmediate(storage);
        }
        internal static void Unload()
        {
            foreach (var pair in TrafficEntries)
            {
                if (pair.Key == null) continue;
                if (pair.Key.trafficCars != null)
                    pair.Key.trafficCars = pair.Key.trafficCars.Where(c => !ReferenceEquals(c, pair.Value)).ToArray();
                if (TrafficWeightDonors.TryGetValue(pair.Key, out var weightDonor) && weightDonor != null)
                    weightDonor.nrOfVehicles++;
            }
            TrafficEntries.Clear();
            TrafficWeightDonors.Clear();
            foreach (var list in AddedTo) list.Remove(AlfaGiuliaMod.VehicleTypeName);
            AddedTo.Clear();
            if (storage != null) DestroyStorage();
            storage = null; template = null; visual = null;
        }
    }


    // Only this mod's visual scripts are gated; AI simulation and native lights stay active.
    internal sealed class AlfaGiuliaTrafficVisualGate : MonoBehaviour {
        private Renderer[] renderers;
        private MonoBehaviour[] scripts;
        private System.Action[] synchronize;
        private bool visible;
        private float nextPoll;
        internal void Configure(GameObject visual) {
            renderers=visual.GetComponentsInChildren<Renderer>(true);
            var found=new System.Collections.Generic.List<MonoBehaviour>();
            var sync=new System.Collections.Generic.List<System.Action>();
            foreach(var behaviour in GetComponentsInChildren<MonoBehaviour>(true)) {
                if(behaviour==null || behaviour==this || !behaviour.enabled)continue;
                var type=behaviour.GetType();var name=type.Name;
                if(type.Namespace!=typeof(AlfaGiuliaTrafficVisualGate).Namespace ||
                    !(name=="AlfaGiuliaPaint" || name=="AlfaGiuliaLighting" || name=="AlfaGiuliaTrafficLights"))continue;
                found.Add(behaviour);
                var method=type.GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                sync.Add(method==null?null:(System.Action)System.Delegate.CreateDelegate(typeof(System.Action),behaviour,method));
            }
            scripts=found.ToArray();synchronize=sync.ToArray();
            foreach(var renderer in renderers) {
                var relay=renderer.gameObject.GetComponent<AlfaGiuliaVisibilityRelay>() ?? renderer.gameObject.AddComponent<AlfaGiuliaVisibilityRelay>();
                relay.Gate=this;
            }
            SetVisualsEnabled(false);
        }
        internal void VisibleNow() {
            if(visible)return;visible=true;SetVisualsEnabled(true);
            // The visibility callback runs before this renderer is drawn. Rebind and
            // apply the current native state now, without waiting for the next poll.
            for(int i=0;i<synchronize.Length;i++)if(scripts[i]!=null)synchronize[i]?.Invoke();
        }
        private void LateUpdate() {
            if(Time.unscaledTime<nextPoll)return;nextPoll=Time.unscaledTime+.15f;
            if(renderers!=null)foreach(var renderer in renderers)
                if(renderer!=null && renderer.isVisible) { VisibleNow();return; }
            if(!visible)return;visible=false;SetVisualsEnabled(false);
        }
        private void SetVisualsEnabled(bool enabled) {
            if(scripts==null)return;
            foreach(var script in scripts)if(script!=null)script.enabled=enabled;
        }
    }
    internal sealed class AlfaGiuliaVisibilityRelay : MonoBehaviour {
        internal AlfaGiuliaTrafficVisualGate Gate;
        private void OnBecameVisible(){if(Gate!=null)Gate.VisibleNow();}
    }
}
