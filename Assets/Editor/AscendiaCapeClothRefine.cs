using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
[InitializeOnLoad]
public static class AscendiaCapeClothRefine {
 const string Done="RigBackup_20261030/cape-cloth-refined-v3.txt";
 static AscendiaCapeClothRefine(){EditorApplication.update+=Pending;}
 static void Pending(){
  if(File.Exists(Done)){EditorApplication.update-=Pending;return;}
  if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
  if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="ClimbScene")return;
  EditorApplication.update-=Pending;Install();
 }
 [MenuItem("Tools/ASCENDIA/Refine Cape Collision")]
 public static void Install(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  var player=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Player");
  var visual=player?player.transform.Find("PlayerVisual"):null;
  if(!visual)return;
  Directory.CreateDirectory("RigBackup_20261030");
  if(!EditorSceneManager.SaveScene(scene,"RigBackup_20261030/ClimbScene_before_cloth_refine_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true))return;
  Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Refine cape collision");
  try{string report=Refine(visual.gameObject);EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);File.WriteAllText(Done,report);Debug.Log("ASCENDIA_CAPE_REFINED: "+report);}
  catch(Exception e){Undo.RevertAllDownToGroup(group);Debug.LogException(e);}
 }
 public static string Refine(GameObject visual){
  var anim=visual.GetComponent<Animator>();
  var cloth=visual.GetComponentsInChildren<Cloth>(true).FirstOrDefault(c=>c.name=="Cape");
  if(!anim||!cloth)throw new Exception("Cape Cloth and Animator required");
  var head=anim.GetBoneTransform(HumanBodyBones.Head);var foot=anim.GetBoneTransform(HumanBodyBones.LeftFoot);
  float size=Vector3.Distance(head.position,foot.position);
  var ys=cloth.vertices.Select(v=>visual.transform.InverseTransformPoint(cloth.transform.TransformPoint(v)).y).ToArray();
  float top=ys.Max(),height=top-ys.Min(),worldHeight=size*.75f;
  Undo.RecordObject(cloth,"Release upper cape constraints");
  var coeff=cloth.coefficients;int pinned=0;float upperFreedom=0;
  for(int i=0;i<coeff.Length;i++){
   float depth=(top-ys[i])/height;
   float free=Mathf.InverseLerp(.045f,1f,depth);
   coeff[i].maxDistance=worldHeight*.7f*Mathf.Sqrt(free);
   if(free==0)pinned++;else if(depth<.3f)upperFreedom=Mathf.Max(upperFreedom,coeff[i].maxDistance);
  }
  if(pinned==0||pinned==coeff.Length)throw new Exception("Invalid collar anchor");
  cloth.coefficients=coeff;cloth.clothSolverFrequency=180f;cloth.bendingStiffness=.35f;cloth.friction=.2f;
  cloth.enableContinuousCollision=true;
  foreach(var c in cloth.capsuleColliders){
   if(!c)continue;
   float factor=c.name=="CapeCollision_UpperArm"?.05f:c.name=="CapeCollision_Forearm"?.04f:0f;
   if(factor==0)continue;
   Undo.RecordObject(c,"Add arm collision margin");
   float segment=Mathf.Max(0,c.height-2*c.radius);c.radius=size*factor/Mathf.Abs(c.transform.lossyScale.y);c.height=segment+2*c.radius;
   PrefabUtility.RecordPrefabInstancePropertyModifications(c);
  }
  var pairs=new List<ClothSphereColliderPair>(cloth.sphereColliders);
  foreach(bool left in new[]{true,false}){
   var hand=anim.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
   var lower=anim.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
   var child=hand.Find("CapeCollision_Hand");
   if(!child){var go=new GameObject("CapeCollision_Hand");Undo.RegisterCreatedObjectUndo(go,"Add hand cloth collider");go.layer=2;go.transform.SetParent(hand,false);child=go.transform;}
   var sphere=child.GetComponent<SphereCollider>();if(!sphere)sphere=Undo.AddComponent<SphereCollider>(child.gameObject);
   Undo.RecordObject(sphere,"Fit hand collider");sphere.isTrigger=true;
   sphere.radius=size*.045f/Mathf.Abs(child.lossyScale.x);
   sphere.center=child.InverseTransformPoint(hand.position+(hand.position-lower.position).normalized*size*.025f);
   if(!pairs.Any(p=>p.first==sphere||p.second==sphere))pairs.Add(new ClothSphereColliderPair(sphere));
  }
  cloth.sphereColliders=pairs.ToArray();
  PrefabUtility.RecordPrefabInstancePropertyModifications(cloth);
  return "particles="+coeff.Length+", pinned="+pinned+", free="+(coeff.Length-pinned)+", upper freedom="+upperFreedom.ToString("F3")+", capsules="+cloth.capsuleColliders.Length+", sphere pairs="+pairs.Count;
 }
}


