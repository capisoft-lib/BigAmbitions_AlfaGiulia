using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using BAModAPI;
using BAModAPI.Services;
using Buildings;
using HarmonyLib;
using Helpers;
using UnityEngine;
using Vehicles.VehicleTypes;
using Object = UnityEngine.Object;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AlfaGiulia.Editor")]
[assembly: RegisterModClass(typeof(AlfaGiulia.AlfaGiuliaMod))]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
namespace AlfaGiulia
{
    [ModEntryOnInitializationLoad]
    public sealed class AlfaGiuliaMod : IModBigAmbitions
    {
        public const string VehicleTypeName = "alfagiulia:vehicletype_alfagiulia";
        public const string DonorName = "HonzaMimic";
        public const string VisualPath = "Assets/Mods/AlfaGiulia/AlfaGiuliaVisual.prefab";
        public const string TypePath = "Assets/Mods/AlfaGiulia/AlfaGiulia.asset";
        private const string BundleKey = "AssetBundles/alfagiulia.unity3d";
        private const string PrefabPath = "Prefabs/Vehicles/PlayerVehicles/alfagiulia.prefab";
        private static VehicleType _type;
        private static GameObject _visual, _storage, _template;
        private static IModLogger _logger;
        private ModPatcher _patcher;
        private GameObject _runtime;
        public string[] RelativeAssetBundlePaths => new[] { BundleKey };

        public Task OnLoadAsync(ModContext context)
        {
            _logger = context.Logger;
            try
            {
                var bundle = AssetService.GetBundle(context.ModId, BundleKey);
                if (bundle == null) throw new InvalidOperationException("AlfaGiulia asset bundle missing.");
                _type = bundle.LoadAsset<VehicleType>(TypePath);
                _visual = bundle.LoadAsset<GameObject>(VisualPath);
                if (_type == null || _visual == null) throw new InvalidOperationException("AlfaGiulia assets missing.");
                AlfaGiuliaMaterials.Repair(_visual);
                _patcher = new ModPatcher("capisoft.alfagiulia");
                _patcher.Patch(AccessTools.DeclaredMethod(typeof(PrefabHelper), "LoadPrefab", new[] { typeof(string) }),
                    prefix: new HarmonyMethod(typeof(AlfaGiuliaMod), nameof(LoadPrefab)));

                _patcher.Patch(AccessTools.Method(typeof(ContractVehicleForSale), nameof(ContractVehicleForSale.GetSpecs)),
                    postfix: new HarmonyMethod(typeof(AlfaGiuliaMod), nameof(ShowMaximumSpeed)));
                if (!ModdingAPI.RegisterModVehicleType(_type)) throw new InvalidOperationException("AlfaGiulia ID registration failed.");
                _runtime = new GameObject("AlfaGiulia.Dealer"); Object.DontDestroyOnLoad(_runtime);
                RegisterDealers(_runtime);
                _logger.Info("[AlfaGiulia] 1.0.0: Giulia Quadrifoglio 2.9 V6, 375 kW, 600 Nm, RWD, 8-speed, 307 km/h. General US Trucks and The Hamptons Axis special order.");
                return Task.CompletedTask;
            }
            catch { OnUnloadAsync(); throw; }
        }

        internal static void RegisterDealers(GameObject host)
        {
            host.AddComponent<AlfaGiuliaVendor>().Configure("General US Trucks", "IndustryCityCarDealershipTrucks");
            host.AddComponent<AlfaGiuliaVendor>().Configure("The Hamptons Axis", "HamptonsCarDealershipLuxury");
        }

        private static bool LoadPrefab(string path, ref Object __result)
        {
            if (!string.Equals(path, PrefabPath, StringComparison.Ordinal)) return true;
            if (_template == null)
            {
                var donor = PrefabHelper.LoadPrefabAssetByName("Vehicles/PlayerVehicles/" + DonorName);
                if (donor == null) throw new InvalidOperationException("AlfaGiulia native Honza Mimic chassis unavailable.");
                if (_storage == null)
                {
                    _storage = new GameObject("AlfaGiulia.Templates");
                    _storage.SetActive(false); Object.DontDestroyOnLoad(_storage);
                }
                _template = AlfaGiuliaVehicle.Create(donor, _visual, _type, _storage.transform);
                _logger.Info("[AlfaGiulia] Native chassis prepared with independent wheels and closed doors.");
            }
            __result = _template; return false;
        }

        private static void ShowMaximumSpeed(ContractVehicleForSale __instance, List<(string key, string value)> __result)
        {
            if (__instance.VehicleName != VehicleTypeName || __result == null) return;
            for (int i = 0; i < __result.Count; i++)
                if (__result[i].key == "vehicle_max_speed")
                    __result[i] = (__result[i].key, AlfaGiuliaVehicle.MaximumKph.ToString("0.0", CultureInfo.CurrentCulture));
        }

        public Task OnUnloadAsync()
        {

            _patcher?.UnpatchAll(_patcher.Id); _patcher = null;
            if (_runtime != null) Object.Destroy(_runtime); _runtime = null;
            if (_type != null) ModdingAPI.UnregisterModVehicleType(VehicleTypeName);
            if (_storage != null) Object.Destroy(_storage);
            _storage = null; _template = null; _type = null; _visual = null;
            return Task.CompletedTask;
        }
    }
}
