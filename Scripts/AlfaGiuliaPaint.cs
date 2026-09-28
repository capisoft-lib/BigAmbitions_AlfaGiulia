using System;
using System.Collections.Generic;
using UnityEngine;
namespace AlfaGiulia {
    // Cache renderer identity; reuse material lists to detect native material replacement
    // without allocating sharedMaterials arrays. State belongs to this vehicle only.
    public sealed class AlfaGiuliaPaint : MonoBehaviour {
        private sealed class Binding {
            internal MeshRenderer Renderer;
            internal readonly List<Material> Materials = new List<Material>();
            internal readonly List<Material> Previous = new List<Material>();
            internal readonly List<int> Paint = new List<int>();
            internal bool Applied;
        }
        private Binding[] bindings;
        private CarFeatures features;
        private MaterialPropertyBlock block;
        private bool quitting;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        internal int PropertyWrites { get; private set; }
        private void OnApplicationQuit() => quitting = true;
        private void OnEnable() => Invalidate();
        private void OnTransformChildrenChanged() => Invalidate();
        public void Invalidate() => bindings = null;
        public void InvalidateSlots() => Invalidate();
        private void LateUpdate() => Sync();
        private void CachePaintSlots() {
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            bindings = new Binding[renderers.Length];
            for (int i=0;i<renderers.Length;i++) bindings[i]=new Binding { Renderer=renderers[i] };
        }
        public void Sync() {
            if (quitting || GameManager.isCitySceneBeingUnloaded) return;
            if (features == null) features=GetComponentInParent<CarFeatures>(true);
            if (features == null || features.VehicleColor == null) return;
            if (bindings == null) CachePaintSlots();
            if (block == null) block=new MaterialPropertyBlock();
            var tint=features.VehicleColor.tint;
            foreach (var binding in bindings) {
                var renderer=binding.Renderer;if(renderer==null)continue;
                renderer.GetSharedMaterials(binding.Materials);
                bool changed=binding.Previous.Count!=binding.Materials.Count;
                if(!changed)for(int i=0;i<binding.Materials.Count;i++)
                    if(binding.Previous[i]!=binding.Materials[i]) { changed=true;break; }
                if(changed || !binding.Applied) {
                    binding.Paint.Clear();binding.Previous.Clear();
                    for(int i=0;i<binding.Materials.Count;i++) {
                        var mat=binding.Materials[i];binding.Previous.Add(mat);
                        if(mat!=null && mat.name.StartsWith("M_AlfaGiulia_Paint_",StringComparison.Ordinal))binding.Paint.Add(i);
                    }
                    binding.Applied=false;
                }
                foreach(int slot in binding.Paint) {
                    renderer.GetPropertyBlock(block,slot);
                    if(binding.Applied && block.HasColor(BaseColor) && block.GetColor(BaseColor)==tint)continue;
                    block.SetColor(BaseColor,tint);renderer.SetPropertyBlock(block,slot);PropertyWrites++;
                }
                binding.Applied=true;
            }
        }
    }
}
