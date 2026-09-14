#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace AlfaGiulia.Editor {
// Light guides projected onto the original lens, in metres; the two sides share
// one mirrored layout. The dark housing remains separate from powered surfaces.
internal static class AlfaGiuliaLampGeometry {
 const string Root="Assets/Mods/AlfaGiulia/Generated/";
 internal static void Apply(GameObject visual){
  var rear=visual.transform.Find("LightRear").GetComponent<MeshFilter>();
  var lens=Object.Instantiate(rear.sharedMesh);
  var housing=new GameObject("RearLampHousing");housing.transform.SetParent(visual.transform,false);
  housing.AddComponent<MeshFilter>().sharedMesh=Save(lens,"RearLampHousing");
  string mp=Root+"M_AlfaGiulia_LightHousing.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);
  if(mat==null){mat=new Material(Shader.Find("HDRP/Lit"));AssetDatabase.CreateAsset(mat,mp);}
  mat.name="M_AlfaGiulia_LightHousing";mat.SetColor("_BaseColor",new Color(.035f,.003f,.004f));
  mat.SetColor("_EmissiveColor",Color.black);mat.SetFloat("_Smoothness",.72f);
  AlfaGiuliaMaterials.ConfigureOpaque(mat);EditorUtility.SetDirty(mat);housing.AddComponent<MeshRenderer>().sharedMaterial=mat;
  var outer=new[]{new Vector2(.629f,.901f),new Vector2(.690f,.900f),new Vector2(.759f,.894f),new Vector2(.796f,.882f),new Vector2(.811f,.866f),new Vector2(.806f,.852f),new Vector2(.778f,.838f),new Vector2(.720f,.823f),new Vector2(.660f,.807f),new Vector2(.604f,.792f)};
  var inner=new[]{new Vector2(.592f,.901f),new Vector2(.531f,.902f),new Vector2(.465f,.897f),new Vector2(.401f,.889f),new Vector2(.358f,.881f)};
  var vertices=new List<Vector3>();var indices=new List<int>();
  foreach(int side in new[]{-1,1}){Ribbon(lens,outer,.012f,side,vertices,indices);Ribbon(lens,inner,.011f,side,vertices,indices);}
  rear.sharedMesh=Save(Make(vertices,indices),"LightRear");
  foreach(var pair in new[]{Tuple.Create("LightLeft",-1),Tuple.Create("LightRight",1)}){
   var f=visual.transform.Find(pair.Item1).GetComponent<MeshFilter>();
   // Retain original front indicators; place rear amber inserts inside the C.
   var kept=Filter(f.sharedMesh,p=>p.z>0);vertices=kept.vertices.ToList();indices=kept.triangles.ToList();
   Ribbon(lens,new[]{new Vector2(.637f,.858f),new Vector2(.700f,.861f),new Vector2(.767f,.862f)},.010f,pair.Item2,vertices,indices);
   f.sharedMesh=Save(Make(vertices,indices),pair.Item1);
  }
  // light_glass also contains licence-plate details: these are not reverse bulbs.
  var reverse=visual.transform.Find("LightReverse").GetComponent<MeshFilter>();
  reverse.sharedMesh=Save(Filter(reverse.sharedMesh,p=>Mathf.Abs(p.x)>.45f),"LightReverse");
 }
 static void Ribbon(Mesh lens,Vector2[] path,float width,int side,List<Vector3> vertices,List<int> indices){
  var samples=new List<Vector2>();
  for(int i=0;i<path.Length-1;i++){
   int count=Mathf.Max(2,Mathf.CeilToInt(Vector2.Distance(path[i],path[i+1])/.002f));
   var a=path[Math.Max(0,i-1)];var b=path[i];var c=path[i+1];var d=path[Math.Min(path.Length-1,i+2)];
   for(int n=0;n<count;n++){float t=n/(float)count;samples.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}
  }
  samples.Add(path[path.Length-1]);int start=vertices.Count;
  for(int i=0;i<samples.Count;i++){
   var tangent=(samples[Math.Min(i+1,samples.Count-1)]-samples[Math.Max(0,i-1)]).normalized;var normal=new Vector2(-tangent.y,tangent.x)*width*.5f;
   const int across=6;
   for(int k=0;k<=across;k++){var p=samples[i]+normal*(1-2*k/(float)across);vertices.Add(Project(lens,new Vector2(p.x*side,p.y)));}
   if(i==0)continue;
   for(int k=0;k<across;k++){int a=start+(i-1)*(across+1)+k;int b=a+across+1;indices.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
  }
 }
 static Vector3 Project(Mesh mesh,Vector2 p){
  var v=mesh.vertices;var t=mesh.triangles;float nearest=float.PositiveInfinity;
  for(int i=0;i<t.Length;i+=3){var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];float d=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(d)<1e-10f)continue;
   float u=((b.y-c.y)*(p.x-c.x)+(c.x-b.x)*(p.y-c.y))/d;float w=((c.y-a.y)*(p.x-c.x)+(a.x-c.x)*(p.y-c.y))/d;
   if(u<-.001f||w<-.001f||u+w>1.001f)continue;nearest=Mathf.Min(nearest,u*a.z+w*b.z+(1-u-w)*c.z);
  }
  if(float.IsPositiveInfinity(nearest))throw new InvalidOperationException("Lamp guide outside original lens: "+p);
  return new Vector3(p.x,p.y,nearest-.003f);
 }
 static Mesh Filter(Mesh mesh,Func<Vector3,bool> predicate){
  var v=mesh.vertices;var t=mesh.triangles;var points=new List<Vector3>();var faces=new List<int>();
  for(int i=0;i<t.Length;i+=3)if(predicate((v[t[i]]+v[t[i+1]]+v[t[i+2]])/3))for(int k=0;k<3;k++){faces.Add(points.Count);points.Add(v[t[i+k]]);}
  return Make(points,faces);
 }
 static Mesh Make(List<Vector3> v,List<int> t){var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.uv=v.Select(p=>new Vector2(p.x,p.y)).ToArray();m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();return m;}
 static Mesh Save(Mesh m,string name){m.name=name;string p=Root+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(p);if(old==null){AssetDatabase.CreateAsset(m,p);return m;}EditorUtility.CopySerialized(m,old);EditorUtility.SetDirty(old);return old;}
}
}
#endif

