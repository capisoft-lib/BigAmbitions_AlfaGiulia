using System;
using System.Linq;
using NWH.WheelController3D;
using UnityEngine;

namespace AlfaGiulia
{
    // Temporary read-only measurements for the reported in-game tyre gap.
    // Never moves a transform, changes physics, or forces a suspension update.
    [DefaultExecutionOrder(200)]
    public sealed class AlfaGiuliaGroundDiagnostics : MonoBehaviour
    {
        public CarController Car;
        private float _next;
        private int _captures;
        private bool _wasDriven;

        private void LateUpdate()
        {
            if (Car == null || Car.vehicleController == null || !Car.vehicleController.IsInitialized) return;
            bool driven = Car.controlledByPlayer;
            bool changed = driven != _wasDriven;
            if (!changed && (_captures >= 4 || Time.realtimeSinceStartup < _next)) return;
            _wasDriven = driven;
            _next = Time.realtimeSinceStartup + 3f;
            _captures++;
            try { Capture(driven); }
            catch (Exception e) { Debug.LogWarning("[AlfaGiuliaGround] measurement unavailable: " + e.Message); }
        }

        private void Capture(bool driven)
        {
            var rb = Car.GetComponent<Rigidbody>();
            var visual = Car.transform.Find("AlfaGiuliaVisual");
            bool currentLamps = visual != null && visual.Find("RearLampHousing") != null;
            Debug.Log("[AlfaGiuliaGround] diagnostic=20260913-v2 capture=" + _captures +
                " driven=" + driven + " root=" + Car.transform.position.ToString("F6") +
                " kinematic=" + rb.isKinematic + " constraints=" + rb.constraints +
                " nativeControllerEnabled=" + Car.vehicleController.enabled +
                " speed=" + rb.velocity.magnitude.ToString("F4") + " currentLampBundle=" + currentLamps);
            foreach (var wc in Car.GetComponentsInChildren<WheelController>(true))
            {
                if (wc.wheel.visual == null) continue;
                float bottom = float.PositiveInfinity;
                foreach (var mf in wc.wheel.visual.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && mf.sharedMesh.isReadable)
                        foreach (var vertex in mf.sharedMesh.vertices)
                            bottom = Mathf.Min(bottom, mf.transform.TransformPoint(vertex).y);
                Vector3 center = wc.wheel.visual.transform.position;
                var hits = Physics.RaycastAll(center + Vector3.up * .6f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.collider.transform.IsChildOf(Car.transform) && h.collider.attachedRigidbody != rb)
                    .OrderBy(h => h.distance).ToArray();
                string ground = hits.Length == 0 ? "none" : hits[0].collider.name +
                    " layer=" + hits[0].collider.gameObject.layer + " groundY=" + hits[0].point.y.ToString("F6") +
                    " visualGap=" + (bottom - hits[0].point.y).ToString("F6") +
                    " physicalGap=" + (wc.wheel.worldPosition.y - wc.wheel.radius - hits[0].point.y).ToString("F6");
                Debug.Log("[AlfaGiuliaGround] wheel=" + wc.name + " enabled=" + wc.enabled +
                    " grounded=" + wc.IsGrounded + " anchor=" + wc.transform.position.ToString("F6") +
                    " visualCenter=" + center.ToString("F6") + " physicalCenter=" + wc.wheel.worldPosition.ToString("F6") +
                    " visualScale=" + wc.wheel.visual.transform.lossyScale.ToString("F6") +
                    " radius=" + wc.wheel.radius.ToString("F6") + " spring=" + wc.spring.length.ToString("F6") +
                    "/" + wc.spring.maxLength.ToString("F6") + " minY=" + bottom.ToString("F6") + " surface=" + ground);
            }
        }
    }
}
