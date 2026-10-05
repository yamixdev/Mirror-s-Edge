using System;
using System.IO;
using System.Linq;
using MEdge;
using MEdge.Core;
using MEdge.Engine;
using MEdge.EditorTools;
using MEdge.Source;
using MEdge.TdGame;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ParkourObjectsProbe
{
    static readonly CollisionProbe.Report report=new();
    static TdPlayerPawn pawn;
    static GameObject[] objects;
    static int ticks, scenario, stage;
    static float previousTime=-1, enterTime;
    static float firstAngle, firstHeight, firstZipZ;
    static UnityEngine.Camera observer;
    static ParkourObjectsProbe() {
        EditorApplication.playModeStateChanged+=state=> {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("ParkourObjectsProbe.Pending",false)) {
                SessionState.SetBool("ParkourObjectsProbe.Pending",false);Start();
            }
        };
    }
    public static void Run() {
        // Save pre-generation evidence once; prefab generation is the feature under test.
        var baseline=new CollisionProbe.Report();
        foreach(var name in ParkourObjectFactory.PrefabNames) {
            bool exists=AssetDatabase.LoadAssetAtPath<GameObject>(ParkourObjectFactory.AssetFolder+"/"+name+".prefab");
            baseline.results.Add(new CollisionProbe.Result{name="Prefab_"+name,passed=exists,detail="Present before generation="+exists});if(!exists)baseline.failures++;
        }
        var baselinePath=Path.Combine(Directory.GetCurrentDirectory(),"Evidence/parkour-prefabs-baseline.json");
        if(!File.Exists(baselinePath))File.WriteAllText(baselinePath,JsonUtility.ToJson(baseline,true));
        if(baseline.failures!=0)ParkourObjectFactory.GeneratePrefabs();
        foreach(var name in ParkourObjectFactory.PrefabNames) {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ParkourObjectFactory.AssetFolder+"/"+name+".prefab");
            Add("Prefab_"+name,prefab && prefab.GetComponent<VolumeProxy>() && prefab.GetComponent<BoxCollider>().isTrigger && prefab.GetComponentsInChildren<MeshRenderer>().Length>0,"Serialized visible geometry and typed trigger");
        }
        var editable=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ParkourObjectFactory.AssetFolder+"/BalanceBeam.prefab"));
        Undo.RegisterFullObjectHierarchyUndo(editable,"Probe rebuild");
        var controls=editable.GetComponent<UnityTdBalanceWalkVolume>();controls.SplineControls[1]=new Vector3(0,0,3);
        ParkourObjectFactory.Rebuild(editable.GetComponent<ParkourObjectGeometry>(),true);
        Add("RebuildMatchesEditedSpline",Mathf.Abs(editable.GetComponent<BoxCollider>().size.z-5.02f)<.001f,"trigger length="+editable.GetComponent<BoxCollider>().size.z);
        Undo.FlushUndoRecordObjects();Undo.PerformUndo();
        Add("RebuildCanUndo",editable.GetComponent<ParkourObjectGeometry>().GeneratedGeometry && Mathf.Abs(editable.GetComponent<BoxCollider>().size.z-4.02f)<.001f,"trigger length="+editable.GetComponent<BoxCollider>().size.z);
        UnityEngine.Object.DestroyImmediate(editable);Undo.ClearAll();
        SessionState.SetString("ParkourObjectsProbe.Report",JsonUtility.ToJson(report));
        SessionState.SetBool("ParkourObjectsProbe.Pending",true);EditorApplication.EnterPlaymode();
    }
    static void Start() {
        try {
            var saved=JsonUtility.FromJson<CollisionProbe.Report>(SessionState.GetString("ParkourObjectsProbe.Report","{}"));report.results.AddRange(saved.results);report.failures+=saved.failures;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(15,-.5f,0);floor.transform.localScale=new Vector3(60,1,40);
            objects=ParkourObjectFactory.PrefabNames.Select(n=>UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ParkourObjectFactory.AssetFolder+"/"+n+".prefab"))).ToArray();
            objects[0].transform.position=new Vector3(0,1,0);objects[1].transform.position=new Vector3(10,3,0);objects[2].transform.position=new Vector3(20,2,0);objects[3].transform.position=new Vector3(30,5,0);
            for(int i=0;i<4;i++)objects[i].transform.rotation=Quaternion.Euler(0,new[]{90f,45f,-60f,25f}[i],0);
            new GameObject("Spawn").AddComponent<SpawnPoint>().transform.position=new Vector3(-5,1,0);
            new GameObject("MainCamera").AddComponent<UnityEngine.Camera>().tag="MainCamera";
            var light=new GameObject("Sun").AddComponent<UnityEngine.Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,-30,0);
            Physics.SyncTransforms();UWorld.EnsureStart();pawn=UWorld.Instance.WorldInfo._allActors.OfType<TdPlayerPawn>().First();
            observer=new GameObject("Observer").AddComponent<UnityEngine.Camera>();observer.enabled=false;
            EditorApplication.update+=Tick;
        } catch(Exception e) {Finish(e);}
    }
    static void Tick() {
        try {
            if(!EditorApplication.isPlaying || previousTime==pawn.WorldInfo.TimeSeconds)return;previousTime=pawn.WorldInfo.TimeSeconds;
            if(++ticks<30)return;
            if(stage==0) {
                pawn.SetMove(TdPawn.EMovement.MOVE_Walking);pawn.ActiveMovementVolume=null;pawn.MoveActionHint=TdPawn.EMoveActionHint.MAH_None;
                var rotation=objects[scenario].transform.rotation.ToUnrealRot();pawn.SetRotation(rotation);pawn.Controller.SetRotation(rotation);pawn.Acceleration=default;pawn.Velocity=default;
                Vector3 position=objects[scenario].transform.TransformPoint(scenario switch {0=>new(0,.92f,0),1=>new(0,-1.2f,-.6f),2=>new(0,-.9f,-.6f),_=>new(0,-1.3f,-2)});
                if(scenario!=0)pawn.SetMove(TdPawn.EMovement.MOVE_Falling);
                pawn.SetLocation(position.ToUnrealPos());pawn.Velocity=objects[scenario].transform.TransformDirection(scenario==1?new Vector3(0,2,4):scenario==2?Vector3.up*2:scenario==3?Vector3.forward*4:Vector3.zero).ToUnrealPos();
                if(scenario==2)pawn.MoveActionHint=TdPawn.EMoveActionHint.MAH_Up;
                pawn.SetZone(false,true);
                var volume=objects[scenario].GetComponent<VolumeProxy>().UnrealVolume;
                Add("Zone_"+scenario,pawn.PhysicsVolume==volume,$"physics volume={pawn.PhysicsVolume?.GetType().Name}; expected={volume.GetType().Name}; active={pawn.ActiveMovementVolume?.GetType().Name}; position={pawn.Location.ToUnityPos()}");
                enterTime=previousTime;stage=1;return;
            }
            float elapsed=previousTime-enterTime;
            if(stage==1 && elapsed>.12f) {
                bool active=scenario switch {0=>pawn.MovementState==TdPawn.EMovement.MOVE_Balance,1=>pawn.MovementState==TdPawn.EMovement.MOVE_Swing,2=>pawn.MovementState==TdPawn.EMovement.MOVE_IntoClimb || pawn.MovementState==TdPawn.EMovement.MOVE_Climb,_=>pawn.MovementState==TdPawn.EMovement.MOVE_IntoZipLine || pawn.MovementState==TdPawn.EMovement.MOVE_ZipLine};
                Add("Interaction_"+scenario,active,"state="+pawn.MovementState);
                if(!active){Next();return;}
                Capture("objects-"+ParkourObjectFactory.PrefabNames[scenario]);
                if(scenario==0){pawn.SetLocation(objects[scenario].transform.TransformPoint(new Vector3(0,.92f,2.8f)).ToUnrealPos());pawn.SetZone(false,true);Add("BalanceCanLeave",pawn.MovementState==TdPawn.EMovement.MOVE_Walking || pawn.MovementState==TdPawn.EMovement.MOVE_Falling,"state="+pawn.MovementState);Next();return;}
                firstAngle=((TdMove_Swing)pawn.Moves[60]).SwingAngle;firstHeight=pawn.Location.Z;firstZipZ=objects[scenario].transform.InverseTransformPoint(pawn.Location.ToUnityPos()).z;stage=2;
            }
            if(stage==2 && elapsed>1.1f) {
                if(scenario==1) {
                    var swing=(TdMove_Swing)pawn.Moves[60];
                    Add("SwingMoves",pawn.MovementState==TdPawn.EMovement.MOVE_Swing && Mathf.Abs(swing.SwingAngle-firstAngle)>.03f,"angle="+swing.SwingAngle);
                    swing.SwingAngle=.35f; // Jump timing deliberately selected on the forward swing.
                    pawn.HandleMoveAction(TdPawn.EMovementAction.MA_Jump);
                    Add("SwingSpaceReleases",pawn.MovementState==TdPawn.EMovement.MOVE_SwingJump,"state="+pawn.MovementState);
                } else if(scenario==2) {
                    Add("LadderSettlesIntoClimb",pawn.MovementState==TdPawn.EMovement.MOVE_Climb,"state="+pawn.MovementState);
                    pawn.HandleMoveAction(TdPawn.EMovementAction.MA_ClimbUp);firstHeight=pawn.Location.Z;stage=3;return;
                } else Add("ZiplineMovesForward",objects[scenario].transform.InverseTransformPoint(pawn.Location.ToUnityPos()).z>firstZipZ+.5f,"position="+pawn.Location.ToUnityPos());
                Next();
            }
            if(stage==3 && elapsed>1.9f){Add("LadderClimbsUp",pawn.Location.Z>firstHeight+20f,"position="+pawn.Location.ToUnityPos());Next();}
        } catch(Exception e) {Finish(e);}
    }
    static void Next() {scenario++;stage=0;ticks=0;if(scenario==4)Finish(null);}
    static void Add(string name,bool ok,string detail) {report.results.Add(new CollisionProbe.Result{name=name,passed=ok,detail=detail});if(!ok)report.failures++;}
    static void Capture(string name) {
        observer.transform.position=objects[scenario].transform.TransformPoint(new Vector3(4,2.5f,-5));observer.transform.LookAt(objects[scenario].transform.position);
        var target=new RenderTexture(640,360,24);var texture=new UnityEngine.Texture2D(640,360,TextureFormat.RGB24,false);
        observer.targetTexture=target;observer.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,640,360),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"Evidence",name+".png"),texture.EncodeToPNG());
        observer.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);
    }
    static void Finish(Exception e) {
        EditorApplication.update-=Tick;if(e!=null)Add("Setup",false,e.ToString());
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"Evidence",Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT")??"objects.json"),JsonUtility.ToJson(report,true));EditorApplication.Exit(report.failures==0?0:2);
    }
}
