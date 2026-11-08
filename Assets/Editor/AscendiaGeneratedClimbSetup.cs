using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
[InitializeOnLoad]
public static class AscendiaGeneratedClimbSetup
{
 const string Folder="RigBackup_20261102/GeneratedMapSwitch";
 static AscendiaGeneratedClimbSetup(){EditorApplication.update+=Apply;}
 static void Apply()
 {
  if(File.Exists(Folder+"/applied.txt")){EditorApplication.update-=Apply;return;}
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  var scene=SceneManager.GetActiveScene();if(scene.name!="ClimbScene")return;
  Directory.CreateDirectory(Folder);
  if(!EditorSceneManager.SaveScene(scene,Folder+"/before_"+System.DateTime.Now.ToString("HHmmss")+".unity",true))return;
  int count=0;
  foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
  {
   if(t.name!="ClimbingIK_TestWall"||!t.gameObject.activeSelf)continue;
   Undo.RecordObject(t.gameObject,"Use generated mountain");t.gameObject.SetActive(false);
   PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);count++;
  }
  if(count>0)EditorSceneManager.MarkSceneDirty(scene);
  File.WriteAllText(Folder+"/applied.txt","Disabled test walls: "+count+". Generated Wall tiles now share an IK surface. Save scene.");
  EditorApplication.update-=Apply;
 }
}
