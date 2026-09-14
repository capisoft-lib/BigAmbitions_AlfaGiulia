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
// Actual Unity HDRP captures of source assets, with runtime light-state code.
// No game process, installed mod, or Workshop item is touched by this review.
public static class AlfaGiuliaLampReview {
 static Camera camera;static RenderTexture target;static AlfaGiuliaLighting lamps;
 static Light sun;static HDAdditionalLightData sunData;static int frame,state;static string folder;static GameObject visual;
 static readonly string[] Names={"rear-evening","rear-night","rear-close","brake","left","right","reverse","front-evening","ground-side","ground-rear"};
 public static void Run(){try{
  AlfaGiuliaBuild.Prepare();AlfaGiuliaBuild.Verify();
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  folder=Path.GetFullPath("lamp-review");Directory.CreateDirectory(folder);
  visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AlfaGiuliaMod.VisualPath));
  AlfaGiuliaMaterials.Repair(visual);
  foreach(var wheel in visual.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Wheel"))){
   var points=wheel.sharedMesh.vertices.Select(v=>wheel.transform.TransformPoint(v)).ToArray();
   float bottom=points.Min(v=>v.y);
   Debug.Log("[GiuliaGround] "+wheel.name+" lowestVertexY="+bottom.ToString("F6")+" metres; floorY=0");
   if(Mathf.Abs(bottom)>.0001f)throw new InvalidOperationException("Tyre not on review ground: "+wheel.name);
  }
  var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Ground at Y zero";floor.transform.localScale=new Vector3(20,1,20);
  var floorMat=new Material(Shader.Find("HDRP/Lit"));floorMat.SetColor("_BaseColor",new Color(.055f,.06f,.065f));floorMat.SetFloat("_Smoothness",.12f);HDMaterial.ValidateMaterial(floorMat);floor.GetComponent<MeshRenderer>().sharedMaterial=floorMat;
  foreach(var r in visual.GetComponentsInChildren<MeshRenderer>())for(int i=0;i<r.sharedMaterials.Length;i++)
   if(r.sharedMaterials[i].name.StartsWith("M_AlfaGiulia_Paint_")){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b,i);b.SetColor("_BaseColor",new Color(.78f,.78f,.78f));r.SetPropertyBlock(b,i);}
  lamps=visual.AddComponent<AlfaGiuliaLighting>();var rs=visual.GetComponentsInChildren<MeshRenderer>();
  lamps.Front=rs.Single(r=>r.name=="LightFront");lamps.Rear=rs.Single(r=>r.name=="LightRear");lamps.Reverse=rs.Single(r=>r.name=="LightReverse");lamps.Left=rs.Single(r=>r.name=="LightLeft");lamps.Right=rs.Single(r=>r.name=="LightRight");
  var volume=new GameObject("Review exposure").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();
  var exposure=volume.sharedProfile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(10);
  var tone=volume.sharedProfile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.Neutral);
  var bloom=volume.sharedProfile.Add<Bloom>(true);bloom.intensity.Override(.12f);bloom.threshold.Override(1);
  sun=new GameObject("Evening illumination").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,25,0);
  sunData=sun.gameObject.AddComponent<HDAdditionalLightData>();
  sun.shadows=LightShadows.Soft;
  camera=new GameObject("Review camera").AddComponent<Camera>();camera.nearClipPlane=.03f;camera.farClipPlane=50;
  var hd=camera.gameObject.AddComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;hd.backgroundColorHDR=new Color(.008f,.010f,.016f);
  target=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
  state=0;frame=0;SetState();EditorApplication.update+=Tick;
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static void SetState(){
  camera.orthographic=false;sun.transform.rotation=Quaternion.Euler(45,25,0);
  camera.transform.position=new Vector3(0,1.32f,-5.8f);camera.transform.LookAt(new Vector3(0,.80f,-1.3f));camera.fieldOfView=32;
  sunData.SetIntensity(state==1?350:4000,LightUnit.Lux);
  if(state==2){camera.transform.position=new Vector3(1.18f,1.03f,-3.35f);camera.transform.LookAt(new Vector3(.60f,.85f,-2.22f));camera.fieldOfView=29;}
  if(state==7){camera.transform.position=new Vector3(-2.7f,1.6f,5.2f);camera.transform.LookAt(new Vector3(0,.7f,1.2f));camera.fieldOfView=36;sun.transform.rotation=Quaternion.Euler(45,205,0);}
  if(state==8){camera.transform.position=new Vector3(-6,.85f,0);camera.transform.LookAt(new Vector3(0,.60f,0));camera.orthographic=true;camera.orthographicSize=1.70f;sun.transform.rotation=Quaternion.Euler(45,205,0);}
  if(state==9){camera.transform.position=new Vector3(-3.1f,1.2f,-5.4f);camera.transform.LookAt(new Vector3(0,.55f,-.65f));camera.fieldOfView=40;}
  lamps.ApplyStates(true,false,true,state==3,state==6,state==4,state==5);
 }
 static void Tick(){try{
  camera.Render();if(++frame<48)return;frame=0;
  RenderTexture.active=target;var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
  File.WriteAllBytes(Path.Combine(folder,Names[state]+".png"),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
  Debug.Log("[GiuliaLampReview] captured "+Names[state]);
  if(++state==Names.Length){EditorApplication.update-=Tick;Debug.Log("[GiuliaLampReview] PASS "+folder);EditorApplication.Exit(0);return;}SetState();
 }catch(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);EditorApplication.Exit(1);}}
}
}
#endif
