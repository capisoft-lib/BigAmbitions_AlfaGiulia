#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BigAmbitions.Items;
using Blueprints;
using BusinessLayoutSets;
using Services;
using UnityEngine;

namespace AlfaGiulia
{
    /// <summary>
    /// Merges the AlfaGiulia into the native dealer contract catalogs.
    /// ContractVehicleForSale then supplies both direct purchase and vanilla warehouse delivery.
    /// </summary>
    internal sealed class AlfaGiuliaVendor : MonoBehaviour
    {
        internal string DealerContactId { get; private set; } = "General US Trucks";
        private const string TargetBusinessTypeName = "ba:businesstype_cardealership";
        private const string TargetBuildingSize = "ba:buildingsize_m";
        private const int TargetBuildingVersion = 1;
        internal string TargetLayoutName { get; private set; } = "IndustryCityCarDealershipTrucks";

        internal void Configure(string contactId, string layoutName)
        { DealerContactId = contactId; TargetLayoutName = layoutName; }
        private const float RetrySeconds = 2f;
        private const float CompatibilityRefreshSeconds = 30f;

        private object? _lastSaveGame;
        private float _nextRefreshAt;
        private bool _hasCompleteCatalog;
        private bool _layoutWarningLogged;

        private void Start()
        {

            EnsureDealerStock("initialization");
        }

        private void OnGameLoaded()
        {
            _lastSaveGame = SaveGameManager.Current;
            _hasCompleteCatalog = false;
            EnsureDealerStock("game-loaded");
        }

        private void Update()
        {
            var currentSave = (object?)SaveGameManager.Current;
            if (!ReferenceEquals(currentSave, _lastSaveGame))
            {
                _lastSaveGame = currentSave;
                _hasCompleteCatalog = false;
                _nextRefreshAt = 0f;
            }

            if (currentSave == null || Time.unscaledTime < _nextRefreshAt)
                return;

            _nextRefreshAt = Time.unscaledTime +
                             (_hasCompleteCatalog ? CompatibilityRefreshSeconds : RetrySeconds);
            EnsureDealerStock(_hasCompleteCatalog ? "compatibility-refresh" : "catalog-retry");
        }

        internal bool EnsureDealerStock(string source)
        {
            var mergedStock = new List<string>();
            var hadExplicitStock = ContractItemsForSaleService.TryGetVehiclesForContact(
                DealerContactId,
                out List<string> existingStock);

            if (hadExplicitStock && existingStock != null)
                AddUniqueRange(mergedStock, existingStock);

            var vanillaStock = GetDealerLayoutVehicles();
            AddUniqueRange(mergedStock, vanillaStock);
            AddUnique(mergedStock, AlfaGiuliaMod.VehicleTypeName);

            // Creating a mod-only override would hide all vanilla trucks if addressable layout
            // data is not ready yet. Keep the native fallback and retry instead.
            var hasOtherVehicle = mergedStock.Any(name =>
                !string.Equals(name, AlfaGiuliaMod.VehicleTypeName, StringComparison.Ordinal));
            if (!hasOtherVehicle)
            {
                if (hadExplicitStock && existingStock != null &&
                    existingStock.All(name => string.Equals(
                        name,
                        AlfaGiuliaMod.VehicleTypeName,
                        StringComparison.Ordinal)))
                {
                    ContractItemsForSaleService.RemoveContact(DealerContactId);
                }

                _hasCompleteCatalog = false;
                return false;
            }

            if (!hadExplicitStock || existingStock == null || !SameVehicleList(existingStock, mergedStock))
            {
                ContractItemsForSaleService.SetVehiclesForContact(DealerContactId, mergedStock);
                Debug.Log("AlfaGiulia: " + DealerContactId + " catalog merged from " + source +
                          " with " + mergedStock.Count + " vehicles, including the AlfaGiulia.");
            }

            _hasCompleteCatalog = vanillaStock.Count > 0;
            return true;
        }

        internal List<string> GetDealerLayoutVehicles()
        {
            var stock = new List<string>();
            try
            {
                // Wait for native loading instead of restarting the global layout scan.
                if (BusinessLayoutSetHelper.loadingLayouts) return stock;
                var layout = BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(
                    TargetBusinessTypeName,
                    new BuildingSizeInfo(TargetBuildingSize, TargetBuildingVersion),
                    TargetLayoutName.ToLowerInvariant(),
                    false);

                if (layout?.Items == null)
                    return stock;

                _layoutWarningLogged = false;

                foreach (var item in layout.Items)
                {
                    var purchaser = item?.playerItemPurchaserSettings;
                    if (purchaser == null || !purchaser.enabled || string.IsNullOrEmpty(purchaser.itemName))
                        continue;

                    var definition = ItemsGetter.GetByName(purchaser.itemName);
                    if (definition == null || string.IsNullOrEmpty(definition.vehicleType))
                        continue;
                    AddUnique(stock, definition.vehicleType);
                }
            }
            catch (Exception exception)
            {
                if (!_layoutWarningLogged)
                {
                    _layoutWarningLogged = true;
                    Debug.LogWarning("AlfaGiulia: " + DealerContactId + " layout is not ready; " +
                                     "the catalog will be retried automatically: " +
                                     exception.GetType().Name + ": " + exception.Message);
                }
            }

            return stock;
        }

        private static void AddUniqueRange(List<string> target, IEnumerable<string> values)
        {
            foreach (var value in values)
                AddUnique(target, value);
        }

        private static void AddUnique(List<string> target, string value)
        {
            if (string.IsNullOrEmpty(value) || target.Any(item =>
                    string.Equals(item, value, StringComparison.Ordinal)))
                return;
            target.Add(value);
        }

        private static bool SameVehicleList(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
            {
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        internal void RemoveFromDealerStock()
        {
            if (!ContractItemsForSaleService.TryGetVehiclesForContact(
                    DealerContactId,
                    out List<string> existingStock) || existingStock == null)
                return;

            var remaining = existingStock.Where(name => !string.Equals(
                name,
                AlfaGiuliaMod.VehicleTypeName,
                StringComparison.Ordinal)).ToList();
            if (remaining.Count == existingStock.Count)
                return;

            var vanillaStock = GetDealerLayoutVehicles();
            if (remaining.Count == 0 || SameVehicleSet(remaining, vanillaStock))
                ContractItemsForSaleService.RemoveContact(DealerContactId);
            else
                ContractItemsForSaleService.SetVehiclesForContact(DealerContactId, remaining);
        }

        private static bool SameVehicleSet(IReadOnlyCollection<string> left, IReadOnlyCollection<string> right) =>
            left.Count == right.Count && left.All(item => right.Any(other =>
                string.Equals(item, other, StringComparison.Ordinal)));

        private void OnDestroy()
        {
            RemoveFromDealerStock();
        }
    }
}

