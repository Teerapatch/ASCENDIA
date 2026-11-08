using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.IO;
using System.Linq;
using System.Text;
[InitializeOnLoad]
public static class AscendiaRigAudit {
 static AscendiaRigAudit(){EditorApplication.delayCall+=Run;}
 [MenuItem("Tools/ASCENDIA/Check Clean Rig")]
 public static void Run(){
 string report="RigBackup_20261029/clean-validation.txt";
 if(File.Exists(report)||EditorApplication.isPlayingOrWillChangePlaymode)return;
  const string path="Assets/_kin/Prefab/Player/Player_Clean.fbx";
 var importer=(ModelImporter)AssetImporter.GetAtPath(path);
 var hd=importer.humanDescription;
 importer.animationType=ModelImporterAnimationType.Generic;
 importer.SaveAndReimport();
 var generic=AssetDatabase.LoadAssetAtPath<GameObject>(path);
 hd.skeleton=generic.GetComponentsInChildren<Transform>(true).Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
 importer.animationType=ModelImporterAnimationType.Human;
 importer.humanDescription=hd;
 importer.SaveAndReimport();
 var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
 if(!model)return;
 var sb=new StringBuilder();
 GameObject go=null;
 try{
 go=Object.Instantiate(model);go.hideFlags=HideFlags.HideAndDontSave;
 var anim=go.GetComponent<Animator>(); anim.cullingMode=AnimatorCullingMode.AlwaysAnimate; anim.Rebind(); anim.Update(0);
 sb.AppendLine("Avatar valid="+(anim&&anim.avatar&&anim.avatar.isValid)+" humanoid="+(anim&&anim.avatar&&anim.avatar.isHuman));
 if(!anim||!anim.avatar||!anim.avatar.isValid||!anim.avatar.isHuman)throw new System.Exception("Invalid humanoid");
 foreach(var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())sb.AppendLine(smr.name+" vertices="+smr.sharedMesh.vertexCount+" missingBones="+smr.bones.Count(b=>!b)+" materials="+string.Join(",",smr.sharedMaterials.Select(m=>m?m.name:"NULL")));
 var clips=AssetDatabase.LoadAllAssetsAtPath("Assets/_kin/Animation/Ch14_nonPBR@Idle.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
 foreach(var clip in clips){
 var graph=PlayableGraph.Create("RigAudit");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
 try{var p=AnimationClipPlayable.Create(graph,clip);var output=AnimationPlayableOutput.Create(graph,"Animation",anim);output.SetSourcePlayable(p);graph.Play();
 foreach(float t in new[]{0f,0.25f,0.5f,0.75f}){p.SetTime(t*clip.length);graph.Evaluate(0.02f);
 var head=anim.GetBoneTransform(HumanBodyBones.Head);var hips=anim.GetBoneTransform(HumanBodyBones.Hips);
 sb.AppendLine("humanMotion="+clip.humanMotion+" length="+clip.length+" "+clip.name+" t="+t+" head="+head.position+" hips="+hips.position);
 foreach(var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>()){var mesh=new Mesh();smr.BakeMesh(mesh);sb.AppendLine(smr.name+" bounds="+mesh.bounds);Object.DestroyImmediate(mesh);}
 }
 }finally{graph.Destroy();}
 }
 sb.AppendLine("Available clips="+clips.Length+"; walk/run not found in supplied animation folder.");
 }catch(System.Exception e){sb.AppendLine(e.ToString());}
 finally{if(go)Object.DestroyImmediate(go);File.WriteAllText(report,sb.ToString());}
 }
}


