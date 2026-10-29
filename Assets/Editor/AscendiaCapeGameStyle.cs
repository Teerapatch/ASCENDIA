using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
[InitializeOnLoad]
public static class AscendiaCapeGameStyle {
 const string Done="RigBackup_20261030/cape-game-style-v1.txt";
 static AscendiaCapeGameStyle(){EditorApplication.update+=Pending;}
 static void Pending(){
  if(File.Exists(Done)){EditorApplication.update-=Pending;return;}
  if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
  if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="ClimbScene")return;
  EditorApplication.update-=Pending;Install();
 }
 [MenuItem("Tools/ASCENDIA/Apply Game Style Cape")]
 public static void Install(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  var player=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Player");
  var visual=player?player.transform.Find("PlayerVisual"):null;if(!visual)return;
  Directory.CreateDirectory("RigBackup_20261030");
  if(!EditorSceneManager.SaveScene(scene,"RigBackup_20261030/ClimbScene_before_cape_style_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true))return;
  Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Style cape movement");
  try{string result=Apply(visual.gameObject);EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);File.WriteAllText(Done,result);Debug.Log("ASCENDIA_CAPE_STYLE: "+result);}
  catch(Exception e){Undo.RevertAllDownToGroup(group);Debug.LogException(e);}
 }
 public static string Apply(GameObject visual){
  var cloth=visual.GetComponentsInChildren<Cloth>(true).FirstOrDefault(c=>c.name=="Cape");
  var animator=visual.GetComponent<Animator>();if(!cloth||!animator)throw new Exception("Cape Cloth and Animator required.");
  float size=Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.Head).position,animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
  var ys=cloth.vertices.Select(v=>visual.transform.InverseTransformPoint(cloth.transform.TransformPoint(v)).y).ToArray();
  float top=ys.Max(),height=top-ys.Min();if(height<=0)throw new Exception("Invalid cloth height.");
  Undo.RecordObject(cloth,"Make cape hold its authored silhouette");
  var coefficients=cloth.coefficients;int changed=0;
  for(int i=0;i<coefficients.Length;i++){
   float depth=(top-ys[i])/height;
   // Keep the accepted collar/upper-back constraints, blend smoothly into a firmer hem.
   if(depth<=.35f||coefficients[i].maxDistance==0)continue;
   float baseline=size*.75f*.7f*Mathf.Sqrt(Mathf.InverseLerp(.045f,1f,depth));
   float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1f,depth));
   coefficients[i].maxDistance=Mathf.Lerp(baseline,size*.12f,blend);changed++;
  }
  cloth.coefficients=coefficients;
  cloth.stretchingStiffness=1f;cloth.bendingStiffness=.85f;cloth.damping=.65f;
  cloth.useGravity=false;cloth.externalAcceleration=new Vector3(0,-1.5f,0);
  cloth.randomAcceleration=Vector3.zero;cloth.worldVelocityScale=.2f;cloth.worldAccelerationScale=.06f;
  cloth.enableContinuousCollision=true;cloth.useTethers=true;
  PrefabUtility.RecordPrefabInstancePropertyModifications(cloth);
  return "Game style: lower particles adjusted="+changed+", bending=0.85, damping=0.65, downward acceleration=1.5, hem max distance="+(size*.12f).ToString("F3")+". Upper constraints and collision references preserved.";
 }
}
