#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using BAModTemplate.Editor;
using NWH.VehiclePhysics2.Modules.SpeedLimiter;
using NWH.WheelController3D;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vehicles.VehicleTypes;
using Object = UnityEngine.Object;

namespace AlfaGiulia.Editor
{
    public static class AlfaGiuliaBuild
    {
        private const string Root="Assets/Mods/AlfaGiulia";
        private static int checks;
        [Serializable] private class Palette { public Surface[] materials; }
        [Serializable] private class Surface { public string name,source; public float[] color; public float metallic, smoothness; public string texture, normal; }
        private static Palette palette;
        public static void Run()
        {
            try { Prepare(); Verify(); ModBuildCli.BuildMod("AlfaGiulia",install:false); }
            catch(Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }
        internal static void Prepare()
        {
            Directory.CreateDirectory(Root+"/Generated"); AssetDatabase.Refresh();
            palette=JsonUtility.FromJson<Palette>(File.ReadAllText(Root+"/Models/materials.json"));
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/Models/AlfaGiulia.fbx");
            importer.isReadable=true; importer.importAnimation=false; importer.SaveAndReimport();
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/AlfaGiulia.fbx"));
            var result=new GameObject("AlfaGiuliaVisual");
            try
            {
                var filters=source.GetComponentsInChildren<MeshFilter>();
                if(filters.First(f=>f.name=="WheelFL").transform.position.z<filters.First(f=>f.name=="WheelRL").transform.position.z)
                    source.transform.Rotate(0,180,0);
                foreach(var filter in filters)
                {
                    var matrix=filter.transform.localToWorldMatrix;
                    var mesh=Object.Instantiate(filter.sharedMesh);
                    var pivot=filter.name.StartsWith("Wheel")||filter.name.StartsWith("Caliper")?matrix.MultiplyPoint3x4(mesh.bounds.center):Vector3.zero;
                    string name=filter.name;
                    if(name.StartsWith("Wheel")||name.StartsWith("Caliper"))
                        name=(name.StartsWith("Wheel")?"Wheel":"Caliper")+(pivot.z>0?"F":"R")+(pivot.x<0?"L":"R");
                    if(name=="LightLeft"||name=="LightRight")
                        name=matrix.MultiplyPoint3x4(mesh.bounds.center).x<0?"LightLeft":"LightRight";
                    mesh.name=name; mesh.vertices=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)-pivot).ToArray();
                    var normals=matrix.inverse.transpose; mesh.normals=mesh.normals.Select(n=>normals.MultiplyVector(n).normalized).ToArray();
                    mesh.RecalculateBounds(); mesh.RecalculateTangents(); Save(mesh,Root+"/Generated/"+name+".asset");
                    var go=new GameObject(name); go.transform.SetParent(result.transform,false); go.transform.localPosition=pivot;
                    go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Generated/"+name+".asset");
                    var materials=filter.GetComponent<Renderer>().sharedMaterials.Select(MaterialFor).ToArray();
                    if(name.StartsWith("Light")){
                        var color=name=="LightRear"?Color.red:name=="LightLeft"||name=="LightRight"?new Color(1,.25f,0):Color.white;
                        var lamp=Material("M_AlfaGiulia_"+name,color*.10f,0,.65f);
                        lamp.SetFloat("_UseEmissiveIntensity",0);lamp.SetColor("_EmissiveColor",Color.black);
                        materials=materials.Select(m=>lamp).ToArray();
                    }
                    go.AddComponent<MeshRenderer>().sharedMaterials=materials;
                }
                // The supplied front tyres sit 1.484 mm above the rear tyre plane.
                // Align each wheel/caliper assembly without moving body geometry;
                // ConfigureWheels derives the physical anchors from these bounds.
                foreach(var wheel in result.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Wheel"))){
                    float bottom=wheel.sharedMesh.vertices.Min(v=>wheel.transform.TransformPoint(v).y);
                    wheel.transform.position-=Vector3.up*bottom;
                    var caliper=result.transform.Find(wheel.name.Replace("Wheel","Caliper"));
                    if(caliper!=null)caliper.position-=Vector3.up*bottom;
                }
                AlfaGiuliaLampGeometry.Apply(result);
                AlfaGiuliaMaterials.Repair(result); PrefabUtility.SaveAsPrefabAsset(result,AlfaGiuliaMod.VisualPath);
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(result); }
            var type=ScriptableObject.CreateInstance<VehicleType>();
            type.name="AlfaGiulia"; type.vehicleTypeName=AlfaGiuliaMod.VehicleTypeName; type.price=79900;
            // Vehicle metadata and the native physical limiter both use 307 km/h.
            type.maxFuel=58; type.maxCargoCapacity=4; type.maxSpeed=Mathf.RoundToInt(AlfaGiuliaVehicle.MaximumKph); type.enginePower=375; type.brakeForce=4500; type.turnRadius=32;
            type.damageIntensity=.3f; type.hasRadio=true; type.enclosed=true; type.countsForPersonalGoals=true;
            type.autoDestroyAfterMinutes=-1; type.canGetDirty=true; type.dirtinessTimer=1800; type.cleanByRainTimer=360;
            Save(type,AlfaGiuliaMod.TypePath);
            var manifest=AssetDatabase.LoadAssetAtPath<BAModManifest>(Root+"/ModManifest.asset");
            if(manifest==null) { manifest=ScriptableObject.CreateInstance<BAModManifest>(); AssetDatabase.CreateAsset(manifest,Root+"/ModManifest.asset"); }
            manifest.ModId="AlfaGiulia"; manifest.DisplayName="Alfa Romeo Giulia Quadrifoglio (2016)"; manifest.Author="capisoft-lib"; manifest.Version="1.0.0";
            manifest.AssetBundleName="alfagiulia.unity3d"; manifest.TargetPlatforms=ModTargetPlatforms.Windows;
            manifest.ModAssembly=AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(Root+"/AlfaGiulia.asmdef");
            manifest.LocalesFolder=AssetDatabase.LoadAssetAtPath<DefaultAsset>(Root+"/Locales"); manifest.DependenciesFolder=AssetDatabase.LoadAssetAtPath<DefaultAsset>(Root+"/Dependencies");
            EditorUtility.SetDirty(manifest);
            foreach(var path in new[]{AlfaGiuliaMod.VisualPath,AlfaGiuliaMod.TypePath}) AssetImporter.GetAtPath(path).SetAssetBundleNameAndVariant("alfagiulia","unity3d");
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        private static void Save(Object value,string path)
        {
            var old=AssetDatabase.LoadAssetAtPath<Object>(path);
            if(old==null) AssetDatabase.CreateAsset(value,path);
            else { EditorUtility.CopySerialized(value,old); EditorUtility.SetDirty(old); Object.DestroyImmediate(value); }
        }
        private static Material MaterialFor(Material source)
        {
            var data=palette.materials.FirstOrDefault(m=>m.name==source.name);
            bool paint=data?.source!=null&&data.source.Contains("2017Paint_Material");
            var mat=Material("M_AlfaGiulia_"+(paint?"Paint_":"")+source.name.Replace(" ","_"),data==null?Color.gray:new Color(data.color[0],data.color[1],data.color[2],1),data?.metallic??0,data?.smoothness??.4f);
            if(!string.IsNullOrEmpty(data?.texture))mat.SetTexture("_BaseColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Models/"+data.texture));
            if(!string.IsNullOrEmpty(data?.normal)){
                var path=Root+"/Models/"+data.normal;var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();
                mat.SetTexture("_NormalMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            AlfaGiuliaMaterials.ConfigureOpaque(mat);EditorUtility.SetDirty(mat);return mat;
        }
        private static Material Material(string name,Color color,float metallic,float smoothness)
        {
            var path=Root+"/Generated/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.name=name; mat.SetColor("_BaseColor",color); mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Smoothness",smoothness);
            AlfaGiuliaMaterials.ConfigureOpaque(mat); EditorUtility.SetDirty(mat); return mat;
        }
        private static void AddLight(Transform parent,string name,Vector3 position,Vector3 size,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=size; Object.DestroyImmediate(go.GetComponent<Collider>());
            var mat=Material("M_AlfaGiulia_"+name,color*.07f,.05f,.65f); mat.SetColor("_EmissiveColor",Color.black); mat.SetFloat("_UseEmissiveIntensity",0);
            go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        private static void Check(bool condition,string message)
        { if(!condition) throw new InvalidOperationException("AlfaGiulia check: "+message); checks++; }
        internal static void Verify()
        {
            var visual=AssetDatabase.LoadAssetAtPath<GameObject>(AlfaGiuliaMod.VisualPath);
            var type=AssetDatabase.LoadAssetAtPath<VehicleType>(AlfaGiuliaMod.TypePath);
            AlfaGiuliaDealerChecks.Verify(type,Check);
            var donor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Mods/Example-Vehicle/TurboHonza.prefab");
            var before=JsonUtility.ToJson(donor.GetComponent<CarController>().vehicleController.powertrain.engine);
            var holder=new GameObject("Validation");holder.SetActive(false);
            try {
                var root=AlfaGiuliaVehicle.Create(donor,visual,type,holder.transform);var car=root.GetComponent<CarController>();
                Check(!root.activeInHierarchy,"Awake deferred");
                Check(Mathf.Abs(root.GetComponent<Rigidbody>().mass-1620)<.1f,"mass="+root.GetComponent<Rigidbody>().mass);
                Check(car.vehicleController.powertrain.engine.maxPower==375,"375 kW");
                Check(car.GetComponent<SpeedLimiterModuleWrapper>().module.speedLimit==307,"307 km/h limiter");
                Check(car.vehicleController.powertrain.transmission.gears.Count==10,"eight forward gears");
                Check(car.vehicleController.powertrain.transmission.Output.name.Contains("Rear"),"rear wheel drive");
                var wheels=root.GetComponentsInChildren<WheelController>(true);
                Check(wheels.Length==4,"four wheels");
                foreach(var wheel in wheels){Check(wheel.wheel.visual!=null&&wheel.wheel.radius>.30f&&wheel.wheel.radius<.37f,"wheel geometry");Check(wheel.wheel.nonRotatingVisual!=null,"fixed caliper");}
                foreach(var wheel in wheels){
                    var mesh=wheel.wheel.visual.GetComponentInChildren<MeshFilter>();
                    float bottom=mesh.sharedMesh.vertices.Min(v=>root.transform.InverseTransformPoint(mesh.transform.TransformPoint(v)).y);
                    Check(Mathf.Abs(bottom)<.0001f,"tyre rests on common ground plane");
                    var wb=AlfaGiuliaVehicle.BoundsOf(mesh.GetComponents<Renderer>(),root.transform);
                    float physicalBottom=root.transform.InverseTransformPoint(wheel.transform.position).y-wheel.spring.maxLength*(2f/3f)-wheel.wheel.radius;
                    Check(Mathf.Abs(physicalBottom-wb.min.y)<.0001f,"physical tyre and visual ground plane agree at configured ride height");
                }
                var bounds=AlfaGiuliaVehicle.BoundsOf(visual.GetComponentsInChildren<Renderer>(),visual.transform);
                Check(Mathf.Abs(bounds.size.z-4.639f)<.04f,"real body length");
                var light=root.GetComponent<AlfaGiuliaLighting>();
                Check(light.Front!=null&&light.Rear!=null&&light.Reverse!=null&&light.Left!=null&&light.Right!=null,"all light channels");
                foreach(var indicator in new[]{light.Left,light.Right}){
                    var lampBounds=AlfaGiuliaVehicle.BoundsOf(new Renderer[]{indicator},root.transform);
                    Check(lampBounds.min.z< -1.8f&&lampBounds.max.z>1.5f,"front and rear indicator geometry");
                }
                var block=new MaterialPropertyBlock();
                light.ApplyStates(false,false,false,false,false,false,false);
                foreach(var lamp in new[]{light.Front,light.Rear,light.Reverse,light.Left,light.Right}){
                    lamp.GetPropertyBlock(block);Check(block.GetColor("_EmissiveColor").maxColorComponent==0,"lights off");
                }
                light.ApplyStates(true,false,true,false,false,true,false);
                light.Front.GetPropertyBlock(block);Check(Mathf.Abs(block.GetColor("_EmissiveColor").r-3)<.01f,"low beam="+block.GetColor("_EmissiveColor"));
                light.Rear.GetPropertyBlock(block);Check(Mathf.Abs(block.GetColor("_EmissiveColor").r-2f)<.01f,"tail light");
                light.Left.GetPropertyBlock(block);Check(block.GetColor("_EmissiveColor").g>0,"left amber");
                light.Right.GetPropertyBlock(block);Check(block.GetColor("_EmissiveColor").maxColorComponent==0,"right remains off");
                light.ApplyStates(false,true,true,true,true,false,true);
                light.Front.GetPropertyBlock(block);Check(Mathf.Abs(block.GetColor("_EmissiveColor").r-8)<.01f,"high beam");
                light.Rear.GetPropertyBlock(block);Check(Mathf.Abs(block.GetColor("_EmissiveColor").r-5)<.01f,"brake priority");
                light.Reverse.GetPropertyBlock(block);Check(Mathf.Abs(block.GetColor("_EmissiveColor").b-3)<.01f,"white reverse");
                light.Right.GetPropertyBlock(block);Check(block.GetColor("_EmissiveColor").g>0,"right amber");
                light.Left.GetPropertyBlock(block);Check(block.GetColor("_EmissiveColor").maxColorComponent==0,"left blink phase off");
                foreach(var wheel in wheels){
                    var position=wheel.wheel.visual.transform.position;
                    wheel.wheel.Initialize(wheel);
                    Check((wheel.wheel.visual.transform.position-position).sqrMagnitude<.000001f,"native initialization retains wheel position");
                }
                var presentation=root.GetComponent<AlfaGiuliaRoadPresentation>();
                var nativePositions=wheels.Select(w=>w.wheel.visual.transform.position).ToArray();
                var nativeRotations=wheels.Select(w=>w.wheel.visual.transform.rotation).ToArray();
                foreach(var wheel in wheels)wheel.wheel.visual.transform.Rotate(73,21,0);
                presentation.ApplyOffset(.05f);
                presentation.ApplyOffset(.05f);
                for(int i=0;i<wheels.Length;i++){
                    var carrier=wheels[i].wheel.visual.transform;
                    var mesh=carrier.GetComponentInChildren<MeshFilter>().transform;
                    Check((carrier.position-nativePositions[i]).sqrMagnitude<1e-10f,"road presentation preserves native wheel pose");
                    Check((mesh.position-carrier.position+root.transform.up*.05f).sqrMagnitude<1e-10f,"road correction stays downward during wheel rotation without accumulating");
                    carrier.rotation=nativeRotations[i];
                }
                presentation.ApplyOffset(0);
                var cloned=Object.Instantiate(root,holder.transform);
                cloned.GetComponent<AlfaGiuliaRoadPresentation>().ApplyOffset(.05f);
                Check((root.transform.Find("AlfaGiuliaVisual").localPosition).sqrMagnitude<1e-10f,"cloned presentation does not move template");
                Check(Mathf.Abs(cloned.transform.Find("AlfaGiuliaVisual").localPosition.y+.05f)<1e-6f,"presentation references survive native prefab cloning");
                Object.DestroyImmediate(cloned);
                foreach(var wheel in wheels){
                    var mesh=wheel.wheel.visual.GetComponentInChildren<MeshFilter>();
                    Check((mesh.transform.position-wheel.wheel.visual.transform.position).sqrMagnitude<1e-10f,"road correction resets for other surfaces");
                }
                var engine=car.vehicleController.powertrain.engine;
                foreach(var rpm in new[]{2500f,4000f,5500f}){
                    float torque=engine.powerCurve.Evaluate(rpm/engine.revLimiterRPM)*engine.maxPower*9549.3f/rpm;
                    Check(Mathf.Abs(torque-600)<2,"600 Nm plateau");
                }
                Check(before==JsonUtility.ToJson(donor.GetComponent<CarController>().vehicleController.powertrain.engine),"donor preserved");
                AlfaGiuliaRegressionChecks.Verify(car,Check);
                Directory.CreateDirectory("Verification");File.WriteAllText("Verification/alfagiulia.json","{\"checks\":"+checks+",\"nativeDrivingVerified\":false}");
                Debug.Log("[AlfaGiuliaValidation] PASS "+checks+" checks");
            }finally{Object.DestroyImmediate(holder);}
        }
        public static void VerifyPackage()
        {
            try
            {
                string path="Output/AlfaGiulia/AssetBundles/Windows/alfagiulia.unity3d";
                Check(new FileInfo(path).Length>100000,"nontrivial bundle");
                Check(BuildPipeline.GetCRCForAssetBundle(path,out uint crc)&&crc!=0,"bundle CRC");
                var bundle=AssetBundle.LoadFromFile(path,crc); Check(bundle!=null,"load actual packaged bundle");
                var visual=bundle.LoadAsset<GameObject>(AlfaGiuliaMod.VisualPath);
                Check(visual!=null&&bundle.LoadAsset<VehicleType>(AlfaGiuliaMod.TypePath)!=null,"packaged assets");
                Check(visual.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh!=null&&f.sharedMesh.vertexCount>0),"packaged meshes");
                Check(visual.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null)),"packaged materials");
                bundle.Unload(true); Debug.Log("[AlfaGiuliaPackage] PASS CRC="+crc); EditorApplication.Exit(0);
            }
            catch(Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }
    }
}
#endif

