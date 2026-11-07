using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor.SceneManagement;

namespace Docky.RiggleTools.Editor
{
    public sealed class RiggleHelperWindow : EditorWindow
    {
        const string Layout="Assets/_EXPERIMENTAL/Riggle/Assets/Riggle/Editor/RiggleHelperWindow.uxml";
        static readonly string[] Steps={"1 Model","2 References","3 Fit","4 Bake","5 Test","6 Save"};
        [SerializeField] Riggle character;
        [SerializeField] GameObject sourceModel;
        [SerializeField] string characterName="New character";
        [SerializeField] float height=1.8f;
        [SerializeField] int step;
        [SerializeField] Riggle.Point originLandmark=Riggle.Point.LeftShoulder;
        string message="";
        VisualElement body;
        [MenuItem("Tools/Riggle/Rig Helper")]
        public static void ShowWindow()=>Open(null);
        public static void Open(Riggle rig)
        {
            var window=GetWindow<RiggleHelperWindow>("Rig Helper");window.minSize=new Vector2(460,580);
            if(rig)window.character=rig;window.Show();window.Refresh();
        }
        void OnEnable(){Undo.undoRedoPerformed+=Refresh;EditorApplication.playModeStateChanged+=PlayChanged;}
        void OnDisable(){Undo.undoRedoPerformed-=Refresh;EditorApplication.playModeStateChanged-=PlayChanged;}
        void PlayChanged(PlayModeStateChange state)=>Refresh();
        public void CreateGUI(){var tree=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout);if(tree)tree.CloneTree(rootVisualElement);Refresh();}
        public void SetCharacter(Riggle rig,int page=0){character=rig;step=Mathf.Clamp(page,0,5);Refresh();}
        public bool CanAdvance()
        {
            if(!character || character.CollectSourceMeshes().Count==0)return false;
            if(step>=3 && !character.GeneratedBones)return false;
            return true;
        }
        void Run(Action action)
        {
            try{action();if(character)EditorSceneManager.MarkSceneDirty(character.gameObject.scene);}
            catch(Exception e){message=e.GetBaseException().Message;Debug.LogWarning("Rig Helper: "+message);}
            Refresh();SceneView.RepaintAll();
        }
        Button ActionButton(string text,Action action,bool enabled=true,bool primary=false)
        {
            var button=new Button(()=>Run(action)){text=text};button.AddToClassList("action");if(primary)button.AddToClassList("primary");button.SetEnabled(enabled && !EditorApplication.isPlaying);body.Add(button);return button;
        }
        void Copy(string text){var label=new Label(text);label.AddToClassList("copy");body.Add(label);}
        void SelectOriginal(){Selection.activeGameObject=character.gameObject;EditorGUIUtility.PingObject(character);}
        void Frame()
        {
            SelectOriginal();if(character.TryGetSourceBounds(out var bounds))SceneView.lastActiveSceneView?.Frame(new Bounds(character.transform.TransformPoint(bounds.center),Vector3.Scale(bounds.size,character.transform.lossyScale)),true);
        }
        public void Refresh()
        {
            if(rootVisualElement.Q("helperRoot")==null)return;
            var nav=rootVisualElement.Q("steps");nav.Clear();
            for(int i=0;i<Steps.Length;i++)
            {
                int index=i;var button=new Button(()=>{step=index;message="";Refresh();}){text=Steps[i]};button.AddToClassList("step-button");if(i==step)button.AddToClassList("step-active");button.SetEnabled(i==0 || character);nav.Add(button);
            }
            rootVisualElement.Q<Label>("characterName").text=character?character.name+"  ·  "+character.CollectSourceMeshes().Count+" source meshes":"Choose a model or an existing Riggle character.";
            body=rootVisualElement.Q<ScrollView>("body");body.Clear();var heading=new Label(Steps[step]);heading.AddToClassList("section-title");body.Add(heading);
            if(step==0)Model();else if(character){if(step==1)References();if(step==2)Fit();if(step==3)Bake();if(step==4)Test();if(step==5)Save();}else Copy("Choose a character in step 1 first.");
            rootVisualElement.Q<Label>("message").text=message;
            var footer=rootVisualElement.Q("footer");footer.Clear();var back=new Button(()=>{step--;message="";Refresh();}){text="Back"};back.SetEnabled(step>0);footer.Add(back);
            var next=new Button(()=>{step++;message="";Refresh();}){text=step==5?"Complete":"Next · "+Steps[Mathf.Min(5,step+1)]};next.SetEnabled(step<5 && CanAdvance());footer.Add(next);
            if(EditorApplication.isPlaying){var stop=new Button(()=>EditorApplication.isPlaying=false){text="Stop preview"};footer.Add(stop);}
        }
        void Model()
        {
            Copy("Use a character already in the scene, or prepare a model asset. Preparation keeps an inactive original and creates independent static source meshes for Riggle—even when the imported model was already skinned.");
            var existing=new ObjectField("Existing character"){objectType=typeof(Riggle),allowSceneObjects=true,value=character};existing.RegisterValueChangedCallback(e=>{character=e.newValue as Riggle;Refresh();});body.Add(existing);
            var model=new ObjectField("Source model"){objectType=typeof(GameObject),allowSceneObjects=true,value=sourceModel};model.RegisterValueChangedCallback(e=>{sourceModel=e.newValue as GameObject;if(sourceModel)characterName=sourceModel.name;Refresh();});body.Add(model);
            var name=new TextField("Character name"){value=characterName};name.RegisterValueChangedCallback(e=>characterName=e.newValue);body.Add(name);
            var size=new FloatField("Height in world units"){value=height};size.RegisterValueChangedCallback(e=>height=e.newValue);body.Add(size);
            ActionButton("Use current selection",()=>{var go=Selection.activeGameObject;if(!go)throw new InvalidOperationException("Select a model or character first.");character=go.GetComponentInParent<Riggle>();if(!character){sourceModel=go;characterName=go.name;}message="Selection loaded.";});
            ActionButton("Prepare a new Riggle character",()=>{character=RiggleModelPreparation.Create(sourceModel,characterName,height);step=1;message="Source preserved. Review references before baking.";},sourceModel && !string.IsNullOrWhiteSpace(characterName),true);
        }
        void References()
        {
            Copy("Place empty children at the joints. Names such as Shoulder.L, Elbow_L, Wrist-R and LeftKnee are recognized. Both explicit sides are respected; if one is absent, it can be reflected across the pelvis in character-local X.");
            Copy("A disconnected part's origin can also supply a landmark. Choose the joint explicitly, then select its source mesh/group. Zero offset uses the pivot; landmark handles adjust the offset without moving the model. Other joints along a limb still need their own references or fitted landmarks.");
            var target=new EnumField("Part origin represents",originLandmark);target.RegisterValueChangedCallback(e=>originLandmark=(Riggle.Point)e.newValue);body.Add(target);
            ActionButton("Use selected part origin for this landmark",()=>{RiggleReferenceSetup.AssignOrigin(character,Selection.activeTransform,originLandmark);message="Assigned "+originLandmark+" to the selected part's origin. Re-bake after fitting.";});
            var refs=character.GetComponent<RiggleReferencePoints>();
            ActionButton("Scan child names",()=>message=RiggleReferenceSetup.Scan(character),true,true);
            ActionButton("Create missing reference objects · one side",()=>{RiggleReferenceSetup.CreateMissing(character,true);message="Created editable empties; the missing opposite side is inferred.";});
            ActionButton("Create missing reference objects · both sides",()=>{RiggleReferenceSetup.CreateMissing(character,false);message="Both sides can now be fitted independently.";});
            if(!refs)return;
            var active=new Toggle("Use reference objects"){value=refs.useReferences};active.RegisterValueChangedCallback(e=>Run(()=>{Undo.RecordObject(refs,"Toggle landmark references");refs.useReferences=e.newValue;}));body.Add(active);
            var infer=new Toggle("Infer a missing opposite side"){value=refs.inferMissingSide};infer.RegisterValueChangedCallback(e=>Run(()=>{Undo.RecordObject(refs,"Toggle reference mirroring");refs.inferMissingSide=e.newValue;}));body.Add(infer);
            var group=new ObjectField("Search below"){objectType=typeof(Transform),allowSceneObjects=true,value=refs.searchRoot};group.RegisterValueChangedCallback(e=>Run(()=>{var t=e.newValue as Transform;if(t && t!=character.transform && !t.IsChildOf(character.transform))throw new InvalidOperationException("Choose a child of this character.");Undo.RecordObject(refs,"Choose reference group");refs.searchRoot=t;}));body.Add(group);
            for(int i=0;i<Riggle.PointCount;i++)
            {
                var point=(Riggle.Point)i;var row=new VisualElement();row.AddToClassList("point-row");var field=new ObjectField(point.ToString()){objectType=typeof(Transform),allowSceneObjects=true,value=refs.Assigned(point)};
                field.RegisterValueChangedCallback(e=>Run(()=>{var t=e.newValue as Transform;if(t && (!RiggleReferenceSetup.IsCandidate(character,t) || t.TryGetComponent<RiggleOriginReference>(out var origin) && origin.landmark!=point))throw new InvalidOperationException("Choose an empty reference child or a part origin assigned to this landmark, outside the generated skeleton and imported source.");Undo.RecordObject(refs,"Assign reference point");refs.points[(int)point]=t;EditorUtility.SetDirty(refs);}));row.Add(field);
                var status=new Label(RiggleReferenceSetup.Status(character,point));status.AddToClassList("point-status");row.Add(status);body.Add(row);
                var assigned=refs.Assigned(point);var originRef=assigned?assigned.GetComponent<RiggleOriginReference>():null;
                if(originRef)
                {
                    var offset=new Vector3Field("Local pivot offset"){value=originRef.localOffset};
                    offset.RegisterValueChangedCallback(e=>Run(()=>{Undo.RecordObject(originRef,"Offset source origin reference");originRef.localOffset=e.newValue;EditorUtility.SetDirty(originRef);}));body.Add(offset);
                }
            }
        }
        void Fit()
        {
            Copy("Check front and side views. Move the empty objects or edit positions below. Inferred-side edits move the assigned opposite marker. Knees and elbows should sit inside the joints; head top marks the crown, not the eyes.");
            ActionButton("Frame character and show landmarks",Frame,true,true);
            AppendageSettings();
            for(int i=0;i<Riggle.PointCount;i++)
            {
                var point=(Riggle.Point)i;var value=new Vector3Field(point+" · local"){value=character.GetLocal(point)};
                value.RegisterValueChangedCallback(e=>{RiggleEditor.RecordPointUndo(character,point,"Fit reference landmark");character.SetLocal(point,e.newValue);EditorUtility.SetDirty(character);SceneView.RepaintAll();});body.Add(value);
            }
        }
        void AppendageSettings()
        {
            var extra=character.GetComponent<RiggleAppendages>();
            if(!extra)
            {
                ActionButton("Add optional ears, tail and wings",()=>{var added=Undo.AddComponent<RiggleAppendages>(character.gameObject);RiggleAppendageBuilder.Defaults(character,added);message="Choose the appendages to enable, then fit their guides. All start disabled.";});
                return;
            }
            Copy("Optional appendages · ears attach to Head, tail to Hips, wings to Upper Chest. Fit the guide paths, then re-bake. Each guide can use a source object's origin plus an offset, or a character-local position.");
            void ToggleSetting(string label,string property,bool value)
            {
                var toggle=new Toggle(label){value=value};toggle.RegisterValueChangedCallback(e=>Run(()=>{var settings=new SerializedObject(extra);settings.FindProperty(property).boolValue=e.newValue;settings.ApplyModifiedProperties();}));body.Add(toggle);
            }
            ToggleSetting("Two bendy bunny ears","bunnyEars",extra.bunnyEars);
            ToggleSetting("Long tail","longTail",extra.longTail);
            ToggleSetting("Two back wings","backWings",extra.backWings);
            ToggleSetting("Show appendage guide handles","showGuides",extra.showGuides);
            if(extra.bunnyEars)ToggleSetting("Mirror right ear from left","mirrorEars",extra.mirrorEars);
            if(extra.backWings)ToggleSetting("Mirror right wing from left","mirrorWings",extra.mirrorWings);
            var settings=new SerializedObject(extra);
            void Chain(string label,string field)
            {
                var fold=new Foldout{text=label+" · segments and guides",value=false};body.Add(fold);
                var property=settings.FindProperty(field);
                fold.Add(new PropertyField(property.FindPropertyRelative("segments"),"Bend segments"));
                fold.Add(new PropertyField(property.FindPropertyRelative("radius"),"Weighting radius / height"));
                fold.Add(new PropertyField(property.FindPropertyRelative("guides"),"Path guides · root to tip"));fold.Bind(settings);
            }
            if(extra.bunnyEars){Chain("Left ear","leftEar");if(!extra.mirrorEars)Chain("Right ear","rightEar");}
            if(extra.longTail)Chain("Tail","tail");
            if(extra.backWings){Chain("Left wing","leftWing");if(!extra.mirrorWings)Chain("Right wing","rightWing");}
            ActionButton("Reset appendage guides to character proportions",()=>{RiggleAppendageBuilder.Defaults(character,extra);message="Guide positions reset. Refit to your mesh before baking.";});
            Copy("Named Body Parts: Ear.L / Ear.R, Tail, Wing.L / Wing.R. Smooth Skin: use matching Skin Binding whole-mesh/island assignments. A mesh needs vertices along the bend to deform smoothly.");
        }
        void Bake()
        {
            Copy(character.Assembly==Riggle.AssemblyMode.SmoothSkin
                ? "Bake creates the skeleton, weights and Humanoid Avatar. Existing painted corrections and sculpts are retained when source topology matches. Build from the rest pose."
                : "Bake creates the skeleton, named-part skinning and Humanoid Avatar. Review the part scan and build from the rest pose.");
            var mode=new EnumField("Assembly mode",character.Assembly);
            mode.RegisterValueChangedCallback(e=>Run(()=>{var settings=new SerializedObject(character);settings.FindProperty("assemblyMode").enumValueIndex=(int)(Riggle.AssemblyMode)e.newValue;settings.ApplyModifiedProperties();}));body.Add(mode);
            var so=new SerializedObject(character);
            if(character.Assembly!=Riggle.AssemblyMode.SmoothSkin)
            {
                bool chunks=character.Assembly==Riggle.AssemblyMode.NamedBodyParts;
                Copy(chunks ? "Name chunks Arm.L, ArmWithHand.R, Leg.L, LegWithFoot.R, Torso, TorsoWithNeck, Head or HeadWithNeck. Each bends only within its own bone chain. Plain Arm/Leg exclude hand/foot; Torso/Head exclude neck. Named groups pass their scope to unnamed descendants."
                    : "Name mesh children Head, Riggle_Chest, UpperArm.L, etc. Named groups also bind their descendants. One material-preserving skin is built with rigid weights.");
                Copy("Head^ Hat stays separate and is parented to Head; Clear restores its original parent and pose. Exact bone names still bind rigidly in either named mode.");
                body.Add(new PropertyField(so.FindProperty("attachmentSymbol")));
                if(chunks)
                {
                    body.Add(new PropertyField(so.FindProperty("separateLimbMeshes")));
                    body.Add(new PropertyField(so.FindProperty("weighting")));
                    body.Add(new PropertyField(so.FindProperty("distributeTwist")));
                    Copy("Separate limb meshes share the skeleton and keep compact bone lists. Use a Riggle Body Part override for arbitrary names or to separate one particular chunk.");
                }
                ActionButton("Scan named parts and accessories",()=>message=RiggleRigidParts.Plan(character).Summary,true,true);
                Copy(chunks ? "Smoothing and twist distribution stay inside the allowed chain. Painted overrides and corrective shapes require Smooth Skin mode."
                    : "Rigid parts do not use smoothing, twist weights or corrective sculpting. Unmatched or hidden mesh parts are reported before baking.");
            }
            else {body.Add(new PropertyField(so.FindProperty("weighting")));body.Add(new PropertyField(so.FindProperty("distributeTwist")));}
            body.Add(new PropertyField(so.FindProperty("tryHumanoid")));body.Bind(so);
            if(character.Assembly==Riggle.AssemblyMode.NamedBodyParts) BodyPartOverrides();
            ActionButton("Bake skeleton, skin and controls",()=>
            {
                var previous=character.GeneratedBones;RiggleBuilder.Bake(character);
                if(!character.GeneratedBones || character.GeneratedBones==previous)throw new InvalidOperationException("Bake did not complete. Check the Console and reference positions.");
                if(character.BakedAvatar){ControlRig.ControlRigBuilder.Build(character.GetComponent<Animator>());RigglePrefabExporter.Save(character);}
                message=character.BakedAvatar?"Baked successfully · Humanoid and controls ready.":"Skin baked. Humanoid validation failed; refine the landmarks before using the pose tests.";
            },true,true);
            Copy(character.GeneratedBones?"Current output: "+character.BakedSources.Where(b=>b.baked).Select(b=>b.baked).Distinct().Count()+" skinned meshes · "+(character.BakedAvatar?"Humanoid valid":"generic skeleton"):"No generated skeleton yet.");
        }
        void BodyPartOverrides()
        {
            ActionButton("Add body-part settings to selected mesh / group",()=>
            {
                var selected=Selection.activeTransform;
                if(!selected || selected==character.transform || !selected.IsChildOf(character.transform) ||
                    character.GeneratedBones && selected.IsChildOf(character.GeneratedBones) ||
                    selected.GetComponentInParent<RiggleImportedSource>() || selected.GetComponentInParent<ControlRig.Rig_References>())
                    throw new InvalidOperationException("Select a source mesh or source group inside this character.");
                var part=selected.GetComponent<RiggleBodyPart>();
                if(!part)
                {
                    part=Undo.AddComponent<RiggleBodyPart>(selected.gameObject);
                    if(RiggleBodyParts.TryName(selected.name,out var kind,out var side,out var terminal,out var neck))
                    {part.kind=kind;part.side=side;part.includeHandOrFoot=terminal;part.includeNeck=neck;}
                }
                message="Body-part settings added to "+selected.name+". Re-bake to apply.";
            });
            foreach(var part in character.GetComponentsInChildren<RiggleBodyPart>(true))
            {
                if(character.GeneratedBones && part.transform.IsChildOf(character.GeneratedBones) || part.GetComponentInParent<RiggleImportedSource>())continue;
                var foldout=new Foldout{text=part.name+" · body-part override",value=true};body.Add(foldout);
                var settings=new SerializedObject(part);
                var kind=new EnumField("Kind",part.kind);kind.RegisterValueChangedCallback(e=>Run(()=>{Undo.RecordObject(part,"Choose body part kind");part.kind=(RiggleBodyPart.Kind)e.newValue;EditorUtility.SetDirty(part);}));foldout.Add(kind);
                bool limb=part.kind==RiggleBodyPart.Kind.Arm || part.kind==RiggleBodyPart.Kind.Leg;
                var fields=limb?new[]{"side","includeHandOrFoot","keepSeparateRenderer"}:
                    part.kind==RiggleBodyPart.Kind.Ear || part.kind==RiggleBodyPart.Kind.Wing?new[]{"side","keepSeparateRenderer"}:
                    part.kind==RiggleBodyPart.Kind.Tail?new[]{"keepSeparateRenderer"}:new[]{"includeNeck","keepSeparateRenderer"};
                foreach(string field in fields)
                    foldout.Add(new PropertyField(settings.FindProperty(field)));
                foldout.Bind(settings);
            }
        }
        void Test()
        {
            Copy("Inspect bends before export. Build the pose row, try the controls in Play Mode, and use the Inspector's weight/corrective brushes for local fixes.");
            ActionButton("Build / refresh visible test poses",()=>{RigglePoseGalleryBuilder.Rebuild(character);message="Pose row refreshed.";},character.BakedAvatar,true);
            var appendages=character.GetComponent<RiggleAppendageRig>();
            if(appendages && appendages.chains.Length>0)
            {
                Copy("Appendage FK controls · rotate individual controls in Play Mode, or use these edit-mode pose previews. Restore rest before rebaking. Disable Riggle Appendage Rig when animating the bones directly.");
                ActionButton("Appendages · rest pose",()=>RiggleAppendageBuilder.Pose(appendages,0));
                ActionButton("Appendages · droop / curl / spread",()=>RiggleAppendageBuilder.Pose(appendages,1));
                ActionButton("Appendages · perk / sweep / fold",()=>RiggleAppendageBuilder.Pose(appendages,2));
                foreach(var chain in appendages.chains)
                    ActionButton("Select "+chain.name+" FK controls",()=>Selection.objects=chain.controls.Select(t=>(UnityEngine.Object)t.gameObject).ToArray());
            }
            ActionButton("Add shoulder & hip corrective starters",()=>{RiggleDeformationTools.AddStarters(character);RigglePoseGalleryBuilder.Rebuild(character);message="Starter shapes and OFF/ON comparisons ready.";},character.BakedAvatar && character.Assembly==Riggle.AssemblyMode.SmoothSkin);
            ActionButton("Open weight / corrective tools in Inspector",SelectOriginal,character.GeneratedBones && character.Assembly==Riggle.AssemblyMode.SmoothSkin);
            var gallery=character.GetComponent<RigglePoseGallery>();if(gallery)
            {
                var compare=new Toggle("Show corrective OFF / ON comparison"){value=gallery.compareCorrectives};compare.RegisterValueChangedCallback(e=>Run(()=>{Undo.RecordObject(gallery,"Switch pose suite");gallery.compareCorrectives=e.newValue;gallery.ApplyVisibility();}));body.Add(compare);
            }
            ActionButton("Enter Play Mode to test controls",()=>{EditorSceneManager.SaveScene(character.gameObject.scene);EditorApplication.isPlaying=true;},character.BakedAvatar);
            if(character.GeneratedBones)ActionButton("Validate skin weights and bind poses",()=>{RiggleDiagnostics.Validate(character);message="Validation written to Console.";});
        }
        void Save()
        {
            Copy("Save a reusable skinned prefab and the authoring scene. The original import and authoring components are excluded from the runtime prefab. Keep the scene to continue fitting and painting.");
            Copy("Output: "+character.AssetFolder+"/"+character.name+"_Skinned.prefab");
            ActionButton("Save skinned prefab and scene",()=>{RiggleModelPreparation.Folder(character.AssetFolder);var prefab=RigglePrefabExporter.Save(character);EditorSceneManager.SaveScene(character.gameObject.scene);EditorGUIUtility.PingObject(prefab);message="Saved prefab and authoring scene.";},character.GeneratedBones,true);
            ActionButton("Select saved prefab",()=>{var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(character.AssetFolder+"/"+character.name+"_Skinned.prefab");if(!prefab)throw new InvalidOperationException("Save the prefab first.");Selection.activeObject=prefab;EditorGUIUtility.PingObject(prefab);});
        }
    }
}
