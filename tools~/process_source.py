"""Import the attributed GLB, bake transforms and preserve its PBR textures."""
import bpy, bmesh, json, sys
from pathlib import Path
from mathutils import Vector, Matrix
source, output, evidence = [Path(p).resolve() for p in sys.argv[sys.argv.index('--')+1:]]
output.mkdir(parents=True,exist_ok=True); evidence.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and len(o.data.materials)]
points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
lo=Vector([min(v[i] for v in points) for i in range(3)]); hi=Vector([max(v[i] for v in points) for i in range(3)])
scale=4.639/(hi.y-lo.y)
# Keep the wheelbase center, rather than the asymmetrical body bounding box, at origin.
matrix=Matrix.Diagonal((scale,scale,scale,1)); matrix.translation=Vector((0,0,-lo.z*scale))
for o in objects:
 world=o.matrix_world.copy(); o.parent=None; o.matrix_world=Matrix.Identity(4)
 o.data.transform(matrix@world)
for o in list(bpy.data.objects):
 if o not in objects: bpy.data.objects.remove(o,do_unlink=True)
groups={}
for o in objects:
 name=o.data.materials[0].name
 center=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
 if 'Wheel1A' in name: key='Wheel'+('F' if center.y<0 else 'R')+('L' if center.x<0 else 'R')
 elif 'Calliper' in name or name=='phong5': key='Caliper'+('F' if center.y<0 else 'R')+('L' if center.x<0 else 'R')
 elif name=='red_glass': key='LightRear'
 elif name=='orange_glass': key='Indicators'
 elif name=='light_glass': key='Lenses'
 elif 'Paint_' in name: key='Body'
 else: key='Trim'
 groups.setdefault(key,[]).append(o)
def join(parts,name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts: o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join(); parts[0].name=name
 return parts[0]
for key,parts in groups.items(): groups[key]=join(parts,key)
def subset(obj,name,predicate):
 mesh=obj.data.copy(); bm=bmesh.new(); bm.from_mesh(mesh)
 bmesh.ops.delete(bm,geom=[f for f in bm.faces if not predicate(f.calc_center_median())],context='FACES')
 bm.to_mesh(mesh); bm.free(); mesh.update()
 if not len(mesh.polygons): return None
 child=bpy.data.objects.new(name,mesh); bpy.context.scene.collection.objects.link(child); return child
ind=groups.pop('Indicators')
subset(ind,'LightLeft',lambda p:p.x<0); subset(ind,'LightRight',lambda p:p.x>0)
bpy.data.objects.remove(ind,do_unlink=True)
lenses=groups.pop('Lenses')
subset(lenses,'LightFront',lambda p:p.y<-1)
subset(lenses,'LightReverse',lambda p:p.y>1)
subset(lenses,'MirrorLenses',lambda p:abs(p.y)<=1)
bpy.data.objects.remove(lenses,do_unlink=True)
# Rear indicators are separate narrow amber inserts in the upper outer taillights.
rear=groups['LightRear']
for side,sign in [('Left',-1),('Right',1)]:
 part=subset(rear,'RearIndicator'+side,lambda p:sign*p.x>.45 and p.y>1.9 and p.z>.70)
 if part:
  # Cut a narrow strip in the lens rather than covering the whole outer tail lamp.
  bm=bmesh.new();bm.from_mesh(part.data)
  for height,normal in [(.785,(0,0,1)),(.815,(0,0,-1))]:
   bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,
    plane_co=(0,0,height),plane_no=normal,clear_inner=True,clear_outer=False)
  bm.to_mesh(part.data);bm.free()
  for v in part.data.vertices: v.co.y+=.002
  join([bpy.data.objects['Light'+side],part],'Light'+side)
# Bumper reflectors are passive reflectors, not tail/brake lights.
reflectors=subset(rear,'RearReflectors',lambda p:p.z<.6)
if reflectors:join([groups['Trim'],reflectors],'Trim')
bm=bmesh.new();bm.from_mesh(rear.data)
bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.calc_center_median().z<.6],context='FACES')
bm.to_mesh(rear.data);bm.free()
parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in parts:
 if o.name.startswith(('Wheel','Caliper')):
  pts=[v.co for v in o.data.vertices]; mn=Vector([min(v[i] for v in pts) for i in range(3)]); mx=Vector([max(v[i] for v in pts) for i in range(3)])
  pivot=(mn+mx)/2
  for v in o.data.vertices:v.co-=pivot
  o.location=pivot
 # Keep all supplied details; model is already suitable for one player car.
materials=[]
for idx,mat in enumerate([m for m in bpy.data.materials if m.users]):
 oldname=mat.name; mat.name='Surface%02d'%idx
 p=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
 color=list(p.inputs['Base Color'].default_value) if p else list(mat.diffuse_color)
 data=dict(name=mat.name,source=oldname,color=color,metallic=p.inputs['Metallic'].default_value if p else 0,smoothness=1-(p.inputs['Roughness'].default_value if p else .5))
 for channel,socket in [('texture','Base Color'),('normal','Normal')]:
  links=list(p.inputs[socket].links) if p else []
  node=links[0].from_node if links else None
  if node and node.type=='NORMAL_MAP':
   links=list(node.inputs['Color'].links); node=links[0].from_node if links else None
  if node and node.type=='TEX_IMAGE' and node.image:
   filename=mat.name+'_'+channel+'.png'; node.image.filepath_raw=str(output/filename); node.image.file_format='PNG'; node.image.save(); data[channel]=filename
 # Glass stays dark and opaque for readable exterior and correct game depth rendering.
 if 'Window_Material' in oldname: data['color']=[.025,.045,.06,1]; data['metallic']=.2; data['smoothness']=.9
 materials.append(data)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(output/'AlfaGiulia.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
(output/'materials.json').write_text(json.dumps(dict(materials=materials),indent=2))
import runpy
saved_args=sys.argv
sys.argv=['extract_materials.py',str(source),str(output)]
runpy.run_path(str(Path(__file__).with_name('extract_materials.py')),run_name='__main__')
sys.argv=saved_args
stats=[dict(name=o.name,triangles=sum(len(p.vertices)-2 for p in o.data.polygons),pivot=list(o.location)) for o in parts]
(evidence/'prepared.json').write_text(json.dumps(dict(parts=stats,scale=scale,length=4.639),indent=2))
scene=bpy.context.scene; scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=2
scene.render.resolution_x=1200;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.6
target=Vector((0,0,.65))
def aim(o):o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
for name,pos,power,size in [('Key',(-3,-4,7),1600,6),('Fill',(4,-2,4),1100,5),('Rim',(0,4,5),1900,4)]:
 light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size
 o=bpy.data.objects.new(name,light);scene.collection.objects.link(o);o.location=pos;aim(o)
camera=bpy.data.objects.new('Camera',bpy.data.cameras.new('Camera'));scene.collection.objects.link(camera);camera.data.type='ORTHO';camera.data.ortho_scale=5.8;scene.camera=camera
bpy.ops.wm.save_as_mainfile(filepath=str(evidence/'AlfaGiulia-prepared.blend'))
for name,pos in [('front',(-6,-8,4)),('rear',(6,8,3.5))]:
 camera.location=pos;aim(camera);scene.render.filepath=str(evidence/(name+'.png'));bpy.ops.render.render(write_still=True)
