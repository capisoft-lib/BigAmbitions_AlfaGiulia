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
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Version == bundled.GetName().Version).ToArray();
            var assembly = loaded.FirstOrDefault(a => a.GetName().Name == "StreetVehicleTheft.Harmony") ??
                loaded.FirstOrDefault(a => a.GetName().Name == "0Harmony") ??
                loaded.FirstOrDefault(a => a.GetName().Name == "SpawnACar.Harmony") ??
                loaded.FirstOrDefault(a => a.GetName().Name == "CityCars.Harmony") ??
                loaded.FirstOrDefault(a => a != typeof(Harmony).Assembly && a.GetType("HarmonyLib.Harmony", false) != null) ?? typeof(Harmony).Assembly;
            var type = assembly.GetType("HarmonyLib.Harmony", true);
            _methodType = assembly.GetType("HarmonyLib.HarmonyMethod", true);
            _instance = Activator.CreateInstance(type, new object[] { id });
            _patch = type.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            _unpatch = type.GetMethod("UnpatchAll", new[] { typeof(string) });
        }

        internal void Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            _patch.Invoke(_instance, new[] { original, Convert(prefix), Convert(postfix), null, null });
        }

        private object Convert(HarmonyMethod method) => method == null ? null :
            Activator.CreateInstance(_methodType, new object[] { method.method });

        internal void UnpatchAll(string id) => _unpatch.Invoke(_instance, new object[] { id });
    }
}


