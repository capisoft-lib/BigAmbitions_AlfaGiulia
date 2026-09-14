#nullable enable
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace AlfaGiulia
{
    /// <summary>
    /// Rebinds AssetBundle materials to a shader object from the running game.
    /// Unity can deserialize a bundled HDRP material with an empty/error shader;
    /// AssetService.RemapShaders deliberately skips that case, which renders magenta.
    /// </summary>
    public static class AlfaGiuliaMaterials
    {
        internal const string RuntimeShaderName = "HDRP/Lit";
        // HDRP 14 packs decal layers into renderer bits 8..15; lighting uses bits 0..7.
        public const uint DecalLayerBits = 0x0000FF00u;
        private static bool _warnedMissingShader;

        public static int Repair(GameObject root, Shader? preferredShader = null)
        {
            if (root == null)
                return 0;

            var runtimeShader = ResolveRuntimeShader(preferredShader);
            if (runtimeShader == null)
            {
                if (!_warnedMissingShader)
                {
                    _warnedMissingShader = true;
                    Debug.LogWarning("AlfaGiulia: no supported runtime shader was found; vehicle materials could not be repaired.");
                }
                return 0;
            }

            _warnedMissingShader = false;
            var repaired = 0;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                var owned = materials.Where(material => material != null &&
                    material.name.StartsWith("M_AlfaGiulia_", StringComparison.Ordinal)).ToArray();
                if (owned.Length == 0) continue;
                renderer.renderingLayerMask &= ~DecalLayerBits;
                foreach (var material in owned)
                {
                    var targetShader = runtimeShader;
                    // Only called on this mod's owned materials, never vanilla donors.
                    // Reapply after rebinding as shader remaps can reset local keywords.
                    if (material.shader == targetShader)
                    {
                        ConfigureOpaque(material);
                        continue;
                    }
                    material.shader = targetShader;
                    ConfigureOpaque(material);
                    repaired++;
                    changed = true;
                }

                if (changed)
                    renderer.sharedMaterials = materials;
            }

            if (repaired > 0)
            {
                Debug.Log("AlfaGiulia: rebound " + repaired +
                          " material slots to runtime shader '" + runtimeShader.name + "'.");
            }
            return repaired;
        }

        public static void ConfigureOpaque(Material material)
        {
            if (material == null || !material.name.StartsWith("M_AlfaGiulia_", StringComparison.Ordinal)) return;
            if (material.HasProperty("_BaseColor"))
            {
                var color = material.GetColor("_BaseColor"); color.a = 1f;
                material.SetColor("_BaseColor", color);
            }
            Set(material, "_SurfaceType", 0f);
            Set(material, "_AlphaCutoffEnable", 0f);
            Set(material, "_SupportDecals", 0f);
            Set(material, "_ReceivesSSR", 0f);
            Set(material, "_ReceivesSSRTransparent", 0f);
            Set(material, "_RefractionModel", 0f);
            if (material.name.StartsWith("M_AlfaGiulia_Light", StringComparison.Ordinal))
            {
                // These values are display-referred signal colors, not physical nits.
                // With exposure weight 1, daytime exposure extinguishes a 1-5 nit lamp.
                Set(material, "_EmissiveExposureWeight", 0f);
                Set(material, "_UseEmissiveIntensity", 0f);
                Set(material, "_AlbedoAffectEmissive", 0f);
                Set(material, "_DoubleSidedEnable", 1f);
                Set(material, "_DoubleSidedNormalMode", 0f);
            }
            material.renderQueue = (int)RenderQueue.Geometry;
            material.SetOverrideTag("RenderType", "Opaque");
            // Updating floats/keywords alone does not rebuild HDRP's depth, blend and stencil state.
            // Revalidate after every shader rebind, including the already-bound fast path.
            if (material.shader.name == RuntimeShaderName) HDMaterial.ValidateMaterial(material);
            if (material.HasProperty("_MaskMap") && material.GetTexture("_MaskMap") != null)
                material.EnableKeyword("_MASKMAP");
            if (material.HasProperty("_NormalMap") && material.GetTexture("_NormalMap") != null)
            {
                Set(material, "_NormalMapSpace", 0f);
                material.DisableKeyword("_NORMALMAP_OBJECT_SPACE");
                material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                material.EnableKeyword("_NORMALMAP");
            }
            Set(material, "_ZWrite", 1f);
            Set(material, "_SrcBlend", (float)BlendMode.One);
            Set(material, "_DstBlend", (float)BlendMode.Zero);
        }

        private static void Set(Material material, string property, float value)
        { if (material.HasProperty(property)) material.SetFloat(property, value); }

        internal static Shader? FindUsableShader(GameObject root)
        {
            if (root == null)
                return null;
            return root.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Select(material => material.shader)
                .FirstOrDefault(IsUsableShader);
        }

        private static Shader? ResolveRuntimeShader(Shader? preferredShader)
        {
            var shader = Shader.Find(RuntimeShaderName);
            if (IsUsableShader(shader))
                return shader;
            if (IsUsableShader(preferredShader))
                return preferredShader;
            shader = Shader.Find("Standard");
            return IsUsableShader(shader) ? shader : null;
        }

        private static bool IsUsableShader(Shader? shader) =>
            shader != null && shader.isSupported &&
            shader.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) < 0;
    }
}

