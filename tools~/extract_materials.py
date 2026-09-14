"""Read glTF factors/textures directly, including Blender's multiply-node cases."""
import json,struct,sys
from pathlib import Path
source,out=map(Path,sys.argv[1:]);f=source.open('rb');f.read(12)
n,t=struct.unpack('<II',f.read(8));g=json.loads(f.read(n));n,t=struct.unpack('<II',f.read(8));blob=f.read(n)
palette=json.loads((out/'materials.json').read_text())
for m in palette['materials']:
 candidates=[v for v in g['materials'] if v['name'].startswith(m['source'])]
 if len(candidates)!=1:raise ValueError((m['source'],len(candidates)))
 raw=candidates[0];pbr=raw.get('pbrMetallicRoughness',{})
 m['color']=pbr.get('baseColorFactor',[1,1,1,1]);m['metallic']=pbr.get('metallicFactor',1);m['smoothness']=1-pbr.get('roughnessFactor',1)
 for channel,tex in [('texture',pbr.get('baseColorTexture')),('normal',raw.get('normalTexture'))]:
  if tex is None:continue
  image=g['images'][g['textures'][tex['index']]['source']];view=g['bufferViews'][image['bufferView']]
  ext='.png' if image['mimeType']=='image/png' else '.jpg';name=m['name']+'_'+channel+ext
  start=view.get('byteOffset',0);(out/name).write_bytes(blob[start:start+view['byteLength']]);m[channel]=name
 if 'Window_Material' in raw['name']:m['color']=[.025,.045,.06,1];m['metallic']=.2;m['smoothness']=.9
(out/'materials.json').write_text(json.dumps(palette,indent=2))
