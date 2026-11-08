using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
[InitializeOnLoad]
public static class AscendiaCapeClothSetup {
 const string Done="RigBackup_20261030/cape-cloth-installed.txt";
 static AscendiaCapeClothSetup(){EditorApplication.update+=Pending;}
 static void Pending(){
  if(File.Exists(Done)){EditorApplication.update-=Pending;return;}
  if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
  if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="ClimbScene")return;
  EditorApplication.update-=Pending; Install();
 }
 [MenuItem("Tools/ASCENDIA/Add Cape Cloth Physics")]
 public static void Install(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  var player=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Player");
  var visual=player?player.transform.Find("PlayerVisual"):null;
  if(!visual){Debug.LogError("Player/PlayerVisual not found.");return;}
  Directory.CreateDirectory("RigBackup_20261030");
  string backup="RigBackup_20261030/ClimbScene_before_cloth_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
  if(!EditorSceneManager.SaveScene(scene,backup,true))return;
  Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Install cape cloth");
  try {
   var result=Configure(visual.gameObject);
   EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);
   File.WriteAllText(Done,result+"\nSave scene to retain installation.");
   Debug.Log("ASCENDIA_CAPE_CLOTH: "+result);
  }catch(Exception ex){Undo.RevertAllDownToGroup(group);Debug.LogException(ex);}
 }
 public static string Configure(GameObject visual){
  var animator=visual.GetComponent<Animator>();
  if(!animator||!animator.isHuman)throw new Exception("Valid humanoid required.");
  var smr=visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name=="Cape");
  if(!smr)throw new Exception("Cape mesh not found.");
  if(smr.GetComponent<Cloth>())return "Cape already has Cloth; existing settings preserved.";
  var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest)??animator.GetBoneTransform(HumanBodyBones.Chest);
  var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
  var head=animator.GetBoneTransform(HumanBodyBones.Head);
  var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
  if(!chest||!hips||!head||!foot)throw new Exception("Required body bones missing.");
  float size=Vector3.Distance(head.position,foot.position);
  var cols=new List<CapsuleCollider>();
  AddCapsule(hips,chest,size*.105f,"Torso",cols);
  AddCapsule(chest,head,size*.08f,"UpperTorso",cols);
  foreach(bool left in new[]{true,false}){
   var upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
   var lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
   var hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
   var thigh=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
   var knee=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
   AddCapsule(upper,lower,size*.042f,"UpperArm",cols);
   AddCapsule(lower,hand,size*.033f,"Forearm",cols);
   AddCapsule(thigh,knee,size*.065f,"Thigh",cols);
  }
  var cloth=Undo.AddComponent<Cloth>(smr.gameObject);
  Undo.RecordObject(cloth,"Configure cape cloth");Undo.RecordObject(smr,"Keep cloth updated");
  cloth.useGravity=true;cloth.stretchingStiffness=1f;cloth.bendingStiffness=.45f;
  cloth.damping=.4f;cloth.friction=.35f;cloth.useTethers=true;
  cloth.enableContinuousCollision=true;cloth.clothSolverFrequency=120f;
  cloth.worldVelocityScale=.5f;cloth.worldAccelerationScale=.15f;
  cloth.externalAcceleration=Vector3.zero;cloth.randomAcceleration=Vector3.zero;
  cloth.capsuleColliders=cols.ToArray();
  var vertices=cloth.vertices;
  if(vertices.Length==0)throw new Exception("Cloth has no particles.");
  // Cloth particle indices can differ from mesh indices; use cloth.vertices directly.
  var ys=vertices.Select(v=>visual.transform.InverseTransformPoint(smr.transform.TransformPoint(v)).y).ToArray();
  float top=ys.Max(),bottom=ys.Min(),height=top-bottom;
  if(height<.01f)throw new Exception("Unexpected cape height.");
  var coefficients=cloth.coefficients;
  int pinned=0;
  float scale=Mathf.Abs(visual.transform.lossyScale.y);
  for(int i=0;i<coefficients.Length;i++){
   float depth=(top-ys[i])/height;
   float freedom=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.18f,1f,depth));
   coefficients[i].maxDistance=freedom*size*.75f*.55f;
   coefficients[i].collisionSphereDistance=0f;
   if(coefficients[i].maxDistance==0f)pinned++;
  }
  if(pinned==0||pinned==coefficients.Length)throw new Exception("Invalid pin distribution.");
  cloth.coefficients=coefficients;smr.updateWhenOffscreen=true;
  Undo.AddComponent<CapeClothMotionReset>(smr.gameObject);
  PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
  return "particles="+coefficients.Length+", pinned="+pinned+", free="+(coefficients.Length-pinned)+", capsule colliders="+cols.Count;
 }
 static void AddCapsule(Transform start,Transform end,float worldRadius,string label,List<CapsuleCollider> list){
  if(!start||!end)throw new Exception("Missing limb for "+label);
  var go=new GameObject("CapeCollision_"+label);Undo.RegisterCreatedObjectUndo(go,"Create cloth collision");
  go.layer=2; // Ignore Raycast: do not intercept the player's wall detection.
  go.transform.SetParent(start,false);
  go.transform.position=(start.position+end.position)*.5f;
  go.transform.rotation=Quaternion.FromToRotation(Vector3.up,(end.position-start.position).normalized);
  var c=Undo.AddComponent<CapsuleCollider>(go);
  float scale=Mathf.Abs(go.transform.lossyScale.y);
  c.direction=1;c.center=Vector3.zero;c.radius=worldRadius/scale;
  c.height=Vector3.Distance(start.position,end.position)/scale+2f*c.radius;
  c.isTrigger=true; // Cloth references these explicitly; they must not block movement.
  list.Add(c);
 }
}


