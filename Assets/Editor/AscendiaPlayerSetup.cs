using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
[InitializeOnLoad]
public static class AscendiaPlayerSetup {
 const string Done="RigBackup_20261029/Integration/installed.txt";
 static AscendiaPlayerSetup(){EditorApplication.update+=Pending;}
 static void Pending(){if(File.Exists(Done)){EditorApplication.update-=Pending;return;} if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return; if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="ClimbScene")return; EditorApplication.update-=Pending; Install();}
 [MenuItem("Tools/ASCENDIA/Install Player Visual and Blend Tree")]
 public static void Install(){
 if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("Stop Play mode before installing the player visual.");return;}
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
 var player=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Player" && o.GetComponent<PlayerWalkController>());
 if(!player){Debug.LogError("Player with PlayerWalkController was not found in the active scene.");return;}
 const string controllerPath="Assets/_kin/Animation/PlayerLocomotion.controller";
 var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
 if(!controller){
 controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
 controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
 var sm=controller.layers[0].stateMachine;var state=sm.AddState("Locomotion");sm.defaultState=state;
 var tree=new BlendTree{name="Idle Walk Run",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);
 var idle=AssetDatabase.LoadAllAssetsAtPath("Assets/_kin/Animation/Ch14_nonPBR@Idle.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
 tree.AddChild(idle,0f);tree.AddChild(idle,1.4f);tree.AddChild(idle,3.5f);state.motion=tree;EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
 }
 Directory.CreateDirectory("RigBackup_20261029/Integration");
 if(!EditorSceneManager.SaveScene(scene,"RigBackup_20261029/Integration/ClimbScene_before_visual.unity",true)){Debug.LogError("Could not back up the active scene; installation stopped.");return;}
 Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Install ASCENDIA Player Visual");
 var visual=player.transform.Find("PlayerVisual")?.gameObject;
 if(!visual){
 visual=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Player_Clean");
 if(!visual){visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_kin/Prefab/Player/Player_Clean.fbx"),scene);Undo.RegisterCreatedObjectUndo(visual,"Create player visual");}
 Undo.SetTransformParent(visual.transform,player.transform,"Attach player visual");Undo.RecordObject(visual,"Rename visual");visual.name="PlayerVisual";
 }
 Undo.RecordObject(visual.transform,"Align player visual");visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;
 var cc=player.GetComponent<CharacterController>();float bottom=cc?cc.center.y-cc.height/2f:-1f;
 var renderers=visual.GetComponentsInChildren<SkinnedMeshRenderer>();
 if(renderers.Length>0){float lowest=renderers.Min(r=>r.bounds.min.y);visual.transform.position+=Vector3.up*(player.transform.TransformPoint(new Vector3(0,bottom,0)).y-lowest);}
 var animator=visual.GetComponent<Animator>();Undo.RecordObject(animator,"Connect animator");animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
 var capsule=player.GetComponent<MeshRenderer>();if(capsule){Undo.RecordObject(capsule,"Hide capsule visual");capsule.enabled=false;}
 var cube=player.transform.Find("Cube");if(cube){var renderer=cube.GetComponent<MeshRenderer>();if(renderer){Undo.RecordObject(renderer,"Hide placeholder marker");renderer.enabled=false;}}
 var walk=player.GetComponent<PlayerWalkController>();Undo.RecordObject(walk,"Set locomotion speeds");walk.walkSpeed=1.4f;walk.runSpeed=3.5f;
 var bridge=player.GetComponent<PlayerAnimationBridge>();if(!bridge)bridge=Undo.AddComponent<PlayerAnimationBridge>(player);Undo.RecordObject(bridge,"Connect animation bridge");bridge.animator=animator;
 foreach(var obj in new Object[]{visual.transform,animator,walk,bridge})PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
 EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);Selection.activeGameObject=player;
 Directory.CreateDirectory(Path.GetDirectoryName(Done));File.WriteAllText(Done,"Installed on active ClimbScene. Scene remains unsaved to preserve existing user edits. Speed thresholds: 0, 1.4, 3.5. Walk/run use Idle placeholders.");
 Debug.Log("ASCENDIA: Player visual connected. Save scene to keep changes. Replace the 1.4 and 3.5 motions in PlayerLocomotion > Locomotion with Walk and Run.");
 }
}

