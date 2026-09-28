using System;
using System.Collections.Generic;
using UnityEngine;
namespace AlfaGiulia {
    // Writes only owned channels, preserving unrelated native and paint overrides.
    internal sealed class AlfaGiuliaEmissionCache {
        private sealed class Entry { internal bool Applied; internal readonly List<Material> Materials=new List<Material>(); }
        private readonly Dictionary<Renderer,Entry> entries=new Dictionary<Renderer,Entry>();
        private readonly HashSet<Material> prepared=new HashSet<Material>();
        private readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        private static readonly int Emission=Shader.PropertyToID("_EmissiveColor");
        private static readonly int Ldr=Shader.PropertyToID("_EmissiveColorLDR");
        internal void Write(Renderer renderer,Color color,bool ldr,Action<Material> prepare=null) {
            if(renderer==null)return;
            if(!entries.TryGetValue(renderer,out var entry))entries.Add(renderer,entry=new Entry());
            renderer.GetSharedMaterials(entry.Materials);
            if(prepare!=null)foreach(var material in entry.Materials)
                if(material!=null && prepared.Add(material))prepare(material);
            Apply(renderer,-1,color,ldr,entry.Applied);
            for(int i=0;i<entry.Materials.Count;i++)Apply(renderer,i,color,ldr,entry.Applied);
            entry.Applied=true;
        }
        private void Apply(Renderer renderer,int slot,Color color,bool ldr,bool initialized) {
            if(slot<0)renderer.GetPropertyBlock(block);else renderer.GetPropertyBlock(block,slot);
            if(initialized && block.HasColor(Emission) && block.GetColor(Emission)==color && (!ldr || (block.HasColor(Ldr) && block.GetColor(Ldr)==color)))return;
            block.SetColor(Emission,color);if(ldr)block.SetColor(Ldr,color);
            if(slot<0)renderer.SetPropertyBlock(block);else renderer.SetPropertyBlock(block,slot);
        }
    }
}
