#if UNITY_EDITOR
using System;
using System.Linq;
using Data.VehicleColors;
using UnityEngine;
using NWH.WheelController3D;
using Object=UnityEngine.Object;
namespace AlfaGiulia.Editor {
internal static class AlfaGiuliaRegressionChecks {
 internal static void Verify(CarController car,Action<bool,string> check){
  var visual=car.transform.Find("AlfaGiuliaVisual");
  var features=car.GetComponent<CarFeatures>();var color=ScriptableObject.CreateInstance<VehicleColor>();
  var block=new MaterialPropertyBlock();var paint=visual.GetComponent<AlfaGiuliaPaint>();
  try{
   foreach(var tint in new[]{new Color32(25,180,55,255),new Color32(40,70,230,255),new Color32(240,240,240,255)}){
    color.tint=tint;features.SetColor(color);paint.Sync();int painted=0;
    foreach(var r in car.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.name.StartsWith("M_AlfaGiulia_"))))for(int i=0;i<r.sharedMaterials.Length;i++){
     r.GetPropertyBlock(block,i);
     if(r.sharedMaterials[i].name.StartsWith("M_AlfaGiulia_Paint_")){
      painted++;check(Vector4.Distance(block.GetColor("_BaseColor"),(Color)tint)<.001f,"native purchase paint reaches body");
     }else check(block.GetColor("_BaseColor")==Color.clear,"trim lamps wheels cabin excluded from paint");
    }
    check(painted>0,"original paint panels present");
   }
  }finally{Object.DestroyImmediate(color);}
  var lights=car.GetComponent<AlfaGiuliaLighting>();
  var lamps=new[]{lights.Front,lights.Rear,lights.Reverse,lights.Left,lights.Right};
  foreach(var lamp in lamps)foreach(var material in lamp.sharedMaterials){
   check(material.GetFloat("_EmissiveExposureWeight")==0,"signal emission survives daylight exposure and shader repair");
   check(material.GetFloat("_DoubleSidedEnable")==1,"imported lamp faces render from both sides");
  }
  check(lights.Left.GetComponent<MeshFilter>().sharedMesh.vertices.All(v=>v.x<0),"left indicators on vehicle left after FBX conversion");
  check(lights.Right.GetComponent<MeshFilter>().sharedMesh.vertices.All(v=>v.x>0),"right indicators on vehicle right after FBX conversion");
  check(lights.Rear.GetComponent<MeshFilter>().sharedMesh.vertices.All(v=>v.y>.6f),"bumper reflectors excluded from powered lamps");
  // Reproduce the indexed block written by native LightSource in a running game.
  foreach(var r in lamps)for(int i=0;i<r.sharedMaterials.Length;i++){
   block.Clear();block.SetFloat("_NativeLightBrightness",1);r.SetPropertyBlock(block,i);
  }
  lights.ApplyStates(true,false,true,true,true,true,false);
  var expected=new[]{3f,5f,3f,5f,0f};
  for(int n=0;n<lamps.Length;n++)for(int i=0;i<lamps[n].sharedMaterials.Length;i++){
   lamps[n].GetPropertyBlock(block,i);
   check(Mathf.Abs(block.GetColor("_EmissiveColor").r-expected[n])<.01f,"emission visible despite native indexed block");
   check(block.GetFloat("_NativeLightBrightness")==1,"native properties retained");
  }
  lights.ApplyStates(false,false,false,false,false,false,true);
  lights.Right.GetPropertyBlock(block,0);check(block.GetColor("_EmissiveColor").g>1,"right amber indexed");
  lights.Left.GetPropertyBlock(block,0);check(block.GetColor("_EmissiveColor").maxColorComponent==0,"left off indexed");
  var rb=car.GetComponent<Rigidbody>();
  // Exercise the game's actual command receiver, not NWH's unused blinker inputs.
  var blinkers=car.GetComponentInChildren<Vehicles.Components.VehicleBlinker>(true);
  // The SDK TurboHonza fixture predates VehicleBlinker; install the current game
  // component in this test only. Runtime cars retain their native donor component.
  if(blinkers==null)blinkers=car.gameObject.AddComponent<Vehicles.Components.VehicleBlinker>();
  var serializedBlinkers=new UnityEditor.SerializedObject(blinkers);
  serializedBlinkers.FindProperty("carFeatures").objectReferenceValue=features;
  serializedBlinkers.ApplyModifiedPropertiesWithoutUndo();
  bool wasControlled=car.controlledByPlayer;car.controlledByPlayer=true;
  Action<bool,bool> verifyBlinkers=(left,right)=>{
   lights.SyncBlinkers();
   foreach(var pair in new[]{Tuple.Create(lights.Left,left),Tuple.Create(lights.Right,right)})
    for(int i=0;i<pair.Item1.sharedMaterials.Length;i++){
     pair.Item1.GetPropertyBlock(block,i);
     check((block.GetColor("_EmissiveColor").g>1)==pair.Item2,"native blinker command reaches every lamp material");
    }
  };
  Action<int> phase=value=>{
   features.bodyMeshes[0].GetPropertyBlock(block);
   block.SetInt("_IsBlinkerOn",value);features.bodyMeshes[0].SetPropertyBlock(block);
  };
  phase(1);verifyBlinkers(false,false);
  blinkers.ToggleLeftBlinker();verifyBlinkers(true,false);
  phase(0);verifyBlinkers(false,false);
  phase(1);verifyBlinkers(true,false);
  blinkers.ToggleRightBlinker();verifyBlinkers(false,true);
  phase(0);verifyBlinkers(false,false);
  phase(1);car.controlledByPlayer=false;verifyBlinkers(false,false);
  car.controlledByPlayer=true;blinkers.ToggleRightBlinker();verifyBlinkers(false,false);
  phase(0);car.controlledByPlayer=wasControlled;
  check(rb.centerOfMass.y<=.33f&&rb.angularDrag>=.3f,"lower center of mass and rotational damping");
  check(rb.collisionDetectionMode==CollisionDetectionMode.ContinuousDynamic,"continuous pole collision detection");
  check(rb.maxDepenetrationVelocity<=3,"bounded collision separation velocity");
  var body=(BoxCollider)car.vehicleCollider;
  check(body.size.y<.6f&&body.sharedMaterial.bounciness==0,"low body collider without bounce");
  check(body.GetComponents<BoxCollider>().Length==2,"separate body and cabin colliders");
  float previousAngle=42;
  foreach(var speed in new[]{0f,30f,50f,80f,120f,180f,307f}){
   var steering=car.vehicleController.steering;
   float angle=steering.speedSensitiveSteeringCurve.Evaluate(speed/180)*steering.maximumSteerAngle;
   check(angle>=8.39f&&angle<=previousAngle+.01f,"usable progressively reduced steering at "+speed+" km/h");
   previousAngle=angle;
  }
  check(car.vehicleController.steering.degreesPerSecondLimit<=95,"progressive keyboard steering");
  var powertrain=car.vehicleController.powertrain;
  check(powertrain.engine.Output==powertrain.clutch&&powertrain.clutch.Output==powertrain.transmission,"engine clutch gearbox connected");
  check(powertrain.clutch.slipTorque>600&&powertrain.clutch.slipTorque<1200,"clutch torque matched to V6");
  check(powertrain.engine.inertia>=.3f,"engine rev inertia above donor");
  var trans=powertrain.transmission;
  check(trans.UpshiftRPM*(1+trans.variableShiftIntensity)<6500,"full throttle upshift before power peak and limiter");
  check(trans.UpshiftRPM<5000&&trans.DownshiftRPM<2000,"earlier light throttle shifts");
  var steer=car.vehicleController.steering;
  check(steer.maximumSteerAngle>=42,"parking steering restored");
  check(steer.speedSensitiveSteeringCurve.Evaluate(30f/180)*steer.maximumSteerAngle>=25,"city steering restored");
  check(steer.speedSensitiveSteeringCurve.Evaluate(50f/180)*steer.maximumSteerAngle>16,"steering remains available while accelerating");
  check(Mathf.Abs(steer.speedSensitiveSteeringCurve.Evaluate(.3f)-.4f)<.001f,"reference native curve at 54 km/h");
  check(Mathf.Abs(steer.speedSensitiveSteeringCurve.Evaluate(1)-.2f)<.001f,"reference native curve at 180 km/h");
  check(car.GetComponentsInChildren<WheelController>(true).All(w=>w.spring.maxLength<=.22f&&w.damper.reboundRate>w.damper.bumpRate),"suspension rebound damped");
 }
}}
#endif
