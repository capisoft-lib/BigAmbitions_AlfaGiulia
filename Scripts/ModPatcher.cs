using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace AlfaGiulia
{
    // Reuse only the bundled Harmony version; a generic 0Harmony may be HarmonyX.
    internal sealed class ModPatcher
    {
        private readonly object _instance;
        private readonly Type _methodType;
        private readonly MethodInfo _patch, _unpatch;
        internal string Id { get; }
        internal string Provider => _instance.GetType().Assembly.GetName().Name;

        internal ModPatcher(string id)
        {
            Id = id;
            var bundled = typeof(Harmony).Assembly;
            // A renamed provider owns independent detours. Prefer an active compatible
            // provider; otherwise use a stable order INCLUDING our own assembly.
            // Excluding our own assembly makes two independent vehicle mods choose each other.
            var preferred = new[] { "StreetVehicleTheft.Harmony", "0Harmony",
                "SpawnACar.Harmony", "CityCars.Harmony", "Batmobile.Harmony",
                "LIB_BaMotorcycle.Harmony", "FiatCamper.Harmony" };
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Version == bundled.GetName().Version &&
                    a.GetType("HarmonyLib.Harmony", false) != null &&
                    a.GetType("HarmonyLib.HarmonyMethod", false) != null)
                .OrderByDescending(HasPatches)
                .ThenBy(a => { var index = Array.IndexOf(preferred, a.GetName().Name); return index < 0 ? int.MaxValue : index; })
                .ThenBy(a => a.GetName().Name, StringComparer.Ordinal)
                .First();
            var type = assembly.GetType("HarmonyLib.Harmony", true);
            _methodType = assembly.GetType("HarmonyLib.HarmonyMethod", true);
            _instance = Activator.CreateInstance(type, new object[] { id });
            _patch = type.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            _unpatch = type.GetMethod("UnpatchAll", new[] { typeof(string) });
        }

        private static bool HasPatches(Assembly assembly)
        {
            var query = assembly.GetType("HarmonyLib.Harmony").GetMethod("GetAllPatchedMethods",
                BindingFlags.Public | BindingFlags.Static);
            return query != null && ((System.Collections.IEnumerable)query.Invoke(null, null)).Cast<object>().Any();
        }

        internal void Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            _patch.Invoke(_instance, new[] { original, Convert(prefix), Convert(postfix), null, null });
        }

        private object Convert(HarmonyMethod method)
        {
            if (method == null) return null;
            var converted = Activator.CreateInstance(_methodType, new object[] { method.method });
            _methodType.GetField("priority").SetValue(converted, method.priority);
            return converted;
        }

        internal void UnpatchAll(string id)
        {
            if (id != Id) throw new ArgumentException("Cannot unpatch another mod owner.", nameof(id));
            _unpatch.Invoke(_instance, new object[] { Id });
        }
    }
}


