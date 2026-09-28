#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlfaGiulia.Editor
{
    public static class AlfaGiuliaBodyRepairChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int checks;
        private static readonly List<Object> cleanup = new List<Object>();

        public static void VerifyIntegration()
        {
            try
            {
                AlfaGiuliaBuild.Verify();
                AlfaGiuliaBuild.VerifyPackage();
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            var patcher = new ModPatcher("capisoft.alfagiulia.repair-checks");
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Mods/AlfaGiulia/Generated/Body.asset");
                Check(asset != null && asset.isReadable, "actual Giulia body is readable");
                var source = Object.Instantiate(asset); cleanup.Add(source);
                var intact = source.vertices;
                var native = Fixture(source, false);
                native.Start();
                Dent(native); native.Reset();
                Check(Same(native.meshFilters[0].sharedMesh.vertices, intact), "native first repair restores geometry");
                Dent(native); native.Reset();
                Check(Same(native.meshFilters[0].sharedMesh.vertices, intact), "native repeated repair restores geometry without pending collisions");
                Queue(native).Enqueue((0, Hit(native)));
                native.Reset();
                typeof(VehicleDeformationController).GetMethod("LateUpdate", Private).Invoke(native, null);
                Check(!Same(native.meshFilters[0].sharedMesh.vertices, intact), "REPRODUCED: queued collision deforms body after native repair");

                source = Object.Instantiate(asset); cleanup.Add(source);
                var template = Fixture(source, true);
                AlfaGiuliaBodyRepair.Install(patcher);
                var first = Object.Instantiate(template.gameObject).GetComponent<VehicleDeformationController>();
                var second = Object.Instantiate(template.gameObject).GetComponent<VehicleDeformationController>();
                cleanup.Add(first.gameObject); cleanup.Add(second.gameObject);
                // Match the native per-spawn instance assignment while keeping scene Awake
                // disabled: these tests run real Start/Reset/DeformMesh, not game services.
                Car(first).vehicleInstance = new VehicleInstance(AlfaGiuliaMod.VehicleTypeName);
                Car(second).vehicleInstance = new VehicleInstance(AlfaGiuliaMod.VehicleTypeName);
                first.Start(); second.Start();
                cleanup.Add(first.meshFilters[0].sharedMesh); cleanup.Add(second.meshFilters[0].sharedMesh);
                var firstMesh = first.meshFilters[0].sharedMesh;
                Check(firstMesh != second.meshFilters[0].sharedMesh && firstMesh != source,
                    "two template clones own independent meshes");
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    var hit = Dent(first);
                    Check(!Same(firstMesh.vertices, intact), "collision visibly deforms body " + cycle);
                    Check(Same(second.meshFilters[0].sharedMesh.vertices, intact), "other car stays intact " + cycle);
                    Queue(first).Enqueue((0, hit));
                    first.Reset();
                    Check(Same(firstMesh.vertices, intact), "repair restores all vertices " + cycle);
                    Check(firstMesh.normals.SequenceEqual(source.normals) && firstMesh.tangents.SequenceEqual(source.tangents)
                        && firstMesh.bounds == source.bounds, "repair restores shading and bounds " + cycle);
                    Check(Queue(first).Count == 0 && Car(first).vehicleInstance.deformations.Count == 0,
                        "repair clears queued and saved damage " + cycle);
                    typeof(VehicleDeformationController).GetMethod("LateUpdate", Private).Invoke(first, null);
                    Check(Same(firstMesh.vertices, intact), "no delayed dent after repair " + cycle);
                }
                Check(first.meshFilters[0].sharedMesh == firstMesh, "repair reuses the owned mesh");
                Check(Same(source.vertices, intact) && Same(asset.vertices, intact), "source and imported asset stay pristine");

                // Save/load the native deformation payload, then replay via native Start.
                Dent(first);
                string saved = JsonUtility.ToJson(Car(first).vehicleInstance);
                var reloaded = Object.Instantiate(template.gameObject).GetComponent<VehicleDeformationController>();
                cleanup.Add(reloaded.gameObject);
                Car(reloaded).vehicleInstance = new VehicleInstance(AlfaGiuliaMod.VehicleTypeName);
                JsonUtility.FromJsonOverwrite(saved, Car(reloaded).vehicleInstance);
                reloaded.Start(); cleanup.Add(reloaded.meshFilters[0].sharedMesh);
                Check(!Same(reloaded.meshFilters[0].sharedMesh.vertices, intact), "saved dents replay on fresh instance");
                reloaded.Reset();
                Check(Same(reloaded.meshFilters[0].sharedMesh.vertices, intact), "loaded damage repairs correctly");
                string repairedSave = JsonUtility.ToJson(Car(reloaded).vehicleInstance);
                var repairedInstance = new VehicleInstance(AlfaGiuliaMod.VehicleTypeName);
                JsonUtility.FromJsonOverwrite(repairedSave, repairedInstance);
                Check(repairedInstance.deformations.Count == 0, "repaired save has no dents to replay");

                var vanillaSource = Object.Instantiate(asset); cleanup.Add(vanillaSource);
                var vanilla = Fixture(vanillaSource, false); vanilla.Start();
                Dent(vanilla); vanilla.Reset();
                Check(Same(vanilla.meshFilters[0].sharedMesh.vertices, intact), "unmarked vehicle retains native reset");
                Queue(vanilla).Enqueue((0, Hit(vanilla)));
                vanilla.Reset();
                Check(Queue(vanilla).Count == 1, "Giulia queue patch does not affect unmarked vehicles");
                Check(first.originalMeshes[0] == source, "native fallback retains pristine source");
                patcher.UnpatchAll(patcher.Id);
                first.Reset();
                Check(Same(first.meshFilters[0].sharedMesh.vertices, intact), "native repair remains valid after patch unload");
                AlfaGiuliaBodyRepair.Install(patcher);
                Dent(first); first.Reset();
                Check(Same(first.meshFilters[0].sharedMesh.vertices, intact), "repair recovers after patch reload");
                var owned = first.meshFilters[0].sharedMesh;
                typeof(AlfaGiuliaBodyRepair).GetMethod("OnDestroy", Private)
                    .Invoke(first.GetComponent<AlfaGiuliaBodyRepair>(), null);
                Check(owned == null && source != null, "teardown releases owned mesh and preserves source");
                Debug.Log("[AlfaGiuliaBodyRepair] PASS " + checks + " checks; native pending-collision repair defect reproduced.");
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
            finally
            {
                patcher.UnpatchAll(patcher.Id);
                foreach (var obj in cleanup) if (obj != null) Object.DestroyImmediate(obj);
                cleanup.Clear();
            }
        }

        private static VehicleDeformationController Fixture(Mesh mesh, bool guarded)
        {
            var holder = new GameObject("Giulia repair fixture"); holder.SetActive(false); cleanup.Add(holder);
            var donor = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Mods/Example-Vehicle/TurboHonza.prefab");
            var root = Object.Instantiate(donor, holder.transform); root.SetActive(false);
            var car = root.GetComponent<CarController>();
            car.vehicleInstance = new VehicleInstance(AlfaGiuliaMod.VehicleTypeName);
            var bodyObject = new GameObject("Body"); bodyObject.transform.SetParent(root.transform, false);
            var body = bodyObject.AddComponent<MeshFilter>(); body.sharedMesh = mesh;
            var controller = car.vehicleDeformationController;
            typeof(VehicleDeformationController).GetField("carController", Private).SetValue(controller, car);
            controller.meshFilters = new[] { body }; controller.originalMeshes = Array.Empty<Mesh>();
            if (guarded) controller.gameObject.AddComponent<AlfaGiuliaBodyRepair>().Configure(car, controller, body);
            return controller;
        }

        private static CarController Car(VehicleDeformationController controller) => controller.GetComponent<CarController>();
        private static Queue<(int, VehicleDeformationController.VehicleDeformation)> Queue(VehicleDeformationController controller)
            => (Queue<(int, VehicleDeformationController.VehicleDeformation)>)typeof(VehicleDeformationController)
                .GetField("_deformationQueue", Private).GetValue(controller);
        private static VehicleDeformationController.VehicleDeformation Hit(VehicleDeformationController controller)
            => new VehicleDeformationController.VehicleDeformation {
                decelerationMagnitude = 1000,
                points = new[] { new VehicleDeformationController.VehicleDeformation.VehicleDeformationPoint {
                    point = controller.meshFilters[0].sharedMesh.vertices[0], normal = Vector3.up, deformationRandomness = 1 } }
            };
        private static VehicleDeformationController.VehicleDeformation Dent(VehicleDeformationController controller)
        {
            var hit = Hit(controller);
            Car(controller).vehicleInstance.deformations.Add(hit);
            typeof(VehicleDeformationController).GetMethod("DeformMesh", Private).Invoke(controller, new object[] { 0, hit });
            return hit;
        }
        private static bool Same(Vector3[] a, Vector3[] b) => a.SequenceEqual(b);
        private static void Check(bool passed, string label)
        {
            if (!passed) throw new InvalidOperationException("Giulia repair check failed: " + label);
            checks++; Debug.Log("[GiuliaRepairCheck] " + label);
        }
    }
}
#endif
