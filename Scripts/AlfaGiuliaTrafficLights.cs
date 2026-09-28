using GleyTrafficSystem;
using UnityEngine;
namespace AlfaGiulia {
 public sealed class AlfaGiuliaTrafficLights : MonoBehaviour {
  private VehicleLightsComponent source;
  private AlfaGiuliaLighting lamps;
  private MaterialPropertyBlock state;
  private Vector3 lastStrengths;
  private int lastFlags;
  private bool hasVisualState;
  private void OnEnable() => hasVisualState = false;
  internal void Configure(Transform visual,VehicleLightsComponent nativeLights){
   source=nativeLights;lamps=gameObject.AddComponent<AlfaGiuliaLighting>();
   hasVisualState=false;
   lamps.Front=Find(visual,"LightFront");lamps.Rear=Find(visual,"LightRear");
   lamps.Reverse=Find(visual,"LightReverse");lamps.Left=Find(visual,"LightLeft");lamps.Right=Find(visual,"LightRight");
   
  }
  static MeshRenderer Find(Transform visual,string name)=>visual.Find(name)?.GetComponent<MeshRenderer>();
  private void LateUpdate(){
   if(source==null||source.meshRenderer==null||lamps==null)return;
   if(state==null)state=new MaterialPropertyBlock();source.meshRenderer.GetPropertyBlock(state);
   float frontStrength=state.GetFloat("_FrontLightStrength");
   float backStrength=state.GetFloat("_BackLightStrength");
   float reverseStrength=state.GetFloat("_ReverseLightStrength");
   int flashState=state.GetInt("_IsBlinkerOn");
   int leftState=state.GetInt("_IsLeftBlinkerOn");
   int rightState=state.GetInt("_IsRightBlinkerOn");
   bool front=frontStrength>0;
   bool brake=backStrength>=source.brakeLightBrightness;
   bool flash=flashState==1;
   int flags=(front?1:0)|(brake?2:0)|(flash?4:0)|(leftState==1?8:0)|(rightState==1?16:0)|(reverseStrength>0?32:0);
   var strengths=new Vector3(frontStrength,backStrength,reverseStrength);
   if(hasVisualState && strengths==lastStrengths && flags==lastFlags)return;
   hasVisualState=true;lastStrengths=strengths;lastFlags=flags;
   lamps.ApplyStates(front,false,front,brake,reverseStrength>0,
    flash&&leftState==1,flash&&rightState==1);
  }
 }
}

