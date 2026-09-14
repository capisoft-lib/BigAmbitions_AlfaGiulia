#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using NWH.WheelController3D;
using Vehicles.VehicleTypes;
using Object=UnityEngine.Object;
namespace AlfaGiulia.Editor {
public static class AlfaGiuliaSuspensionProbe {
 static object Call(object obj,string method)=>obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,null);
 static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
 public static void Run(){try{
  AlfaGiuliaBuild.Verify();
  var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="RoadGroundPlane (3)";floor.transform.position=new Vector3(0,-.05f,0);floor.transform.localScale=new Vector3(30,.2f,30);
  var storage=new GameObject("inactive probe");storage.SetActive(false);
  var donor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Mods/Example-Vehicle/TurboHonza.prefab");
  var visual=AssetDatabase.LoadAssetAtPath<GameObject>(AlfaGiuliaMod.VisualPath);
  var type=AssetDatabase.LoadAssetAtPath<VehicleType>(AlfaGiuliaMod.TypePath);
  var root=AlfaGiuliaVehicle.Create(donor,visual,type,storage.transform);root.transform.position=Vector3.up*.08f;
  var wheels=root.GetComponentsInChildren<WheelController>(true);
  var rb=root.GetComponent<Rigidbody>();rb.isKinematic=false;rb.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ|RigidbodyConstraints.FreezeRotation;
  foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))if(!(behaviour is WheelController))behaviour.enabled=false;
  root.transform.SetParent(null,true);
  foreach(var c in root.GetComponentsInChildren<Collider>(true))if(c.enabled&&!c.isTrigger)Debug.Log("[SuspensionProbe] collider "+c.name+" type="+c.GetType().Name+" min="+c.bounds.min.ToString("F6"));
  foreach(var wc in wheels){
   Call(wc,"Start");wc.layerMask=1;
   var detection=wc.GetComponent<StandardGroundDetection>();Call(detection,"Awake");
  }
  Physics.autoSimulation=false;
  for(int frame=0;frame<300;frame++){
   foreach(var wc in wheels){
   Set(wc,"_transformPosition",wc.transform.position);Set(wc,"_transformRotation",wc.transform.rotation);Set(wc,"_transformUp",wc.transform.up);
    Physics.SyncTransforms();bool hit=(bool)Call(wc,"FindTheHitPoint");Set(wc,"_isGrounded",hit);wc.spring.prevLength=wc.spring.length;
    Call(wc,"UpdateSpringAndDamper");Call(wc,"UpdateWheelValues");
   }
   Physics.Simulate(Time.fixedDeltaTime);
  }
  Debug.Log("[SuspensionProbe] settled root="+root.transform.position.ToString("F6"));
  var presentation=root.GetComponent<AlfaGiuliaRoadPresentation>();
  var roadCheck=typeof(AlfaGiuliaRoadPresentation).GetMethod("IsMeasuredFlatRoad",BindingFlags.Instance|BindingFlags.NonPublic);
  foreach(var wc in wheels)if(!(bool)roadCheck.Invoke(presentation,new object[]{wc}))throw new Exception("Measured road was not recognized");
  floor.name="BridgeConnectionGroundPlane";
  foreach(var wc in wheels)if((bool)roadCheck.Invoke(presentation,new object[]{wc}))throw new Exception("Unmeasured bridge must not be lowered");
  floor.name="RoadGroundPlane (3)";
  presentation.ApplyOffset(.05f);
  foreach(var wc in wheels){
   var mf=wc.wheel.visual.GetComponentInChildren<MeshFilter>();float lowest=mf.sharedMesh.vertices.Min(v=>mf.transform.TransformPoint(v).y);
   if(Mathf.Abs(lowest)>.0015f || Mathf.Abs(wc.wheel.worldPosition.y-wc.wheel.radius-.05f)>.0015f)
    throw new Exception("Physical or rendered road contact failed: "+wc.name);
   Debug.Log("[SuspensionProbe] "+wc.name+" anchor="+wc.transform.position.ToString("F6")+" scale="+wc.transform.lossyScale+" wheelScale="+mf.transform.lossyScale+" radius="+wc.wheel.radius+" spring="+wc.spring.length+" grounded="+wc.IsGrounded+" minY="+lowest.ToString("F6")+" physicalBottom="+(wc.wheel.worldPosition.y-wc.wheel.radius));
  }
  Object.DestroyImmediate(root);Object.DestroyImmediate(storage);Object.DestroyImmediate(floor);Debug.Log("[SuspensionProbe] PASS");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
}
#endif
