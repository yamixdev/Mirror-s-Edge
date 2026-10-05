using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MEdge;
using MEdge.Core;
using MEdge.Engine;
using MEdge.Source;
using MEdge.TdGame;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ShimmyProbe
{
    sealed class CountNotify : AnimNotify {
        public int count;
        public override void Notify(AnimNodeSequence node) { count++; }
    }
    static readonly CollisionProbe.Report report = new();
    static readonly List<string> samples = new();
    static TdPlayerPawn pawn;
    static TdMove_Swing swing;
    static GameObject bar;
    static Transform[] bones;
    static int stage, frames, shot;
    static float previous = -1, started;
    static CountNotify left, right;
    static float maximumContactError, maximumHandStep, minimumLeft = 1, minimumRight = 1;
    static bool bothReleased;
    static Vector3 lastLeft, lastRight;
    static ShimmyProbe() {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("ShimmyProbe.Pending", false)) {
                SessionState.SetBool("ShimmyProbe.Pending", false); Start();
            }
        };
    }
    public static void Run() { SessionState.SetBool("ShimmyProbe.Pending", true); EditorApplication.EnterPlaymode(); }
    static void Start() {
        try {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Time.captureDeltaTime = 1f/60f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0,-.5f,0); floor.transform.localScale = new Vector3(40,1,40);
            bar = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Parkour/SwingBar.prefab")); bar.transform.position = new Vector3(0,5,0);
            new GameObject("Spawn").AddComponent<SpawnPoint>().transform.position = Vector3.up;
            new GameObject("MainCamera").AddComponent<UnityEngine.Camera>().tag = "MainCamera";
            var light = new GameObject("Sun").AddComponent<UnityEngine.Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(35,-40,0);
            Physics.SyncTransforms(); UWorld.EnsureStart(); pawn = UWorld.Instance.WorldInfo._allActors.OfType<TdPlayerPawn>().First(); swing = (TdMove_Swing)pawn.Moves[60];
            EditorApplication.update += Tick;
        } catch(Exception e) { Finish(e); }
    }
    static void Tick() {
        try {
            if (!EditorApplication.isPlaying || previous == pawn.WorldInfo.TimeSeconds) return;
            previous = pawn.WorldInfo.TimeSeconds; if (++frames < 30) return;
            if (stage == 0) {
                Asset.UScriptToUnity.TryGetValue(pawn.Mesh1p.SkeletalMesh, out var mesh);
                var renderer = (SkinnedMeshRenderer)mesh; bones = renderer.transform.parent.GetComponentsInChildren<Transform>();
                // Neutral material makes geometry visible without depending on unimported texture assets.
                var material = new UnityEngine.Material(Shader.Find("Standard")) { color = new Color(.65f,.72f,.78f) };
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                var seq = pawn.Mesh1p.FindAnimSequence("SwingStrafe");
                right = new CountNotify(); left = new CountNotify();
                NotifyBoundaries();
                var events = seq.Notifies; events[0].Notify = right; events[1].Notify = left; seq.Notifies = events;
                pawn.SetMove(TdPawn.EMovement.MOVE_Falling); pawn.SetLocation(new Vector3(0,3.8f,-.6f).ToUnrealPos()); pawn.SetRotation(default); pawn.Controller.SetRotation(default);
                swing.Volume = (TdSwingVolume)bar.GetComponent<VolumeProxy>().UnrealVolume; pawn.Velocity = new Vector3(0,2,4).ToUnrealPos(); pawn.SetMove(TdPawn.EMovement.MOVE_Swing);
                started = previous; stage = 1; return;
            }
            swing.SwingAngle = 0; swing.SwingVelocity = 0;
            if (stage == 1) {
                if (previous-started < 2f) return;
                pawn.HandleMoveAction(TdPawn.EMovementAction.MA_ShimmyRight); Check("RightStarts", swing.bIsShimmying, "actual MA_ShimmyRight"); started=previous; stage=2; shot=0; ResetMetrics();
            }
            if (stage == 2 || stage == 4) {
                float elapsed=previous-started;
                Metrics(elapsed);
                samples.Add($"{stage},{elapsed:F5},{swing.CustomAnimNode.CurrentTime:F5},{swing.CustomAnimNode.NodeTotalWeight:F5},"+string.Join(",", new[]{"LeftArm","LeftForeArm","LeftHand","LeftHandMiddle1","RightArm","RightForeArm","RightHand","RightHandMiddle1"}.Select(n=>bones.First(b=>b.name==n).position.ToString("F5"))));
                if (elapsed > .2f + shot*.25f && shot < 5) { Capture((stage==2 ? "right" : "left")+shot++); }
                if (elapsed < 1.8f) return;
                Check(stage==2 ? "RightBothGripEvents" : "LeftBothGripEvents", left.count==1 && right.count==1, $"LH={left.count},RH={right.count}");
                string label=stage==2 ? "Right" : "Left";
                Check(label+"HandsTransferSeparately", minimumLeft < .1f && minimumRight < .1f && !bothReleased, $"minimum LH/RH={minimumLeft}/{minimumRight}; both released={bothReleased}");
                Check(label+"SupportHandContact", maximumContactError < .06f, $"maximum palm-axis distance at full IK={maximumContactError}m");
                Check(label+"NoHandTeleport", maximumHandStep < .12f, $"maximum hand displacement per 60Hz frame={maximumHandStep}m");
                if (stage==2) { stage=3; started=previous; return; }
                PreserveTwist();
                swing.SwingAngle=.35f; swing.SwingVelocity=3f;
                pawn.HandleMoveAction(TdPawn.EMovementAction.MA_Jump);
                Check("GripSettingsReleasedForNextMove", pawn.LeftHandWorldIKController.UnityPalmBone == MEdge.Core.Object.NAME_None && pawn.RightHandWorldIKController.UnityPalmBone == MEdge.Core.Object.NAME_None && !pawn.LeftHandWorldIKController.UnityFollowAnimatedGrip && !pawn.RightHandWorldIKController.UnityFollowAnimatedGrip, "next move uses its own wrist targets");
                Check("ExitKeepsWristGoalDuringFade", (pawn.LeftHandWorldIKController.EffectorLocation-pawn.Mesh1p.GetBoneLocation("LeftHand")).Size()<.1f && (pawn.RightHandWorldIKController.EffectorLocation-pawn.Mesh1p.GetBoneLocation("RightHand")).Size()<.1f, "palm goal converted back to current wrist before blending out");
                Finish(null); return;
            }
            if (stage == 3) {
                if (previous-started < .6f) return;
                left.count=right.count=0;
                pawn.HandleMoveAction(TdPawn.EMovementAction.MA_ShimmyLeft); Check("LeftStarts", swing.bIsShimmying, "actual MA_ShimmyLeft"); started=previous; stage=4; shot=0; ResetMetrics();
            }
        } catch(Exception e) { Finish(e); }
    }
    static void NotifyBoundaries() {
        var a=new CountNotify(); var b=new CountNotify();
        var seq=new AnimSequence { SequenceName="SwingStrafe", SequenceLength=1f, RateScale=1f };
        seq.Notifies.AddItem(new AnimSequence.AnimNotifyEvent {Time=.25f, Notify=a});
        seq.Notifies.AddItem(new AnimSequence.AnimNotifyEvent {Time=.75f, Notify=b});
        var node=new AnimNodeSequence {AnimSeq=seq, SkelComponent=pawn.Mesh1p, CurrentTime=1f, NodeTotalWeight=1f, NotifyWeightThreshold=.1f, bPlaying=true};
        node.AdvanceBy(-.25f,.25f,true); node.AdvanceBy(-.25f,.25f,true);
        Check("ReverseExactBoundaryOnce", b.count==1 && a.count==0, $"late={b.count},early={a.count}");
        a.count=b.count=0;node.CurrentTime=.9f;node.bLooping=true;node.AdvanceBy(-2.2f,2.2f,true);
        Check("ReverseLoopCrossings", a.count==2 && b.count==3, $"early={a.count},late={b.count}");
        a.count=b.count=0;node.CurrentTime=.9f;node.bLooping=false;node.NodeTotalWeight=0;node.AdvanceBy(-.8f,.8f,true);
        Check("ReverseRespectsNotifyWeight", a.count==0 && b.count==0, "zero-weight node remains silent");
        node.CurrentTime=.9f;node.NodeTotalWeight=1;node.AdvanceBy(-.8f,.8f,false);
        Check("ReverseRespectsNoFire", a.count==0 && b.count==0, "bFireNotifies=false remains silent");
        seq.SequenceName="OtherReverseClip";node.CurrentTime=.9f;node.AdvanceBy(-.8f,.8f,true);
        Check("OtherReverseClipsUnchanged", a.count==0 && b.count==0, "opt-in limited to SwingStrafe");
    }
    static void PreserveTwist() {
        foreach(var hand in new[]{"LeftHand","RightHand"}) {
            var mesh=pawn.Mesh1p; int wrist=mesh.MatchRefBone(hand),lower=mesh.SkeletalMesh.RefSkeleton[wrist].ParentIndex,upper=mesh.SkeletalMesh.RefSkeleton[lower].ParentIndex;
            var controller=new SkelControlLimb { UnityPreserveAnimatedTwist=true, JointTargetLocationSpace=SkelControlBase.EBoneControlSpace.BCS_ComponentSpace, EffectorLocationSpace=SkelControlBase.EBoneControlSpace.BCS_ComponentSpace, EffectorLocation=mesh.SpaceBases[wrist].GetOrigin(), JointTargetLocation=mesh.SpaceBases[lower].GetOrigin() };
            var output=new MEdge.array<MEdge.Core.Object.Matrix>(); controller.CalculateNewBoneTransforms(wrist,mesh,ref output);
            float difference=0;
            for(int bone=0;bone<2;bone++) for(int axis=0;axis<3;axis++) for(int column=0;column<3;column++) difference=Mathf.Max(difference,Mathf.Abs(output[bone].M[axis,column]-mesh.SpaceBases[bone==0?upper:lower].M[axis,column]));
            Check(hand+"IKPreservesAnimatedRoll", difference<.001f, "maximum rotation matrix difference="+difference);
        }
    }
    static void ResetMetrics() {
        maximumContactError=maximumHandStep=0; minimumLeft=minimumRight=1; bothReleased=false;
        lastLeft=bones.First(b=>b.name=="LeftHandMiddle1").position; lastRight=bones.First(b=>b.name=="RightHandMiddle1").position;
    }
    static void Metrics(float elapsed) {
        float l=pawn.LeftHandWorldIKController.ControlStrength,r=pawn.RightHandWorldIKController.ControlStrength;
        if(swing.bIsShimmying) { minimumLeft=Mathf.Min(minimumLeft,l); minimumRight=Mathf.Min(minimumRight,r); bothReleased|=l<.1f && r<.1f; }
        Vector3 lp=bones.First(b=>b.name=="LeftHandMiddle1").position,rp=bones.First(b=>b.name=="RightHandMiddle1").position;
        maximumHandStep=Mathf.Max(maximumHandStep,Vector3.Distance(lp,lastLeft),Vector3.Distance(rp,lastRight)); lastLeft=lp;lastRight=rp;
        if(elapsed<.2f) return;
        foreach(var hand in new[]{("LeftHandMiddle1",l),("RightHandMiddle1",r)}) {
            if(hand.Item2<.99f) continue;
            Vector3 p=bar.transform.InverseTransformPoint(bones.First(b=>b.name==hand.Item1).position);
            maximumContactError=Mathf.Max(maximumContactError,new Vector2(p.y,p.z).magnitude);
        }
    }
    static void Capture(string label) {
        var camera = new GameObject("Observer").AddComponent<UnityEngine.Camera>(); camera.enabled=false;
        var center=swing.SwingLocation.ToUnityPos(); camera.transform.position=center+new Vector3(1.5f, .1f,-1.5f); camera.transform.LookAt(center-Vector3.up*.55f);
        Image(camera,label+"-body"); UnityEngine.Object.DestroyImmediate(camera.gameObject);
        Image(UnityEngine.Camera.main,label+"-fps");
        // Also inspect the grip in the existing game camera's projection while
        // looking up. Restore the rotation immediately after this capture.
        var gameCamera=UnityEngine.Camera.main; var rotation=gameCamera.transform.rotation;
        gameCamera.transform.Rotate(-25,0,0,Space.Self); Image(gameCamera,label+"-fps-up"); gameCamera.transform.rotation=rotation;
    }
    static void Image(UnityEngine.Camera camera,string label) {
        var target=new RenderTexture(960,540,24); var texture=new UnityEngine.Texture2D(960,540,TextureFormat.RGB24,false);
        var old=camera.targetTexture; var active=RenderTexture.active;
        camera.targetTexture=target; camera.Render(); RenderTexture.active=target; texture.ReadPixels(new Rect(0,0,960,540),0,0); texture.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"Evidence","shimmy-"+label+".png"),texture.EncodeToPNG());
        camera.targetTexture=old; RenderTexture.active=active; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
    }
    static void Check(string name,bool passed,string detail) { report.results.Add(new CollisionProbe.Result { name=name,passed=passed,detail=detail }); if(!passed) report.failures++; }
    static void Finish(Exception e) {
        EditorApplication.update-=Tick; if(e!=null) Check("Harness",false,e.ToString());
        string name=Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT")??"shimmy.json";
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"Evidence",name),JsonUtility.ToJson(report,true));
        File.WriteAllLines(Path.Combine(Directory.GetCurrentDirectory(),"Evidence",name+".csv"),samples);
        EditorApplication.Exit(report.failures==0?0:2);
    }
}
