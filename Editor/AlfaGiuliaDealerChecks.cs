#if UNITY_EDITOR
using System;
using System.Linq;
using BAModAPI;
using BusinessLayoutSets;
using Services;
using UnityEngine;
using Vehicles.VehicleTypes;
namespace AlfaGiulia.Editor {
internal static class AlfaGiuliaDealerChecks {
 internal static void Verify(VehicleType type,Action<bool,string> check) {
  var host=new GameObject("Dealer checks");host.SetActive(false);
  bool loading=BusinessLayoutSetHelper.loadingLayouts;
  try {
   check(ModdingAPI.RegisterModVehicleType(type),"native registration");
   check(VehicleTypeHelper.GetVehicleType(AlfaGiuliaMod.VehicleTypeName)==type,"native lookup");
   BusinessLayoutSetHelper.loadingLayouts=true;
   AlfaGiuliaMod.RegisterDealers(host);
   var vendors=host.GetComponents<AlfaGiuliaVendor>();
   check(vendors.Length==2,"two dealers");
   check(vendors.Any(v=>v.DealerContactId=="The Hamptons Axis"&&v.TargetLayoutName=="HamptonsCarDealershipLuxury"),"Hamptons mapping");
   foreach(var vendor in vendors){
    var id=vendor.DealerContactId;
    vendor.EnsureDealerStock("empty");
    check(!ContractItemsForSaleService.TryGetVehiclesForContact(id,out _),"native fallback while loading");
    ContractItemsForSaleService.SetVehiclesForContact(id,new[]{"native-car","other-mod-car"});
    vendor.EnsureDealerStock("seed");vendor.EnsureDealerStock("repeat");
    check(ContractItemsForSaleService.TryGetVehiclesForContact(id,out var stock)&&stock.SequenceEqual(new[]{"native-car","other-mod-car",AlfaGiuliaMod.VehicleTypeName}),"preserve stock without duplicates");
    vendor.RemoveFromDealerStock();
    check(ContractItemsForSaleService.TryGetVehiclesForContact(id,out stock)&&stock.SequenceEqual(new[]{"native-car","other-mod-car"}),"unload preserves other vehicles");
    ContractItemsForSaleService.RemoveContact(id);
   }
   check(ModdingAPI.UnregisterModVehicleType(AlfaGiuliaMod.VehicleTypeName),"native unregister");
  }finally{UnityEngine.Object.DestroyImmediate(host);ModdingAPI.UnregisterModVehicleType(AlfaGiuliaMod.VehicleTypeName);BusinessLayoutSetHelper.loadingLayouts=loading;}
 }
}}
#endif
