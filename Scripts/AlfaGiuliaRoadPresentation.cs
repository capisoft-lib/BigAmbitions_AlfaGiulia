using System;
using System.Linq;
using NWH.WheelController3D;
using UnityEngine;

namespace AlfaGiulia
{
    // Hamptons flat RoadGroundPlane is at Y=.05; its rendered asphalt is at Y=0.
    // Keep native wheel carriers and all colliders unchanged; move only presentation.
    [DefaultExecutionOrder(100)]
    public sealed class AlfaGiuliaRoadPresentation : MonoBehaviour
    {
        [SerializeField] private CarController car;
        [SerializeField] private Transform body;
        [SerializeField] private Vector3 bodyOrigin;
        [SerializeField] private Transform driver;
        [SerializeField] private Vector3 driverOrigin;
        [SerializeField] private WheelController[] wheels;
        [SerializeField] private Transform[] carriers;
        [SerializeField] private Transform[] meshes;
        private float nextCheck, targetOffset, currentOffset;

        public void Configure(CarController controller, Transform visual, Transform driverVisual)
        {
            car=controller; body=visual; bodyOrigin=body.localPosition;
            driver=driverVisual; if(driver!=null)driverOrigin=driver.localPosition;
            wheels=car.GetComponentsInChildren<WheelController>(true);
            carriers=new Transform[wheels.Length*2]; meshes=new Transform[carriers.Length];
            for(int i=0;i<wheels.Length;i++)
            {
                var wheel=wheels[i].wheel;
                wheel.visual=Wrap(wheel.visual,i*2);
                wheel.nonRotatingVisual=Wrap(wheel.nonRotatingVisual,i*2+1);
            }
        }

        private GameObject Wrap(GameObject mesh, int index)
        {
            if(mesh==null)return null;
            var carrier=new GameObject(mesh.name+"_NativePose").transform;
            carrier.gameObject.layer=mesh.layer;
            carrier.SetParent(transform,false);
            carrier.SetPositionAndRotation(mesh.transform.position,mesh.transform.rotation);
            mesh.transform.SetParent(carrier,true);
            carriers[index]=carrier; meshes[index]=mesh.transform;
            return carrier.gameObject;
        }

        private void LateUpdate()
        {
            if(car==null || car.vehicleController==null || !car.vehicleController.IsInitialized)return;
            if(Time.unscaledTime>=nextCheck)
            {
                nextCheck=Time.unscaledTime+.2f;
                targetOffset=wheels.Length==4 && wheels.All(IsMeasuredFlatRoad) ? .05f : 0f;
            }
            currentOffset=Mathf.MoveTowards(currentOffset,targetOffset,Time.unscaledDeltaTime);
            ApplyOffset(currentOffset);
        }

        private bool IsMeasuredFlatRoad(WheelController wheel)
        {
            if(!wheel.IsGrounded)return false;
            var hits=Physics.RaycastAll(wheel.wheel.worldPosition+Vector3.up*.1f,Vector3.down,
                wheel.wheel.radius+.2f,~0,QueryTriggerInteraction.Ignore);
            var hit=hits.Where(h=>!h.collider.transform.IsChildOf(transform)).OrderBy(h=>h.distance).FirstOrDefault();
            return hit.collider!=null && hit.collider.name.StartsWith("RoadGroundPlane",StringComparison.Ordinal)
                && hit.normal.y>.999f && Mathf.Abs(hit.point.y-.05f)<.002f;
        }

        internal void ApplyOffset(float offset)
        {
            body.localPosition=bodyOrigin-Vector3.up*offset;
            if(driver!=null)driver.localPosition=driverOrigin-Vector3.up*offset;
            var displacement=transform.up*offset;
            for(int i=0;i<meshes.Length;i++)
                if(meshes[i]!=null)meshes[i].position=carriers[i].position-displacement;
        }
    }
}
