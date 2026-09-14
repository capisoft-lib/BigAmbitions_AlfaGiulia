#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
namespace AlfaGiulia.Editor {
public static class AlfaGiuliaLightRender {
 static Camera camera; static RenderTexture target; static AlfaGiuliaLighting lamps;
 static int frame, state; static string folder; static Color32[] dark;
 static readonly string[] names={"off","old-tail","tail","brake","reverse","left","right"};
 public static void Run(){
  try{
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   folder=Path.GetFullPath("light-render");Directory.CreateDirectory(folder);
   var bundle=AssetBundle.LoadFromFile("Output/AlfaGiulia/AssetBundles/Windows/alfagiulia.unity3d");
   if(bundle==null)throw new Exception("Built lamp bundle unavailable");
   var visual=UnityEngine.Object.Instantiate(bundle.LoadAsset<GameObject>(AlfaGiuliaMod.VisualPath));
   AlfaGiuliaMaterials.Repair(visual);
   lamps=visual.AddComponent<AlfaGiuliaLighting>();
   var renderers=visual.GetComponentsInChildren<MeshRenderer>();
   lamps.Front=renderers.Single(r=>r.name=="LightFront");lamps.Rear=renderers.Single(r=>r.name=="LightRear");
   lamps.Reverse=renderers.Single(r=>r.name=="LightReverse");lamps.Left=renderers.Single(r=>r.name=="LightLeft");lamps.Right=renderers.Single(r=>r.name=="LightRight");
   var volume=new GameObject("Exposure").AddComponent<Volume>();volume.isGlobal=true;
   volume.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();
   var exposure=volume.sharedProfile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(10);
   var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=50000;sun.transform.rotation=Quaternion.Euler(45,25,0);
   camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,1.8f,-6.8f);
   camera.transform.LookAt(new Vector3(0,.65f,-.4f));camera.fieldOfView=38;camera.nearClipPlane=.1f;camera.farClipPlane=50;
   var hd=camera.gameObject.AddComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;hd.backgroundColorHDR=new Color(.025f,.025f,.025f);
   target=new RenderTexture(960,640,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
   SetState();EditorApplication.update+=Tick;
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
 static void SetState(){
  foreach(var material in lamps.Rear.sharedMaterials)material.SetFloat("_EmissiveExposureWeight",state==1?1:0);
  lamps.ApplyStates(false,false,state==1||state==2,state==3,state==4,state==5,state==6);
 }
 static void Tick(){
  try{
   camera.Render();if(++frame<12)return;frame=0;
   RenderTexture.active=target;var texture=new Texture2D(960,640,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,960,640),0,0);texture.Apply();
   var pixels=texture.GetPixels32();int changed=0;
   if(state==0)dark=pixels;else for(int i=0;i<pixels.Length;i++)if(Math.Abs(pixels[i].r-dark[i].r)+Math.Abs(pixels[i].g-dark[i].g)+Math.Abs(pixels[i].b-dark[i].b)>30)changed++;
   File.WriteAllBytes(Path.Combine(folder,names[state]+".png"),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
   Debug.Log("[GiuliaRender] "+names[state]+" changedPixels="+changed);
   if(state>=2&&changed<30)throw new Exception("Rear signal has no visible rendered change: "+names[state]);
   if(++state==names.Length){EditorApplication.update-=Tick;Debug.Log("[GiuliaRender] PASS "+folder);EditorApplication.Exit(0);return;}SetState();
  }catch(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
}
#endif
