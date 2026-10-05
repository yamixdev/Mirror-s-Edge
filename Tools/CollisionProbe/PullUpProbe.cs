using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MEdge;
using MEdge.Core;
using MEdge.Engine;
using MEdge.Source;
using MEdge.TdGame;
using UnityEditor;
using UnityEngine;
using UVector = MEdge.Core.Object.Vector;
using Camera = UnityEngine.Camera;
using Light = UnityEngine.Light;
using Texture2D = UnityEngine.Texture2D;

[InitializeOnLoad]
public static class PullUpProbe
{
    [Serializable] public class Pose {
        public float time;
        public string state;
        public Vector3 actor, eye, hand, root, velocity, rootDelta;
    }
    [Serializable] public class Timeline { public List<Pose> samples = new(); }
    static readonly CollisionProbe.Report report = new();
    static readonly Timeline timeline = new();
    static TdPlayerPawn pawn;
    static float grabTime = -1, jumpTime = -1, previousTime = -1;
    static int stage, ticks;
    static Vector3 initial;
    static float minimumZ;
    static float maximumVisualRootOffset;
    static Transform root, eye, hand;
    static Camera observer;
    static GameObject wall, ceiling;
    static int scenario;
    static readonly string[] scenarios = { "Wide", "Thin", "Crouched", "Blocked" };
    static PullUpProbe() {
        EditorApplication.playModeStateChanged += state => {
            if(state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PullUpProbe.Pending",false)) {
                SessionState.SetBool("PullUpProbe.Pending",false); Start();
            }
        };
    }
    public static void Run() { SessionState.SetBool("PullUpProbe.Pending",true); EditorApplication.EnterPlaymode(); }
    static void Start() {
        try {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position=new Vector3(0,1.5f,2.4f);wall.transform.localScale=new Vector3(10,3,4);
            new GameObject("Spawn").AddComponent<SpawnPoint>().transform.position=Vector3.up;
            new GameObject("MainCamera").AddComponent<Camera>().tag="MainCamera";
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(40,-30,0);
            observer=new GameObject("Observer").AddComponent<Camera>();observer.enabled=false;
            observer.transform.position=new Vector3(4,4,-5);observer.transform.LookAt(new Vector3(0,3,.4f));
            UWorld.EnsureStart();pawn=UWorld.Instance.WorldInfo._allActors.OfType<TdPlayerPawn>().First();
            EditorApplication.update+=Tick;
        } catch(Exception e) { Finish(e); }
    }
    static void Tick() {
        if(!EditorApplication.isPlaying) return;
        try {
            float time=pawn.WorldInfo.TimeSeconds;
            if(time == previousTime) return;
            previousTime=time;
            if(++ticks<30) return;
            if(stage==0) {
                wall.transform.localScale=new Vector3(10,3,scenario==1?.33723f:4f);
                wall.transform.position=new Vector3(0,1.5f,.4f+wall.transform.localScale.z*.5f);
                if(scenario>=2) {
                    ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ceiling.transform.localScale=new Vector3(10,.3f,4);
                    ceiling.transform.position=new Vector3(0,scenario==2?4.6f:3.95f,2.4f);
                }
                Physics.SyncTransforms();
                pawn.MoveNormal=new Vector3(0,0,-1).ToUnrealDir();pawn.MoveLedgeNormal=Vector3.up.ToUnrealDir();
                pawn.MoveLedgeLocation=new Vector3(0,3,.4f).ToUnrealPos();
                var grab=(TdMove_Grab)pawn.Moves[3];float extent=grab.CalculateRelativeExtent(pawn.CylinderComponent.CollisionRadius);
                pawn.SetLocation(pawn.MoveLedgeLocation+pawn.MoveNormal*(pawn.CylinderComponent.CollisionRadius+extent)-new UVector(0,0,92.8f));
                pawn.SetMove(TdPawn.EMovement.MOVE_Grabbing,false,false);
                Asset.UScriptToUnity.TryGetValue(pawn.Mesh1p.SkeletalMesh,out var mesh);
                var bones=((SkinnedMeshRenderer)mesh).transform.parent.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
                root=bones["root"];eye=bones["EyeJoint"];hand=bones["LeftHand"];
                grabTime=time;stage=1;return;
            }
            if(stage==1) {
                if(time-grabTime<1.5f) return;
                Capture("pullup-before");
                var pullup=(TdMove_GrabPullUp)pawn.Moves[10];
                bool canPullUp=pullup.CanDoMove();
                if(scenario==3) {
                    Add("BlockedHeadroomRejectsPullUp",!canPullUp,$"type={pullup.GrabPullUpType}");Finish(null);return;
                }
                Add("ClearPlatformAllowsPullUp",canPullUp,$"floor={pullup.FloorOverLedgeLocation.ToUnityPos()}; type={pullup.GrabPullUpType}; vault={pawn.Moves[9].CanDoMove()}");
                if(scenario==2) Add("LowCeilingChoosesCrouch",pullup.GrabPullUpType==TdMove_GrabPullUp.EGrabPullUpType.GPUT_IntoCrouch,"type="+pullup.GrabPullUpType);
                initial=pawn.Location.ToUnityPos();minimumZ=initial.z;maximumVisualRootOffset=0;
                var controller=(TdPlayerController)pawn.Controller;
                ((TdPlayerInput)controller.PlayerInput).Jump();controller.CheckJumpPressed();
                Add("SpaceChoosesPullUp",pawn.MovementState==TdPawn.EMovement.MOVE_GrabPullUp,"state="+pawn.MovementState);
                jumpTime=time;stage=2;
            }
            if(stage==2) {
                var position=pawn.Location.ToUnityPos();minimumZ=Math.Min(minimumZ,position.z);
                maximumVisualRootOffset=Mathf.Max(maximumVisualRootOffset,Vector2.Distance(new Vector2(root.position.x,root.position.z),new Vector2(position.x,position.z)));
                timeline.samples.Add(new Pose {time=time-jumpTime,state=pawn.MovementState.ToString(),actor=position,eye=eye.position,hand=hand.position,root=root.position,velocity=pawn.Velocity.ToUnityPos(),rootDelta=pawn.Mesh.RootMotionDelta.Translation.ToUnityPos()});
                if(time-jumpTime>.4f && stage==2) {Capture("pullup-moving");stage=3;}
            }
            if(stage==3) {
                var position=pawn.Location.ToUnityPos();minimumZ=Math.Min(minimumZ,position.z);
                maximumVisualRootOffset=Mathf.Max(maximumVisualRootOffset,Vector2.Distance(new Vector2(root.position.x,root.position.z),new Vector2(position.x,position.z)));
                timeline.samples.Add(new Pose {time=time-jumpTime,state=pawn.MovementState.ToString(),actor=position,eye=eye.position,hand=hand.position,root=root.position,velocity=pawn.Velocity.ToUnityPos(),rootDelta=pawn.Mesh.RootMotionDelta.Translation.ToUnityPos()});
                if(time-jumpTime<3.2f) return;
                Capture("pullup-after");
                var expectedState=scenario==2?TdPawn.EMovement.MOVE_Crouch:TdPawn.EMovement.MOVE_Walking;
                bool onSurface=scenario==1 ? position.y>=.75f && position.z>.73723f : position.y>=(scenario==2?3.55f:3.75f) && position.y<4.1f && position.z>.4f;
                Add("PullUpFinishesOnPlatform",pawn.MovementState==expectedState && onSurface,$"state={pawn.MovementState}; position={position}; start={initial}");
                Add("PullUpDoesNotLaunchBackwards",minimumZ>=initial.z-.25f,$"minimum forward position={minimumZ}; start={initial.z}");
                Add("VisualRootStaysWithPawn",maximumVisualRootOffset<.5f,"maximum offset="+maximumVisualRootOffset);
                scenario++;stage=0;ticks=0;
                if(ceiling) UnityEngine.Object.DestroyImmediate(ceiling);
                pawn.SetMove(TdPawn.EMovement.MOVE_Walking);pawn.Velocity=default;pawn.Acceleration=default;
                pawn.SetLocation(new Vector3(0,1,-2).ToUnrealPos());
            }
        } catch(Exception e) {Finish(e);}
    }
    static void Add(string name,bool passed,string detail) {report.results.Add(new CollisionProbe.Result{name=scenarios[scenario]+"_"+name,passed=passed,detail=detail});if(!passed)report.failures++;}
    static void Capture(string name) {
        foreach(var item in new[]{(Camera.main,"first"),(observer,"side")}) {
            var texture=new RenderTexture(640,360,24);var image=new Texture2D(640,360,TextureFormat.RGB24,false);
            var previous=item.Item1.targetTexture;item.Item1.targetTexture=texture;item.Item1.Render();RenderTexture.active=texture;
            image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"Evidence",name+"-"+scenarios[scenario]+"-"+item.Item2+".png"),image.EncodeToPNG());
            RenderTexture.active=null;item.Item1.targetTexture=previous;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(texture);
        }
    }
    static void Finish(Exception e) {
        EditorApplication.update-=Tick;
        if(e!=null)Add("PullUpSetup",false,e.ToString());
        var directory=Path.Combine(Directory.GetCurrentDirectory(),"Evidence");
        File.WriteAllText(Path.Combine(directory,"pullup-timeline.json"),JsonUtility.ToJson(timeline,true));
        File.WriteAllText(Path.Combine(directory,Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT")??"pullup.json"),JsonUtility.ToJson(report,true));
        EditorApplication.Exit(report.failures==0?0:2);
    }
}
