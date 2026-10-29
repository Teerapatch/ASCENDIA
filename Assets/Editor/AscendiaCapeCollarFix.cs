using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
[InitializeOnLoad]
public static class AscendiaCapeCollarFix {
 const string Done="RigBackup_20261030/cape-collar-two-sided-v1.txt";
 static AscendiaCapeCollarFix(){EditorApplication.update+=Pending;}
 static void Pending(){
  if(File.Exists(Done)){EditorApplication.update-=Pending;return;}
  if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
  if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="ClimbScene")return;
  EditorApplication.update-=Pending;Install();
 }
 [MenuItem("Tools/ASCENDIA/Fix Cape Collar and Inside")]
 public static void Install(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var player=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Player");
  var visual=player?player.transform.Find("PlayerVisual"):null;if(!visual)return;
  Directory.CreateDirectory("RigBackup_20261030");
  if(!EditorSceneManager.SaveScene(scene,"RigBackup_20261030/ClimbScene_before_collar_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true))return;
  Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Fix cape collar and inside");
  try{string report=Apply(visual.gameObject,true);EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);File.WriteAllText(Done,report);Debug.Log("ASCENDIA_CAPE_COLLAR: "+report);}
  catch(Exception e){Undo.RevertAllDownToGroup(group);Debug.LogException(e);}
 }
 public static string Apply(GameObject visual,bool assignMaterial){
  var cloth=visual.GetComponentsInChildren<Cloth>(true).First(c=>c.name=="Cape");var smr=cloth.GetComponent<SkinnedMeshRenderer>();var anim=visual.GetComponent<Animator>();
  var chest=anim.GetBoneTransform(HumanBodyBones.UpperChest)??anim.GetBoneTransform(HumanBodyBones.Chest);
  var neck=anim.GetBoneTransform(HumanBodyBones.Neck);var head=anim.GetBoneTransform(HumanBodyBones.Head);var foot=anim.GetBoneTransform(HumanBodyBones.LeftFoot);
  float size=Vector3.Distance(head.position,foot.position);
  var ys=cloth.vertices.Select(v=>visual.transform.InverseTransformPoint(cloth.transform.TransformPoint(v)).y).ToArray();float top=ys.Max(),height=top-ys.Min();
  var coeff=cloth.coefficients;Undo.RecordObject(cloth,"Anchor collar");int pinned=0;
  for(int i=0;i<coeff.Length;i++){
   float depth=(top-ys[i])/height;
   if(depth<=.10f)coeff[i].maxDistance=0;
   else if(depth<.20f)coeff[i].maxDistance=Mathf.Min(coeff[i].maxDistance,size*.07f*Mathf.InverseLerp(.10f,.20f,depth));
   if(coeff[i].maxDistance==0)pinned++;
  }
  cloth.coefficients=coeff;
  var upper=cloth.capsuleColliders.FirstOrDefault(c=>c&&c.name=="CapeCollision_UpperTorso");
  if(upper&&neck){
   Undo.RecordObject(upper.transform,"Fit upper torso below collar");Undo.RecordObject(upper,"Reduce neck collision pressure");
   Vector3 end=Vector3.Lerp(chest.position,neck.position,.75f);
   upper.transform.position=(chest.position+end)*.5f;
   upper.transform.rotation=Quaternion.FromToRotation(Vector3.up,(end-chest.position).normalized);
   float scale=Mathf.Abs(upper.transform.lossyScale.y);upper.radius=size*.055f/scale;
   upper.height=Vector3.Distance(chest.position,end)/scale+2*upper.radius;
   PrefabUtility.RecordPrefabInstancePropertyModifications(upper.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(upper);
  }
  if(assignMaterial){
   const string folder="Assets/_kin/Prefab/Player/";
   var shader=AssetDatabase.LoadAssetAtPath<Shader>(folder+"SG_CapeTwoSided.shadergraph");if(!shader)throw new Exception("Two-sided cape shader not imported yet.");
   var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"Cape_TwoSided.mat");
   if(!material){material=new Material(smr.sharedMaterial);material.name="Cape_TwoSided";material.shader=shader;material.doubleSidedGI=true;if(material.HasProperty("_Cull"))material.SetFloat("_Cull",0);AssetDatabase.CreateAsset(material,folder+"Cape_TwoSided.mat");AssetDatabase.SaveAssets();}
   Undo.RecordObject(smr,"Assign cape-only double-sided material");var materials=smr.sharedMaterials;materials[0]=material;smr.sharedMaterials=materials;
   PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
  }
  PrefabUtility.RecordPrefabInstancePropertyModifications(cloth);
  return "Collar pins="+pinned+"/"+coeff.Length+". Upper torso collider ends below neck. Two-sided material assigned="+assignMaterial+". Lower cape style preserved.";
 }
}
