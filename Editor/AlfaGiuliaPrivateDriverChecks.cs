#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Buildings.BuildingTypes.Special.PrivateDriverService;
using UnityEngine;
namespace AlfaGiulia.Editor {
internal static class AlfaGiuliaPrivateDriverChecks {
 internal static void Verify(Action<bool,string> check) {
  var first=ScriptableObject.CreateInstance<PrivateDriverContract>();
  var second=ScriptableObject.CreateInstance<PrivateDriverContract>();
  var existing=ScriptableObject.CreateInstance<PrivateDriverContract>();
  first.usableVehicleTypes=new List<string>{"native","other-mod"};
  second.usableVehicleTypes=new List<string>{"luxury-native"};
  existing.usableVehicleTypes=new List<string>{AlfaGiuliaMod.VehicleTypeName};
  first.maxCars=2;first.costPerDay=123;
  var contracts=new Dictionary<string,PrivateDriverContract>{{"first",first},{"second",second},{"existing",existing}};
  try {
   AlfaGiuliaPrivateDriver.Contracts(contracts); AlfaGiuliaPrivateDriver.Contracts(contracts);
   check(first.usableVehicleTypes.Count==3 && second.usableVehicleTypes.Count==2,"private driver all contracts and no duplicate");
   check(first.usableVehicleTypes.Contains(AlfaGiuliaMod.VehicleTypeName),"native drop-off Contains accepts mod ID");
   check(first.maxCars==2 && first.costPerDay==123,"private driver price and capacity preserved");
   AlfaGiuliaPrivateDriver.Unload();
   check(first.usableVehicleTypes.Count==2 && first.usableVehicleTypes.Contains("other-mod"),"private driver unload retains native and third-party IDs");
   check(existing.usableVehicleTypes.Contains(AlfaGiuliaMod.VehicleTypeName),"private driver preexisting entry not owned or removed");
   AlfaGiuliaPrivateDriver.Contracts(contracts);
   check(first.usableVehicleTypes.Count==3,"private driver reload registers again");
  } finally { AlfaGiuliaPrivateDriver.Unload(); UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); UnityEngine.Object.DestroyImmediate(existing); }
 }
}}
#endif

