import bpy, math, json, sys, argparse
from pathlib import Path
from mathutils import Vector, Matrix
repo=Path('/Volumes/Mac Data/Git/PootleFramework/Pootle_Unity')
sources=Path('/tmp/codex-remote-attachments/01a1bbaf-b7d1-75d3-8464-dc088e4faaa1/717FA1A5-17C8-4800-9FE1-EADC8E3DDB1F')
out=repo/'Assets/_EXPERIMENTAL/Riggle/Demo/Characters/Quadruped Trials'
preview=repo/'Builds/Quadruped Trials'; preview.mkdir(parents=True,exist_ok=True)
parser=argparse.ArgumentParser()
parser.add_argument('--source');parser.add_argument('--label');parser.add_argument('--yaw',type=float,default=0)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
if bool(args.source)!=bool(args.label):parser.error('--source and --label must be supplied together')
models=[(Path(args.source),args.label,args.yaw)] if args.source else [(sources/'1-Pink_Cartoon_Horse_Toy.glb','Pink Horse',72),(sources/'2-Plastic_Toy_Fawn_Figurine.glb','Toy Fawn',0)]
for file,label,yaw in models:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.gltf(filepath=str(file))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
 low=Vector([min(v[i] for v in points) for i in range(3)]); high=Vector([max(v[i] for v in points) for i in range(3)])
 center=(low+high)*.5; center.z=low.z
 scale=1.8/(high.z-low.z)
 for obj in meshes:
  for v in obj.data.vertices:v.co=(obj.matrix_world@v.co-center)*scale
  obj.matrix_world.identity()
  if yaw:
   rotation=Matrix.Rotation(math.radians(yaw),4,"Z")
   for v in obj.data.vertices:v.co=rotation@v.co
 dest=out/label; dest.mkdir(parents=True,exist_ok=True)
 for mat in bpy.data.materials:
  if not mat.use_nodes:continue
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE' and node.image:
    image=node.image; image.filepath_raw=str(dest/(image.name.replace('/','_')+'.png')); image.file_format='PNG'; image.save()
 bpy.ops.object.select_all(action='DESELECT')
 for obj in meshes:obj.select_set(True)
 bpy.context.view_layer.objects.active=meshes[0]
 bpy.ops.export_scene.fbx(filepath=str(dest/(label+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,path_mode='COPY',embed_textures=False,add_leaf_bones=False,bake_anim=False)
 scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=12
 scene.render.resolution_x=700; scene.render.resolution_y=700; scene.render.resolution_percentage=100
 scene.world=bpy.data.worlds.new('World');scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.14,.14,.14,1)
 for pos,power,size in [((3,-4,5),450,5),((-3,2,4),350,4)]:
  light=bpy.data.lights.new('Softbox','AREA');light.energy=power;light.shape='DISK';light.size=size;go=bpy.data.objects.new('Softbox',light);scene.collection.objects.link(go);go.location=pos;go.rotation_euler=(Vector((0,0,.9))-go.location).to_track_quat('-Z','Y').to_euler()
 camera=bpy.data.cameras.new('Camera');camera.type='ORTHO';camera.ortho_scale=2.5;go=bpy.data.objects.new('Camera',camera);scene.collection.objects.link(go);scene.camera=go
 for name,pos in [('threequarter',(3,-4,2.7)),('side',(4,0,.95)),('front',(0,-4,.95))]:
  go.location=pos;go.rotation_euler=(Vector((0,0,.9))-go.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(preview/(label+'-'+name+'.png'));bpy.ops.render.render(write_still=True)
 print('MODEL',label,[(o.name,len(o.data.vertices),[list(v) for v in o.bound_box]) for o in meshes])
