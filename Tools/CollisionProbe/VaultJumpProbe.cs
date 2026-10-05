using System;
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

[InitializeOnLoad]
public static class VaultJumpProbe
{
    static readonly CollisionProbe.Report report = new();
    static TdPlayerPawn pawn;
    static int ticks, scenario;
    static float previousTime=-1, launchTime=-1;
    static Vector3 start;
    static float maximumHeight;
    static readonly string[] names={"HeldLeftHand", "ReleasedLeftHand", "TwoHands", "VaultOnto", "Ceiling"};
    static VaultJumpProbe() {
        EditorApplication.playModeStateChanged+=state=> {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("VaultJumpProbe.Pending",false)) {
                SessionState.SetBool("VaultJumpProbe.Pending",false);Start();
            }
        };
    }
    public static void Run() {SessionState.SetBool("VaultJumpProbe.Pending",true);EditorApplication.EnterPlaymode();}
    static void Start() {
        try {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(30,1,30);
            new GameObject("Spawn").AddComponent<SpawnPoint>().transform.position=Vector3.up;
            new GameObject("MainCamera").AddComponent<UnityEngine.Camera>().tag="MainCamera";
            UWorld.EnsureStart();pawn=UWorld.Instance.WorldInfo._allActors.OfType<TdPlayerPawn>().First();
            EditorApplication.update+=Tick;
        } catch(Exception e) {Finish(e);}
    }
    static void Tick() {
        try {
            if(!EditorApplication.isPlaying || pawn.WorldInfo.TimeSeconds==previousTime)return;
            previousTime=pawn.WorldInfo.TimeSeconds;
            if(++ticks<30)return;
            if(launchTime<0) {
                pawn.SetMove(TdPawn.EMovement.MOVE_Walking);
                pawn.SetLocation(new Vector3(0,2.1f,0).ToUnrealPos());pawn.SetRotation(default);pawn.Controller.SetRotation(default);
                pawn.MoveLedgeLocation=new Vector3(0,1,0).ToUnrealPos();pawn.MoveNormal=Vector3.back.ToUnrealDir();pawn.MoveLedgeNormal=Vector3.up.ToUnrealDir();
                var vault=(TdMove_SpeedVault)pawn.Moves[9];
                vault.ActiveVaultType=Enumerable.Range(0,vault.VaultTypes.Length).First(i=>vault.VaultTypes[i].AnimName=="VaultOver");
                vault.bVaultOnto=scenario==3;vault.bEndMoveFalling=true;vault.MoveDirection=Vector3.forward.ToUnrealDir();vault.SavedVelocity=Vector3.forward.ToUnrealPos()*6f;
                vault.VaultEndPosition=new Vector3(0,.92f,1.8f).ToUnrealPos();
                var type=vault.VaultTypes[vault.ActiveVaultType];type.bRightHandIK=scenario==2;vault.VaultTypes[vault.ActiveVaultType]=type;
                pawn.SetMove(TdPawn.EMovement.MOVE_VaultOver);
                vault.VaultState=3;vault.TargetLocation=new Vector3(0,2.1f,0).ToUnrealPos();vault.SavedVelocity=Vector3.forward.ToUnrealPos()*6f;pawn.Velocity=vault.SavedVelocity;
                if(scenario==4) {
                    var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.transform.position=new Vector3(0,3.6f,3);ceiling.transform.localScale=new Vector3(20,.2f,20);Physics.SyncTransforms();
                }
                ((TdPlayerInput)((TdPlayerController)pawn.Controller).PlayerInput).aUp=scenario==1?0:1;
                typeof(TdPlayerInput).GetProperty("UnityJumpHeld")?.SetValue(((TdPlayerController)pawn.Controller).PlayerInput,scenario!=1);
                vault.UpdateVaultMovement();start=pawn.Location.ToUnityPos();maximumHeight=start.y;
                if(scenario==0 || scenario==4) {
                    Add("LaunchesOnce",pawn.MovementState==TdPawn.EMovement.MOVE_Falling && pawn.Velocity.Z>0 && !vault.bUsePreciseLocation,$"state={pawn.MovementState}; velocity={pawn.Velocity.ToUnityPos()}");
                    Add("PreservesForwardSpeed",Mathf.Abs(pawn.Velocity.ToUnityPos().z-6f)<.05f,"velocity="+pawn.Velocity.ToUnityPos());
                    Add("RestoresCollisions",pawn.bCollideWorld && pawn.bBlockActors,"collide="+pawn.bCollideWorld+"; block="+pawn.bBlockActors);
                    launchTime=previousTime;
                    if(pawn.MovementState!=TdPawn.EMovement.MOVE_Falling) Next();
                } else {Add("KeepsOrdinaryVault",pawn.Velocity.Z<=0 && pawn.MovementState!=TdPawn.EMovement.MOVE_VaultOver,"state="+pawn.MovementState+"; velocity="+pawn.Velocity.ToUnityPos());Next();}
                return;
            }
            float elapsed=previousTime-launchTime;
            maximumHeight=Mathf.Max(maximumHeight,pawn.Location.ToUnityPos().y);
            typeof(TdPlayerInput).GetProperty("UnityJumpHeld")?.SetValue(((TdPlayerController)pawn.Controller).PlayerInput,true);
            if(elapsed<.2f)return;
            if(!report.results.Any(r=>r.name==names[scenario]+"_HeldButtonCannotRepeatImpulse")) {
                var tryJump=typeof(TdMove_SpeedVault).GetMethod("TryUnityVaultJump",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var velocity=pawn.Velocity;
                bool repeated=tryJump!=null && (bool)tryJump.Invoke(pawn.Moves[9],null);
                Add("HeldButtonCannotRepeatImpulse",tryJump!=null && !repeated && pawn.Velocity==velocity,"state="+pawn.MovementState+"; held=true; repeated="+repeated);
            }
            if(scenario==0 && elapsed<.3f) AddOnce("MovesUpAndForward",pawn.Location.ToUnityPos().y>start.y+.2f && pawn.Location.ToUnityPos().z>start.z+.5f,"position="+pawn.Location.ToUnityPos());
            if(elapsed<.7f)return;
            Add("GravityOrCeilingEndsAscent",pawn.Velocity.Z<0 || pawn.MovementState==TdPawn.EMovement.MOVE_Walking,"velocity="+pawn.Velocity.ToUnityPos());
            if(scenario==4)Add("CeilingBlocksJump",maximumHeight<2.65f,"maximum height="+maximumHeight);
            Add("NoRepeatedAirJump",pawn.Location.ToUnityPos().y<start.y+1.5f,"position="+pawn.Location.ToUnityPos());
            Next();
        } catch(Exception e) {Finish(e);}
    }
    static void Next() {scenario++;launchTime=-1;ticks=0;if(scenario==names.Length)Finish(null);}
    static void AddOnce(string name,bool ok,string detail) {if(!report.results.Any(r=>r.name==names[scenario]+"_"+name))Add(name,ok,detail);}
    static void Add(string name,bool ok,string detail) {report.results.Add(new CollisionProbe.Result{name=names[Math.Min(scenario,names.Length-1)]+"_"+name,passed=ok,detail=detail});if(!ok)report.failures++;}
    static void Finish(Exception e) {
        EditorApplication.update-=Tick;if(e!=null)Add("Setup",false,e.ToString());
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"Evidence",Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT")??"vault.json"),JsonUtility.ToJson(report,true));EditorApplication.Exit(report.failures==0?0:2);
    }
}
