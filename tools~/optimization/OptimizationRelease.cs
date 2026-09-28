using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using BAModTemplate.Editor;
using Object=UnityEngine.Object;

public static class OptimizationRelease {
    static JArray jobs;
    static int completed;
    static bool failed;
    static readonly List<string> results=new List<string>();
    public static void Run() {
        try {
            File.WriteAllText("build-progress.txt","");
            if(File.Exists("release-success.txt"))File.Delete("release-success.txt");
            jobs=JArray.Parse(File.ReadAllText("optimization-jobs.json"));
            string requested=Environment.GetEnvironmentVariable("OPTIMIZATION_MOD");
            if(!string.IsNullOrEmpty(requested))jobs=new JArray(jobs.Where(j=>(string)j["mod"]==requested));
            foreach(JObject job in jobs) {
                string id=(string)job["mod"],root="Assets/Mods/"+id;
                if((bool)job["prepareModel"]) {
                    var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(id+".Editor."+id+"Build")).First(t=>t!=null);
                    type.GetMethod("Prepare",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
                }
                // Clear old folder/source labels. Only runtime roots are explicit assets.
                foreach(string guid in AssetDatabase.FindAssets("",new[]{root})) {
                    var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    if(importer!=null && !string.IsNullOrEmpty(importer.assetBundleName))importer.SetAssetBundleNameAndVariant("","");
                }
                DeduplicateTextures(root);
                foreach(JProperty bundle in ((JObject)job["bundles"]).Properties())foreach(string path in bundle.Value.Values<string>()) {
                    if(AssetDatabase.LoadMainAssetAtPath(path)==null)throw new Exception("Missing root "+path);
                    var name=bundle.Name;int dot=name.LastIndexOf('.');
                    AssetImporter.GetAtPath(path).SetAssetBundleNameAndVariant(name.Substring(0,dot),name.Substring(dot+1));
                }
                var manifest=AssetDatabase.LoadAssetAtPath<BAModManifest>(root+"/ModManifest.asset");
                manifest.Version=(string)job["version"];EditorUtility.SetDirty(manifest);
            }
            AssetDatabase.SaveAssets();
            CheckVisualCaches();
            CheckEmissionCaches();
            CheckLightingSlots();
            CheckTrafficOwnership();
            ModPackager.JobChanged+=Changed;
            var mods=ModDiscovery.DiscoverAll();
            foreach(JObject job in jobs)ModPackager.Enqueue(mods.Single(m=>m.Manifest.ModId==(string)job["mod"]),false,false);
            var flags=BindingFlags.NonPublic|BindingFlags.Static;
            var update=typeof(EditorApplication).GetMethod("Internal_CallUpdateFunctions",flags);
            var delay=typeof(EditorApplication).GetMethod("Internal_CallDelayFunctions",flags);
            var deadline=DateTime.UtcNow.AddHours(3);
            while(completed<jobs.Count && !failed) {
                update?.Invoke(null,null);delay?.Invoke(null,null);
                if(DateTime.UtcNow>deadline)throw new Exception("Build queue timed out");
                System.Threading.Thread.Sleep(10);
            }
            if(failed)EditorApplication.Exit(1);
            Finish();
        } catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
    }
    static void Changed(BuildJob job) {
        if(!job.IsTerminal)return;
        foreach(var line in job.Log)Debug.Log(line);
        failed|=job.State==BuildState.Failed;completed++;
        results.Add(job.Mod.Manifest.ModId+": "+job.State);
        File.WriteAllLines("build-progress.txt",results);
    }
    static void Finish() {
        try {
            if(failed)throw new Exception("One or more builds failed.");
            foreach(JObject job in jobs) {
                string id=(string)job["mod"];
                foreach(var extra in ((JObject)job["bundles"]).Properties().Skip(1)) {
                    int dot=extra.Name.LastIndexOf('.');
                    var build=new AssetBundleBuild { assetBundleName=extra.Name.Substring(0,dot),assetBundleVariant=extra.Name.Substring(dot+1),assetNames=extra.Value.Values<string>().ToArray() };
                    foreach(var target in new[]{BuildTarget.StandaloneWindows64,BuildTarget.StandaloneOSX}) {
                        string dir="Output/"+id+"/AssetBundles/"+(target==BuildTarget.StandaloneWindows64?"Windows":"Mac");Directory.CreateDirectory(dir);
                        if(BuildPipeline.BuildAssetBundles(dir,new[]{build},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,target)==null)throw new Exception("Extra bundle failed "+id);
                    }
                }
                File.Copy("Assets/Mods/"+id+"/ModManifest.asset","Output/"+id+"/ModManifest.asset",true);
            }
            File.WriteAllText("release-success.txt",DateTime.UtcNow.ToString("O"));
            foreach(JObject job in jobs)File.WriteAllText("release-success."+(string)job["mod"]+".txt",DateTime.UtcNow.ToString("O"));
            Debug.Log("[OptimizationRelease] PASS "+jobs.Count+" packages");EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
    }
    static void DeduplicateTextures(string root) {
        var canonical=new Dictionary<string,Texture>();var replacements=new Dictionary<Texture,Texture>();
        foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{root})) {
            string path=AssetDatabase.GUIDToAssetPath(guid);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(texture==null || importer==null || !File.Exists(path))continue;
            // Equal encoded content AND complete importer configuration, excluding identity/labels.
            var config=JObject.Parse(EditorJsonUtility.ToJson(importer));
            foreach(string key in new[]{"m_AssetBundleName","m_AssetBundleVariant","m_UserData","m_Name","m_ExternalObjects"})config.Remove(key);
            string hash=BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(path)))+config.ToString();
            if(canonical.TryGetValue(hash,out var previous))replacements[texture]=previous;else canonical[hash]=texture;
        }
        int changes=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{root})) {
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(material==null)continue;
            foreach(string property in material.GetTexturePropertyNames()) {
                var texture=material.GetTexture(property);if(texture!=null && replacements.TryGetValue(texture,out var replacement)) {material.SetTexture(property,replacement);EditorUtility.SetDirty(material);changes++;}
            }
        }
        Debug.Log("[OptimizationRelease] Texture references deduplicated "+root+": "+changes);
    }
    static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
    static void CheckVisualCaches() {
        int checks=0;
        foreach(JObject job in jobs) {
            string id=(string)job["mod"];
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(id+"."+id+"Paint")).FirstOrDefault(t=>t!=null);
            if(type==null || type.GetProperty("PropertyWrites",BindingFlags.Instance|BindingFlags.NonPublic)==null)continue;
            var root=new GameObject("OptimizationPaintTest");root.SetActive(false);
            var material=new Material(Shader.Find("HDRP/Lit")){name="M_"+id+"_Paint_Test"};
            var replacement=new Material(material);var color=ScriptableObject.CreateInstance<Data.VehicleColors.VehicleColor>();
            try {
                var features=root.AddComponent<CarFeatures>();var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;features.bodyMeshes=new Renderer[]{renderer};
                var paint=root.AddComponent(type);var sync=type.GetMethod("Sync");var count=type.GetProperty("PropertyWrites",BindingFlags.Instance|BindingFlags.NonPublic);
                color.tint=Color.black;features.SetColor(color);sync.Invoke(paint,null);
                int before=(int)count.GetValue(paint);Check(before>0,id+" initial black");
                sync.Invoke(paint,null);Check((int)count.GetValue(paint)==before,id+" unchanged write");
                var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",Color.magenta);block.SetFloat("_OptimizationMarker",17);renderer.SetPropertyBlock(block,0);
                sync.Invoke(paint,null);renderer.GetPropertyBlock(block,0);Check(block.GetColor("_BaseColor")==Color.black && block.GetFloat("_OptimizationMarker")==17,id+" native overwrite");
                renderer.sharedMaterial=replacement;renderer.SetPropertyBlock(null,0);sync.Invoke(paint,null);renderer.GetPropertyBlock(block,0);Check(!block.isEmpty && block.GetColor("_BaseColor")==Color.black,id+" replacement binding");
                block.Clear();block.SetFloat("_OptimizationMarker",19);renderer.SetPropertyBlock(block,0);
                sync.Invoke(paint,null);renderer.GetPropertyBlock(block,0);
                Check(block.HasColor("_BaseColor") && block.GetColor("_BaseColor")==Color.black && block.GetFloat("_OptimizationMarker")==19,id+" missing black override");
                checks+=5;
                var gateType=type.Assembly.GetType(id+"."+id+"TrafficVisualGate");
                if(gateType!=null) {
                    var gate=root.AddComponent(gateType);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    gateType.GetMethod("Configure",flags).Invoke(gate,new object[]{root});
                    Check(!((Behaviour)paint).enabled,id+" offscreen paint disabled");
                    block.SetColor("_BaseColor",Color.magenta);renderer.SetPropertyBlock(block,0);
                    gateType.GetMethod("VisibleNow",flags).Invoke(gate,null);renderer.GetPropertyBlock(block,0);
                    Check(((Behaviour)paint).enabled && block.GetColor("_BaseColor")==Color.black,id+" immediate visibility resync");checks+=2;
                }
            } finally {Object.DestroyImmediate(root);Object.DestroyImmediate(material);Object.DestroyImmediate(replacement);Object.DestroyImmediate(color);}
        }
        File.WriteAllText("cache-checks.txt",checks+" paint checks passed");Debug.Log("[OptimizationRelease] cache checks "+checks);
    }
    public static void VerifyOnly() {
        try { jobs=JArray.Parse(File.ReadAllText("optimization-jobs.json"));CheckVisualCaches();CheckEmissionCaches();CheckLightingSlots();CheckTrafficOwnership();EditorApplication.Exit(0); }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void CheckEmissionCaches() {
        int checks=0;
        foreach(JObject job in jobs) {
            string id=(string)job["mod"];
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(id+"."+id+"EmissionCache")).FirstOrDefault(t=>t!=null);
            if(type==null)continue;
            var first=new GameObject("EmissionFirst");var second=new GameObject("EmissionSecond");
            var material=new Material(Shader.Find("HDRP/Lit"));material.SetColor("_EmissiveColor",Color.white);
            try {
                var a=first.AddComponent<MeshRenderer>();var b=second.AddComponent<MeshRenderer>();a.sharedMaterial=b.sharedMaterial=material;
                var cache=Activator.CreateInstance(type,true);var other=Activator.CreateInstance(type,true);
                var write=type.GetMethod("Write",BindingFlags.Instance|BindingFlags.NonPublic);
                var block=new MaterialPropertyBlock();block.SetFloat("_OptimizationMarker",23);a.SetPropertyBlock(block,0);
                write.Invoke(cache,new object[]{a,Color.black,true,null});a.GetPropertyBlock(block,0);
                Check(block.HasColor("_EmissiveColor") && block.GetColor("_EmissiveColor")==Color.black && block.GetFloat("_OptimizationMarker")==23,id+" first off");
                block.Clear();block.SetFloat("_OptimizationMarker",29);a.SetPropertyBlock(block,0);
                write.Invoke(cache,new object[]{a,Color.black,true,null});a.GetPropertyBlock(block,0);
                Check(block.HasColor("_EmissiveColor") && block.GetColor("_EmissiveColor")==Color.black && block.GetFloat("_OptimizationMarker")==29,id+" missing off override");
                write.Invoke(other,new object[]{b,Color.red,true,null});a.GetPropertyBlock(block,0);
                Check(block.GetColor("_EmissiveColor")==Color.black && material.GetColor("_EmissiveColor")==Color.white,id+" independent instances");
                block.SetColor("_EmissiveColor",Color.green);a.SetPropertyBlock(block,0);
                write.Invoke(cache,new object[]{a,Color.black,true,null});a.GetPropertyBlock(block,0);
                Check(block.GetColor("_EmissiveColor")==Color.black,id+" native emission overwrite");checks+=4;
            } finally {Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(material);}
        }
        File.WriteAllText("emission-checks.txt",checks+" emission checks passed");
    }
    static void CheckLightingSlots() {
        int checks=0;
        foreach(JObject job in jobs) {
            string id=(string)job["mod"];
            if(!new[]{"BMWX6MG06","MGMG4XPowerElectric","NissanGTRR35","OpelComboE2018","PorscheCayenneTurboGT2024","TeslaModel3","FiatCamper"}.Contains(id))continue;
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(id+"."+id+"Lighting")).First(t=>t!=null);
            var root=new GameObject("LightingSlots");root.SetActive(false);
            var material=new Material(Shader.Find("HDRP/Lit"));material.SetColor("_EmissiveColor",Color.white);
            try {
                var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                var lighting=root.AddComponent(type);var write=type.GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic);
                write.Invoke(lighting,new object[]{renderer,Color.black});
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,0);
                Check(block.HasColor("_EmissiveColor") && block.GetColor("_EmissiveColor")==Color.black,id+" lighting first off");
                block.Clear();block.SetFloat("_OptimizationMarker",31);renderer.SetPropertyBlock(block,0);
                write.Invoke(lighting,new object[]{renderer,Color.black});renderer.GetPropertyBlock(block,0);
                Check(block.HasColor("_EmissiveColor") && block.GetColor("_EmissiveColor")==Color.black && block.GetFloat("_OptimizationMarker")==31,id+" lighting missing off");
                renderer.sharedMaterials=new[]{material,material};
                write.Invoke(lighting,new object[]{renderer,Color.red});renderer.GetPropertyBlock(block,1);
                Check(block.HasColor("_EmissiveColor") && block.GetColor("_EmissiveColor")==Color.red,id+" lighting added slot");checks+=3;
            } finally {Object.DestroyImmediate(root);Object.DestroyImmediate(material);}
        }
        File.WriteAllText("lighting-checks.txt",checks+" lighting slot checks passed");
    }
    static void CheckTrafficOwnership() {
        int checks=0;
        foreach(JObject job in jobs) {
            string id=(string)job["mod"];
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(id+"."+id+"PrivateDriver")).FirstOrDefault(t=>t!=null);
            if(type==null)continue;
            var flags=BindingFlags.Static|BindingFlags.NonPublic;
            var modType=type.Assembly.GetType(id+"."+id+"Mod");
            var donor=new GameObject((string)modType.GetField("DonorName").GetRawConstantValue());donor.SetActive(false);
            var ai=new GameObject("CachedAiTemplate");ai.SetActive(false);var vehicle=ai.AddComponent<OptimizationAiFixture>();
            var pool=ScriptableObject.CreateInstance<GleyTrafficSystem.VehiclePool>();
            var entry=new GleyTrafficSystem.CarType{vehiclePrefab=donor,nrOfVehicles=3,canBeAiDriven=true};pool.trafficCars=new[]{entry};
            try {
                type.GetField("template",flags).SetValue(null,vehicle);
                var register=type.GetMethod("RegisterTraffic",flags);var unload=type.GetMethod("Unload",flags);
                register.Invoke(null,new object[]{pool});register.Invoke(null,new object[]{pool});
                Check(pool.trafficCars.Length==2 && entry.nrOfVehicles==2 && pool.trafficCars.Sum(c=>c.nrOfVehicles)==3,id+" traffic double registration budget");
                unload.Invoke(null,null);unload.Invoke(null,null);
                Check(pool.trafficCars.Length==1 && entry.nrOfVehicles==3,id+" traffic double unload restoration");
                entry.nrOfVehicles=1;register.Invoke(null,new object[]{pool});
                Check(pool.trafficCars.Length==1 && entry.nrOfVehicles==1,id+" exhausted donor budget");checks+=3;
            } finally {type.GetMethod("Unload",flags).Invoke(null,null);Object.DestroyImmediate(pool);Object.DestroyImmediate(ai);Object.DestroyImmediate(donor);}
        }
        File.WriteAllText("traffic-checks.txt",checks+" traffic ownership checks passed");
    }
}
// Registration tests need only a cached component identity, not native authoring
// validation, which requires the complete traffic prefab's front trigger hierarchy.
public sealed class OptimizationAiFixture : GleyTrafficSystem.VehicleComponent {
    private void OnValidate() {}
}
