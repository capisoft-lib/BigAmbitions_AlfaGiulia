using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AlfaGiulia
{
    // Serialized references are copied from the inactive template. Runtime meshes
    // are deliberately not serialized: each spawned car owns its deformation mesh.
    public sealed class AlfaGiuliaBodyRepair : MonoBehaviour
    {
        [SerializeField] private CarController car;
        [SerializeField] private VehicleDeformationController deformation;
        [SerializeField] private MeshFilter body;
        [SerializeField] private Mesh pristine;
        private Mesh working;

        private static readonly FieldInfo QueueField = AccessTools.Field(
            typeof(VehicleDeformationController), "_deformationQueue");

        internal static void Install(ModPatcher patcher)
        {
            if (QueueField == null || QueueField.FieldType !=
                typeof(Queue<(int, VehicleDeformationController.VehicleDeformation)>))
                throw new InvalidOperationException("Giulia deformation queue API changed.");
            patcher.Patch(AccessTools.Method(typeof(VehicleDeformationController), "Start"),
                prefix: new HarmonyMethod(typeof(AlfaGiuliaBodyRepair), nameof(BeforeStart)),
                postfix: new HarmonyMethod(typeof(AlfaGiuliaBodyRepair), nameof(AfterStart)));
            patcher.Patch(AccessTools.Method(typeof(VehicleDeformationController), "Reset"),
                prefix: new HarmonyMethod(typeof(AlfaGiuliaBodyRepair), nameof(BeforeReset)));
        }

        public void Configure(CarController controller, VehicleDeformationController damage, MeshFilter filter)
        {
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                throw new InvalidOperationException("Giulia body must have a readable pristine mesh.");
            car = controller;
            deformation = damage;
            body = filter;
            pristine = filter.sharedMesh;
        }

        private void Initialize()
        {
            if (working != null && body.sharedMesh == working) return;
            ReleaseMesh();
            body.sharedMesh = pristine;
            working = body.mesh;
            working.name = "AlfaGiulia body (instance)";
        }

        private static void BeforeStart(VehicleDeformationController __instance)
        {
            var repair = __instance.GetComponent<AlfaGiuliaBodyRepair>();
            if (repair != null && repair.deformation == __instance) repair.Initialize();
        }

        private static void AfterStart(VehicleDeformationController __instance)
        {
            var repair = __instance.GetComponent<AlfaGiuliaBodyRepair>();
            if (repair != null && repair.deformation == __instance)
                // Keep the native fallback valid if our Harmony patches are unloaded.
                __instance.originalMeshes = new[] { repair.pristine };
        }

        private static bool BeforeReset(VehicleDeformationController __instance)
        {
            var repair = __instance.GetComponent<AlfaGiuliaBodyRepair>();
            if (repair == null || repair.deformation != __instance) return true;
            repair.Restore();
            return false;
        }

        private void Restore()
        {
            Initialize();
            ((Queue<(int, VehicleDeformationController.VehicleDeformation)>)
                QueueField.GetValue(deformation)).Clear();
            car.vehicleInstance.deformations.Clear();
            working.vertices = pristine.vertices;
            // Preserve the artist's normals/tangents exactly, including hard edges.
            working.normals = pristine.normals;
            working.tangents = pristine.tangents;
            working.bounds = pristine.bounds;
        }

        private void OnDestroy()
        {
            ReleaseMesh();
        }

        private void ReleaseMesh()
        {
            if (working == null) return;
            if (Application.isPlaying) Destroy(working);
            else DestroyImmediate(working);
            working = null;
        }
    }
}
