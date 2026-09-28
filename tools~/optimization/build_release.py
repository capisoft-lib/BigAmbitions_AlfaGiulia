"""Build scoped vehicle releases in one disposable Unity 2022 project; never install/upload."""
from pathlib import Path
import argparse,json,shutil,subprocess,os,re,tempfile,hashlib,gzip
ap=argparse.ArgumentParser();ap.add_argument('--sdk',required=True);ap.add_argument('--mods',nargs='+',required=True);ap.add_argument('--prepare-only',action='store_true');ap.add_argument('--scratch');ap.add_argument('--skip',nargs='*',default=[]);args=ap.parse_args()
sdk=Path(args.sdk).resolve();mods=sdk/'Assets/Mods'
scratch=Path(args.scratch).resolve() if args.scratch else Path(tempfile.gettempdir())/'BigAmbitions'/('car-optimization-'+hashlib.sha256('|'.join(sorted(args.mods)).encode()).hexdigest()[:12])
temporary=(Path(tempfile.gettempdir())/'BigAmbitions').resolve()
if not scratch.is_relative_to(temporary):raise SystemExit('Build scratch must remain under the system temporary BigAmbitions directory.')
scratch.mkdir(parents=True,exist_ok=True)
def copy(src,dst):
 if src.is_dir():shutil.copytree(src,dst,dirs_exist_ok=True,ignore=shutil.ignore_patterns('.git','tools~','Tools~','releases','bin','obj','Library','Temp','Logs','__pycache__'))
 else:dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
for name in ['Packages','ProjectSettings']:copy(sdk/name,scratch/name)
for name in ['Editor','_BaDependencies','Settings']:copy(sdk/'Assets'/name,scratch/'Assets'/name)
# Refresh installed game references, retaining SDK-only optional assemblies.
metadata=json.loads((sdk/'UserSettings/BAModBuilder.ImportedDlls.json').read_text(encoding='utf-8-sig'))
managed=Path(metadata['installPath'])/'Big Ambitions_Data/Managed'
for dll in (scratch/'Assets/_BaDependencies/GameDlls').glob('*.dll'):
 if (managed/dll.name).exists():copy(managed/dll.name,dll)
definitions={};guids={}
for p in mods.glob('*/*.asmdef'):
 d=json.loads(p.read_text(encoding='utf-8-sig'));definitions[d['name']]=(p,d)
 meta=p.with_suffix('.asmdef.meta')
 if meta.exists():guids['GUID:'+re.search(r'guid: (\w+)',meta.read_text()).group(1)]=d['name']
selected=set(args.mods);queue=list(args.mods)
while queue:
 n=queue.pop();p,d=next((p,d) for p,d in definitions.values() if p.parent.name==n)
 for ref in d.get('references',[]):
  ref=guids.get(ref,ref)
  if ref in definitions:
   dep=definitions[ref][0].parent.name
   if dep not in selected:selected.add(dep);queue.append(dep)
for n in sorted(selected):
 src=mods/n;dst=scratch/'Assets/Mods'/n;dst.mkdir(parents=True,exist_ok=True)
 for p in src.iterdir():
  if p.name in ['Editor','.git','tools~','Tools~','releases','bin','obj']:continue
  copy(p,dst/p.name)
 if (mods/(n+'.meta')).exists():copy(mods/(n+'.meta'),dst.with_suffix('.meta'))
 if n in ['MitsubishiLancerEvolutionX','ToyotaRAV4Hybrid2023']:
  copy(src/'Editor',dst/'Editor')
# Referenced vanilla shader fixture, without its example runtime assembly.
fixture=mods/'Example-Vehicle'
for p in fixture.iterdir():
 if p.suffix not in ['.cs','.asmdef'] and not p.name.endswith(('.cs.meta','.asmdef.meta')) and p.name!='Scripts':copy(p,scratch/'Assets/Mods/Example-Vehicle'/p.name)
for p in (scratch/'Assets/Mods').rglob('*.dll.meta'):
 s=p.read_text();p.write_text(s.replace('isExplicitlyReferenced: 0','isExplicitlyReferenced: 1'))
# These three minimal historical plugin metas omit the serialized importer version;
# use Unity's complete known-good layout in the isolated build only, preserving GUIDs.
plugin_template=(mods/'Batmobile/Dependencies/Batmobile.Harmony.dll.meta').read_text()
for n in ['CityCars','SpawnACar','StreetVehicleTheft']:
 p=scratch/'Assets/Mods'/n/'Dependencies'/f'{n}.Harmony.dll.meta'
 if p.exists():
  guid=re.search(r'guid: (\w+)',p.read_text()).group(1)
  p.write_text(re.sub(r'guid: \w+','guid: '+guid,plugin_template))
for p in (scratch/'Assets').rglob('*Harmony.dll.meta'):
 s=p.read_text();guid=re.search(r'guid: (\w+)',s).group(1)
 p.write_text('fileFormatVersion: 2\nguid: '+guid+'''\nPluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 1
  validateReferences: 0
  platformData:
  - first:
      Any:
    second:
      enabled: 1
      settings: {}
  userData:
  assetBundleName:
  assetBundleVariant:
''')
# Self-contained compiler isolation: each AssemblyBuilder references dependencies,
# never Unity's cached DLL of the assembly currently being rebuilt.
packager=scratch/'Assets/Editor/ModBuilder/ModPackager.cs';s=packager.read_text(encoding='utf-8-sig')
marker='            builder.buildFinished += (outputPath, messages) =>'
s=s.replace(marker,'''            builder.excludeReferences = builder.defaultReferences.Where(r =>
                r.Replace('\\\\','/').EndsWith("/ScriptAssemblies/" + mod.AsmdefName + ".dll", StringComparison.OrdinalIgnoreCase)
                || (Path.GetFileName(r).EndsWith("Harmony.dll",StringComparison.OrdinalIgnoreCase) && !references.Contains(r,StringComparer.OrdinalIgnoreCase))).ToArray();
'''+marker)
marker='        private static bool IsBuildOnlyMetadata(DiscoveredMod mod, string path)\n        {'
assert marker in s
s=s.replace(marker,marker+'''
            var policy = Path.Combine(mod.ModFolderAssetPath,"OptimizationRoots.txt");
            if(File.Exists(policy))return !File.ReadAllLines(policy).Contains(path,StringComparer.OrdinalIgnoreCase);
''')
packager.write_text(s)
# All included mods have been selected explicitly and must compile together.
isolation=scratch/'Assets/Editor/ModBuilder/ModPlayerBuildIsolation.cs';s=isolation.read_text(encoding='utf-8-sig');start=s.index('public static bool Begin(');a=s.index('{',start);i=a+1;depth=1
while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
isolation.write_text(s[:a]+'{ return false; }'+s[i:])
p=scratch/'Assets/Editor/ModBuilder/ModBuilder.Editor.asmdef';d=json.loads(p.read_text(encoding='utf-8-sig'));d['precompiledReferences']=list(dict.fromkeys(d['precompiledReferences']+['BigAmbitions.dll','ExternalPlugins.dll','HGPlugins.dll','BigAmbitions.Tags.dll']));p.write_text(json.dumps(d,indent=2))
jobs=[]
for n in args.mods:
 data=json.loads((mods/n/'tools~/optimization/release.json').read_text());jobs.append(data)
 for archive in data.get('sourceArchives',[]):
  target=scratch/'Assets/Mods'/n/archive['target']
  if not target.exists():
   target.parent.mkdir(parents=True,exist_ok=True)
   with gzip.open(mods/n/archive['source'],'rb') as source,target.open('wb') as output:shutil.copyfileobj(source,output)
  if hashlib.sha256(target.read_bytes()).hexdigest()!=archive['sha256']:raise SystemExit('Generated source differs from its reconstruction archive: '+str(target))
 main=next(iter(data['bundles'].values()),[])
 (scratch/'Assets/Mods'/n/'OptimizationRoots.txt').write_text('\n'.join(main))
(scratch/'optimization-jobs.json').write_text(json.dumps(jobs,indent=2))
editor=scratch/'Assets/OptimizationEditor';editor.mkdir(exist_ok=True)
copy(Path(__file__).with_name('OptimizationRelease.cs'),editor/'OptimizationRelease.cs')
asm={'name':'Optimization.Editor','references':['ModBuilder.Editor','Unity.RenderPipelines.HighDefinition.Runtime'],'includePlatforms':['Editor'],'overrideReferences':True,'precompiledReferences':['Newtonsoft.Json.dll','BigAmbitions.dll','ExternalPlugins.dll','HGPlugins.dll'],'autoReferenced':True}
(editor/'Optimization.Editor.asmdef').write_text(json.dumps(asm,indent=2))
print(json.dumps({'scratch':str(scratch),'selected':sorted(selected)},indent=2),flush=True)
if args.prepare_only:raise SystemExit(0)
unity=Path('C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Unity.exe')
# The SDK's asynchronous queue owns exit; CLI beta.5 adds -quit too early.
for name in args.mods:
 if name in args.skip:continue
 marker=scratch/('release-success.'+name+'.txt')
 if marker.exists():marker.unlink()
 environment=dict(os.environ,OPTIMIZATION_MOD=name)
 result=subprocess.run([str(unity),'-batchmode','-disable-assembly-updater','-job-worker-count','2','-projectPath',str(scratch),'-executeMethod','OptimizationRelease.Run','-logFile',str(scratch/('unity-'+name+'.log'))],env=environment,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0),timeout=1800)
 print(name,'UNITY_EXIT',result.returncode,flush=True)
 if result.returncode:raise SystemExit(result.returncode)
 if not (scratch/('release-success.'+name+'.txt')).exists():raise SystemExit('Missing release completion marker: '+name)
 # Capture checks before the next mod overwrites the working report.
 for f in ['cache-checks.txt','traffic-checks.txt','emission-checks.txt','lighting-checks.txt']:
  if (scratch/f).exists():shutil.copy2(scratch/f,scratch/(name+'-'+f))
subprocess.run([os.sys.executable,str(Path(__file__).with_name('audit_release.py')),'--sdk',str(sdk),'--scratch',str(scratch)],check=True)
