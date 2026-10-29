using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
[InitializeOnLoad]
public static class AscendiaClimbingIKSetup {
 const string Done="RigBackup_20261030/ClimbingIK/installed-v1.txt";
 const string Folder="Assets/_kin/Animation/";
 static AscendiaClimbingIKSetup(){EditorApplication.update+=Pending;}
 static void Pending(){if(File.Exists(Done)){EditorApplication.update-=Pending;return;}if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="ClimbScene")return;EditorApplication.update-=Pending;Install();}
 public static string ConfigureController(){
  var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"PlayerLocomotion.controller");
  var clip=AssetDatabase.LoadAllAssetsAtPath(Folder+"Ch14_nonPBR@Climbing Up Wall.fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
  if(!controller||!clip||!clip.humanMotion)throw new Exception("Humanoid climbing clip and PlayerLocomotion are required.");
  Undo.RegisterCompleteObjectUndo(controller,"Add climbing animation");
  if(!controller.parameters.Any(p=>p.name=="IsClimbing"))controller.AddParameter("IsClimbing",AnimatorControllerParameterType.Bool);
  if(!controller.parameters.Any(p=>p.name=="ClimbRate"))controller.AddParameter("ClimbRate",AnimatorControllerParameterType.Float);
  var layers=controller.layers;layers[0].iKPass=true;controller.layers=layers;
  var sm=layers[0].stateMachine;Undo.RegisterCompleteObjectUndo(sm,"Add climbing state");
  var locomotion=sm.states.First(s=>s.state.name=="Locomotion").state;
  var climb=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Climbing");if(!climb)climb=sm.AddState("Climbing",new Vector3(650,100,0));
  climb.motion=clip;climb.tag="Climbing";climb.speedParameter="ClimbRate";climb.speedParameterActive=true;
  if(!locomotion.transitions.Any(t=>t.destinationState==climb)){var t=locomotion.AddTransition(climb);t.hasExitTime=false;t.duration=.2f;t.AddCondition(AnimatorConditionMode.If,0,"IsClimbing");}
  if(!climb.transitions.Any(t=>t.destinationState==locomotion)){var t=climb.AddTransition(locomotion);t.hasExitTime=false;t.duration=.2f;t.AddCondition(AnimatorConditionMode.IfNot,0,"IsClimbing");}
  EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();return "Climbing clip="+clip.name+", Humanoid="+clip.humanMotion+", IK Pass enabled; original locomotion retained.";
 }
 [MenuItem("Tools/ASCENDIA/Install Climbing IK Prototype")]
 public static void Install(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var player=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Player");var visual=player?player.transform.Find("PlayerVisual"):null;if(!visual)return;
  Directory.CreateDirectory("RigBackup_20261030/ClimbingIK");
  if(!EditorSceneManager.SaveScene(scene,"RigBackup_20261030/ClimbingIK/scene_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true))return;
  string report=ConfigureController();
  Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Attach climbing IK prototype");
  var ik=visual.GetComponent<ClimbingContactIK>();if(!ik)ik=Undo.AddComponent<ClimbingContactIK>(visual.gameObject);
  Undo.RecordObject(ik,"Connect climbing state");ik.state=player.GetComponent<PlayerStateManager>();ik.playerRoot=player.transform;
  var climb=player.GetComponent<PlayerClimbController>();if(climb){Undo.RecordObject(climb,"Set prototype climbing pace");climb.verticalSpeed=.65f;climb.horizontalSpeed=.45f;PrefabUtility.RecordPrefabInstancePropertyModifications(climb);}
  PrefabUtility.RecordPrefabInstancePropertyModifications(ik);EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);
  BuildTestWallPrefab();File.WriteAllText(Done,report+"\nClimbing IK attached. Vertical speed=.65, horizontal=.45. Save scene. Prototype: no ledge mantle or automatic dismount.");Debug.Log("ASCENDIA_CLIMBING_IK: "+report);
 }
 public static void BuildTestWallPrefab(){
  const string path="Assets/_kin/Prefab/ClimbingIK_TestWall.prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(path))return;
  var wall=new GameObject("ClimbingIK_TestWall");
  try{
   wall.tag="Wall";var collider=wall.AddComponent<BoxCollider>();collider.size=new Vector3(6,8,.4f);
   var mesh=GameObject.CreatePrimitive(PrimitiveType.Cube);mesh.name="WallVisual";mesh.transform.SetParent(wall.transform,false);mesh.transform.localScale=collider.size;UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
   var points=new List<Transform>();
   for(int row=0;row<16;row++)for(int col=0;col<11;col++){
    var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="Hold_"+row+"_"+col;marker.transform.SetParent(wall.transform,false);marker.transform.localPosition=new Vector3((col-5)*.45f+(row%2)*.12f,-3.5f+row*.45f,-.23f);marker.transform.localScale=Vector3.one*.075f;UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());points.Add(marker.transform);
   }
   wall.AddComponent<ClimbingSurface>().holds=points.ToArray();PrefabUtility.SaveAsPrefabAsset(wall,path);
  }finally{UnityEngine.Object.DestroyImmediate(wall);}
 }
 [MenuItem("Tools/ASCENDIA/Create Climbing Test Wall Ahead")]
 public static void CreateTestWall(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)return;BuildTestWallPrefab();
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var player=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Player");if(!player)return;
  var wall=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_kin/Prefab/ClimbingIK_TestWall.prefab"),scene);Undo.RegisterCreatedObjectUndo(wall,"Create test wall");
  // Existing climbing movement is along world X/Y, so the prototype wall faces -Z.
  wall.transform.position=new Vector3(player.transform.position.x,4,player.transform.position.z+3);
  var climb=player.GetComponent<PlayerClimbController>();if(climb&&climb.environmentContainer)Undo.SetTransformParent(wall.transform,climb.environmentContainer,"Attach to moving cliff");
  Selection.activeGameObject=wall;EditorSceneManager.MarkSceneDirty(scene);
 }
}
