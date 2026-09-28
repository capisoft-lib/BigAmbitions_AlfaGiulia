#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using GleyTrafficSystem;
namespace AlfaGiulia.Editor {
 public static class AlfaGiuliaNativeAiChecks {
  public static void Run(){
   try{
    const string aa="C:/Program Files (x86)/Steam/steamapps/common/Big Ambitions/Big Ambitions_Data/StreamingAssets/aa";
    Addressables.InternalIdTransformFunc=loc=>loc.InternalId.Replace("{UnityEngine.AddressableAssets.Addressables.RuntimePath}",aa).Replace(Addressables.RuntimePath,aa);
    var locator=Addressables.LoadContentCatalogAsync(aa+"/catalog.json").WaitForCompletion();
    var key=locator.Keys.OfType<string>().First(k=>k.EndsWith("Prefabs/Vehicles/HonzaMimic.prefab",StringComparison.OrdinalIgnoreCase));
    var donor=Addressables.LoadAssetAsync<GameObject>(key).WaitForCompletion();
    if(donor==null)throw new Exception("Native donor missing");
    var native=donor.GetComponent<VehicleComponent>();
    Debug.Log("[NativeAi] carHolder="+native.carHolder.localPosition+" collider="+donor.GetComponentInChildren<MeshCollider>(true).sharedMesh.bounds);
    foreach(var w in native.allWheels)Debug.Log("[NativeAi] donor wheel="+w.wheelGraphics.position+" anchor="+w.wheelTransform.position+" radius="+w.wheelRadius+" suspension="+w.maxSuspension);
    var holder=new GameObject("Native AI validation");holder.SetActive(false);
    var visual=AssetDatabase.LoadAssetAtPath<GameObject>(AlfaGiuliaMod.VisualPath);
    var ai=AlfaGiuliaPrivateDriver.Create(donor,visual,holder.transform);
    if(ai.gameObject.activeInHierarchy||ai.allWheels.Length!=4||ai.visibilityScript==null)throw new Exception("Invalid AI structure");
    var model=ai.carHolder.Find("AlfaGiuliaVisual");
    if(model==null||ai.visibilityScript.GetComponent<Renderer>().enabled==false)throw new Exception("Invisible AI");
    var collider=ai.GetComponentInChildren<MeshCollider>(true);
    if(collider==null||collider.sharedMesh==null)throw new Exception("PrivateDriverVehicle requires native mesh collider");
    foreach(var wheel in ai.allWheels){
     if(!wheel.wheelGraphics.IsChildOf(model)||wheel.wheelRadius<=0)throw new Exception("Invalid AI wheel");
     Debug.Log("[NativeAi] "+wheel.wheelGraphics.name+" hub="+wheel.wheelGraphics.position+" radius="+wheel.wheelRadius);
    }
    ai.Initialize(0,0,0);
    var nativeLights=ai.GetComponent<VehicleLightsComponent>();
    var bridge=ai.GetComponent<AlfaGiuliaTrafficLights>();
    var tick=typeof(AlfaGiuliaTrafficLights).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
    var properties=new MaterialPropertyBlock();
    nativeLights.SetMainLights(true);nativeLights.SetBrakeLights(true);nativeLights.SetReverseLights(true);nativeLights.UpdateLights(1,true);
    tick.Invoke(bridge,null);
    foreach(var name in new[]{"LightFront","LightRear","LightReverse"}){
     model.Find(name).GetComponent<Renderer>().GetPropertyBlock(properties,0);
     if(properties.GetColor("_EmissiveColor").maxColorComponent<1)throw new Exception("Native AI lamp unlit: "+name);
    }
    nativeLights.SetMainLights(false);nativeLights.SetBrakeLights(false);nativeLights.SetReverseLights(false);nativeLights.UpdateLights(2,true);
    tick.Invoke(bridge,null);
    foreach(var name in new[]{"LightFront","LightRear","LightReverse"}){
     model.Find(name).GetComponent<Renderer>().GetPropertyBlock(properties,0);
     if(properties.GetColor("_EmissiveColor").maxColorComponent>0)throw new Exception("Native AI lamp stuck on: "+name);
    }
    Debug.Log("[NativeAi] PASS native headlight/brake/reverse commands reach actual mod meshes on/off");

    var pool=ScriptableObject.CreateInstance<VehiclePool>();
    pool.trafficCars=new[]{new CarType{vehiclePrefab=donor,nrOfVehicles=3,canBeAiDriven=true}};
    var patcher=new ModPatcher("validation.alfagiulia.private-driver");
    try{
     AlfaGiuliaPrivateDriver.Install(patcher,visual);
     AlfaGiuliaPrivateDriver.RegisterTraffic(pool);AlfaGiuliaPrivateDriver.RegisterTraffic(pool);
     if(pool.trafficCars.Length!=2||pool.trafficCars[0].nrOfVehicles!=3||pool.trafficCars[1].nrOfVehicles!=1)throw new Exception("Traffic registration failed");
     var type=AssetDatabase.LoadAssetAtPath<Vehicles.VehicleTypes.VehicleType>(AlfaGiuliaMod.TypePath);
     var resolved=Helpers.PrivateDriverHelpers.GetAiVehiclePrefab(type);
     if(resolved!=pool.trafficCars[1].vehiclePrefab.GetComponent<VehicleComponent>())throw new Exception("Patched private driver lookup failed");
     var contracts=Helpers.PrivateDriverHelpers.GetContracts();
     if(contracts.Count==0||contracts.Values.Any(c=>!c.usableVehicleTypes.Contains(AlfaGiuliaMod.VehicleTypeName)))throw new Exception("Native contracts rejected mod");
     AlfaGiuliaPrivateDriver.Unload();
     if(pool.trafficCars.Length!=1||contracts.Values.Any(c=>c.usableVehicleTypes.Contains(AlfaGiuliaMod.VehicleTypeName)))throw new Exception("Unload failed");
     Debug.Log("[NativeAi] PASS native initialization, patched lookup, "+contracts.Count+" native contracts, traffic registration/idempotence/unload");
    }finally{patcher.UnpatchAll(patcher.Id);AlfaGiuliaPrivateDriver.Unload();UnityEngine.Object.DestroyImmediate(pool);}
    Debug.Log("[NativeAi] PASS native donor="+donor.name+" wheels=4 visibility=visible collider=mesh");
    UnityEngine.Object.DestroyImmediate(holder);
    EditorApplication.Exit(0);
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}
#endif
