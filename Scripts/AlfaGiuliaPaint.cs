using UnityEngine;
namespace AlfaGiulia {
    // Only the original Paint material is body paint. The coloured atlas also
    // contains badges, lamps, calipers and cabin details and must stay untouched.
    public sealed class AlfaGiuliaPaint : MonoBehaviour {
        private CarFeatures features;
        private MaterialPropertyBlock block;
        private void LateUpdate()=>Sync();
        public void Sync(){
            if(features==null)features=GetComponentInParent<CarFeatures>(true);
            if(features==null||features.VehicleColor==null)return;
            if(block==null)block=new MaterialPropertyBlock();
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true)){
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++){
                    if(!materials[i].name.StartsWith("M_AlfaGiulia_Paint_"))continue;
                    renderer.GetPropertyBlock(block,i);
                    block.SetColor("_BaseColor",features.VehicleColor.tint);
                    renderer.SetPropertyBlock(block,i);
                }
            }
        }
    }
}
