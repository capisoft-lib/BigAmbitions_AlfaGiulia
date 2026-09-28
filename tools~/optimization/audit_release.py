"""Read built DLL/bundle identities and promote only a completely audited package."""
from pathlib import Path
import argparse,json,hashlib,re,shutil,datetime
import UnityPy,dnfile
ap=argparse.ArgumentParser();ap.add_argument('--sdk',required=True);ap.add_argument('--scratch',required=True);ap.add_argument('--no-promote',action='store_true');ap.add_argument('--only',nargs='*');a=ap.parse_args()
sdk=Path(a.sdk).resolve();scratch=Path(a.scratch).resolve();jobs=json.loads((scratch/'optimization-jobs.json').read_text())
if a.only:jobs=[j for j in jobs if j['mod'] in a.only]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
reports=[]
for job in jobs:
 n=job['mod'];out=scratch/'Output'/n;src=scratch/'Assets/Mods'/n
 report={'mod':n,'version':job['version'],'bundles':[],'dlls':[],'sourceHashes':{str(p.relative_to(src)):sha(p) for p in (src/'Scripts').glob('*.cs')}}
 assert out.is_dir(),n+' package missing'
 for p in out.rglob('*'):
  if not p.is_file():continue
  assert p.suffix.lower() not in ['.pdb','.mdb','.cs','.ps1','.py','.glb','.fbx','.zip'],str(p)+' source/debug data packaged'
  raw=p.read_bytes()
  assert b'C:\\Users\\' not in raw and 'C:\\Users\\'.encode('utf-16-le') not in raw,str(p)+' user path leak'
 for p in out.glob('*.dll'):
  pe=dnfile.dnPE(str(p));asm=pe.net.mdtables.Assembly.rows[0];version=f'{asm.MajorVersion}.{asm.MinorVersion}.{asm.BuildNumber}.{asm.RevisionNumber}'
  assert version==job['version']+'.0',(n,version,job['version'])
  types={str(t.TypeName) for t in pe.net.mdtables.TypeDef}
  for code in (src/'Scripts').glob('*.cs'):
   if 'internal sealed class '+n+'TrafficVisualGate' in code.read_text(encoding='utf-8-sig'):assert n+'VisibilityRelay' in types,n+' stale traffic DLL'
  report['dlls'].append({'file':p.name,'sha256':sha(p),'version':version});pe.close()
 assert report['dlls'],n+' no runtime DLL'
 for platform in ['Windows','Mac']:
  # Preserve the mod's existing platform contract (some test variants are Windows only).
  mask=int(re.search(r'  TargetPlatforms: (\d+)',(src/'ModManifest.asset').read_text()).group(1))
  if not mask&(1 if platform=='Windows' else 2):continue
  for bundle,roots in job['bundles'].items():
   p=out/'AssetBundles'/platform/bundle;assert p.is_file() and p.stat().st_size>80,str(p)
   env=UnityPy.load(str(p));actual=set(env.container)
   assert actual=={x.lower() for x in roots},(n,platform,'roots mismatch',actual,roots)
   meshes=[];textures=[]
   for o in env.objects:
    if o.type.name=='Mesh':
     d=o.read();meshes.append({'name':d.m_Name,'vertices':d.m_VertexData.m_VertexCount,'triangles':sum(x.indexCount//3 for x in d.m_SubMeshes if x.topology==0)})
    elif o.type.name=='Texture2D':
     d=o.read();raw=d.get_image_data();textures.append({'name':d.m_Name,'bytes':len(raw),'key':hashlib.sha256(raw).hexdigest()+f':{d.m_Width}:{d.m_Height}:{d.m_TextureFormat}:{d.m_MipCount}:{d.m_ColorSpace}:{d.m_TextureSettings}'})
   seen={};duplicates=[]
   for texture in textures:
    if texture['key'] in seen:duplicates.append([seen[texture['key']],texture['name'],texture['bytes']])
    else:seen[texture['key']]=texture['name']
   report['bundles'].append({'platform':platform,'file':bundle,'sha256':sha(p),'bytes':p.stat().st_size,'roots':sorted(actual),'meshes':meshes,'textureCount':len(textures),'duplicateTextures':duplicates})
   del env
 reports.append(report)
 print(n,job['version'],'verified',[(b['platform'],round(b['bytes']/1e6,2),len(b['meshes']),len(b['duplicateTextures'])) for b in report['bundles']],flush=True)
(scratch/'package-audit.json').write_text(json.dumps(reports,indent=2))
if a.no_promote:raise SystemExit(0)
# Promotion keeps the preceding deliverable recoverable; no live game installation.
stamp=datetime.datetime.now().strftime('%Y%m%d-%H%M%S');backup=scratch/'previous-output'/stamp
for job,report in zip(jobs,reports):
 n=job['mod'];source=scratch/'Assets/Mods'/n;destination=sdk/'Assets/Mods'/n
 # Verify current source did not change while the build was running.
 for rel,h in report['sourceHashes'].items():assert sha(destination/rel)==h,n+' source changed since compilation: '+rel
for job,report in zip(jobs,reports):
 n=job['mod'];source=scratch/'Assets/Mods'/n;destination=sdk/'Assets/Mods'/n;out=sdk/'Output'/n
 if out.exists():backup.mkdir(parents=True,exist_ok=True);shutil.move(str(out),str(backup/n))
 shutil.copytree(scratch/'Output'/n,out)
 for p in source.rglob('*'):
  if not p.is_file():continue
  rel=p.relative_to(source)
  if p.name=='OptimizationRoots.txt.meta':continue
  # Keep generated materials, prefabs, model/type metadata and new script GUIDs.
  if rel.parts[0]=='Generated' or (len(rel.parts)==1 and p.suffix in ['.prefab','.asset']) or (p.suffix=='.meta' and not (destination/rel).exists()):
   target=destination/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
 evidence=destination/'tools~/optimization';evidence.mkdir(parents=True,exist_ok=True)
 (evidence/'package-audit.json').write_text(json.dumps(report,indent=2))
 for f in ['cache-checks.txt','traffic-checks.txt','emission-checks.txt','lighting-checks.txt']:
  result=scratch/(n+'-'+f)
  if result.exists():shutil.copy2(result,evidence/f)
 description=destination/'releases'/job['version']/'full-description.md'
 if description.exists():(out/'README.md').write_text('# '+n+' '+job['version']+'\n\n'+description.read_text(encoding='utf-8-sig'),encoding='utf-8')
 print('PROMOTED',n,flush=True)
