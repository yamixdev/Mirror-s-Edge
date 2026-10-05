using System;
using System.IO;
using System.Linq;
using MEdge.Source;
using MEdge.TdGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PublishedSceneProbe
{
    static readonly CollisionProbe.Report report = new();
    static int frames;
    static PublishedSceneProbe()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PublishedSceneProbe.Pending", false)) {
                SessionState.SetBool("PublishedSceneProbe.Pending", false);
                JsonUtility.FromJsonOverwrite(SessionState.GetString("PublishedSceneProbe.Preflight", "{}"), report);
                EditorApplication.update += Tick;
            }
        };
    }

    public static void Run()
    {
        try {
            var scene = EditorSceneManager.OpenScene("Assets/New Scene.unity", OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            Add("SceneHasOneSpawn", roots.SelectMany(r=>r.GetComponentsInChildren<MEdge.SpawnPoint>(true)).Count()==1, "one player start");
            Add("SceneHasCameraAndLight", roots.Any(r=>r.GetComponent<Camera>()) && roots.Any(r=>r.GetComponent<Light>()), "camera and lighting");
            Add("SceneHasNoMissingScripts", roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0), "all scene components resolve");
            Add("SceneNeedsNoPavementPack", !AssetDatabase.GetDependencies(scene.path,true).Any(p=>p.Contains("YughuesFreePavementsMaterials")), "portable scene materials");
            SessionState.SetString("PublishedSceneProbe.Preflight", JsonUtility.ToJson(report));
            SessionState.SetBool("PublishedSceneProbe.Pending", true);
            EditorApplication.EnterPlaymode();
        } catch (Exception e) { Finish(e); }
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || ++frames<180) return;
        EditorApplication.update-=Tick;
        try {
            var world=MEdge.Engine.UWorld.Instance;
            var pawn=world.WorldInfo._allActors.OfType<TdPlayerPawn>().First();
            Add("SceneStartsLivingPlayer", pawn.Health>0, "health="+pawn.Health);
            Add("PlayerPositionIsFinite", Finite(pawn.Location.X) && Finite(pawn.Location.Y) && Finite(pawn.Location.Z), "location="+pawn.Location);
            Add("FullBodyMaterialsAvailable", Resources.Load<Material>("LocalFaith3P/MI_Faith_Lowres_Face")?.mainTexture && Resources.Load<Material>("LocalFaith3P/MI_Faith_Lowres_Upper")?.mainTexture, "included face and clothes textures");
            Finish(null);
        } catch (Exception e) { Finish(e); }
    }

    static void Add(string name,bool passed,string detail)
    {
        if(!passed) report.failures++;
        report.results.Add(new CollisionProbe.Result{name=name,passed=passed,detail=detail});
    }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static void Finish(Exception error)
    {
        EditorApplication.update-=Tick;
        if(error!=null) Add("PublishedSceneSetup",false,error.ToString());
        Directory.CreateDirectory("Evidence");
        File.WriteAllText(Path.Combine("Evidence",Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT")??"published-scene.json"),JsonUtility.ToJson(report,true));
        EditorApplication.Exit(report.failures==0?0:2);
    }
}
