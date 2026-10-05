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

// Lives only in the isolated editor project, never in the user's game assembly.
[InitializeOnLoad]
public static class JumpSurvivalProbe
{
    public sealed class TimerPawn : TdPawn
    {
        public int Suicides;
        public override void Suicide() { Suicides++; }
    }
    static readonly CollisionProbe.Report report = new();
    static TdPlayerPawn pawn;
    static float startTime = -1, previousTime = -1, initialY, maximumY;
    static int ticks;
    static bool vault;
    static JumpSurvivalProbe()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("JumpSurvival.Pending", false)) {
                SessionState.SetBool("JumpSurvival.Pending", false); Start();
            }
        };
    }
    public static void Run() { SessionState.SetBool("JumpSurvival.Pending", true); EditorApplication.EnterPlaymode(); }
    static void Start()
    {
        try {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(30, 1, 30);
            new GameObject("Spawn").AddComponent<SpawnPoint>().transform.position = Vector3.up;
            new GameObject("MainCamera").AddComponent<UnityEngine.Camera>().tag = "MainCamera";
            UWorld.EnsureStart(); pawn = UWorld.Instance.WorldInfo._allActors.OfType<TdPlayerPawn>().First();
            var legacy = new TimerPawn();
            legacy.SetTimer(.01f, false, "CalculateJumpSpeed"); legacy.UpdateTimers(.02f);
            Add("PreviouslyScheduledCallbackDoesNotKill", legacy.Suicides == 0, "suicide calls=" + legacy.Suicides);
            var pending = new TimerPawn(); pending.SetTimer(.01f, false, "CalculateJumpSpeed");
            pending.NotifyJump(); pending.UpdateTimers(16f);
            Add("JumpCancelsLegacyCallback", pending.Suicides == 0 && !pending.IsTimerActive("CalculateJumpSpeed"), "suicide calls=" + pending.Suicides);
            var lethal = new Pawn { Health = 100 };
            bool deathReported = false;
            lethal.Died = (killer, type, location) => { deathReported = true; return true; };
            lethal.Suicide();
            Add("ExplicitLethalPathStillWorks", deathReported && lethal.Health == 0, "health=" + lethal.Health);
            EditorApplication.update += Tick;
        } catch (Exception e) { Finish(e); }
    }
    static void Tick()
    {
        try {
            if (!EditorApplication.isPlaying || previousTime == pawn.WorldInfo.TimeSeconds) return;
            previousTime = pawn.WorldInfo.TimeSeconds;
            if (++ticks < 30) return;
            if (startTime < 0) {
                initialY = maximumY = pawn.Location.ToUnityPos().y;
                var controller = (TdPlayerController)pawn.Controller;
                ((TdPlayerInput)controller.PlayerInput).Jump(); controller.CheckJumpPressed();
                startTime = previousTime;
                Add("NormalJumpStarts", pawn.Velocity.Z > 0, "state=" + pawn.MovementState + "; velocity=" + pawn.Velocity.ToUnityPos());
            }
            maximumY = Mathf.Max(maximumY, pawn.Location.ToUnityPos().y);
            float elapsed = previousTime - startTime;
            if (pawn.Health <= 0 || elapsed >= 16f) {
                Add((vault ? "Vault" : "Normal") + "AliveAfterRandomTimerWindow", pawn.Health > 0 && elapsed >= 16f, "elapsed=" + elapsed + "; health=" + pawn.Health + "; state=" + pawn.MovementState);
                Add((vault ? "Vault" : "Normal") + "JumpRisesAndLands", maximumY > initialY + .3f && pawn.MovementState == TdPawn.EMovement.MOVE_Walking,
                    "rise=" + (maximumY - initialY) + "; state=" + pawn.MovementState);
                if (!vault && pawn.Health > 0) {
                    LaunchVault(); return;
                }
                Finish(null);
            }
        } catch (Exception e) { Finish(e); }
    }
    static void LaunchVault()
    {
        vault = true;
        pawn.SetMove(TdPawn.EMovement.MOVE_Walking);
        pawn.SetLocation(new Vector3(0, 2.1f, 0).ToUnrealPos()); pawn.SetRotation(default); pawn.Controller.SetRotation(default);
        pawn.MoveLedgeLocation = new Vector3(0, 1, 0).ToUnrealPos(); pawn.MoveNormal = Vector3.back.ToUnrealDir(); pawn.MoveLedgeNormal = Vector3.up.ToUnrealDir();
        var move = (TdMove_SpeedVault)pawn.Moves[9];
        move.ActiveVaultType = Enumerable.Range(0, move.VaultTypes.Length).First(i => move.VaultTypes[i].AnimName == "VaultOver");
        move.bVaultOnto = false; move.bEndMoveFalling = true; move.MoveDirection = Vector3.forward.ToUnrealDir(); move.SavedVelocity = Vector3.forward.ToUnrealPos() * 6f;
        move.VaultEndPosition = new Vector3(0, .92f, 1.8f).ToUnrealPos();
        pawn.SetMove(TdPawn.EMovement.MOVE_VaultOver);
        move.VaultState = 3; move.TargetLocation = pawn.Location; move.SavedVelocity = Vector3.forward.ToUnrealPos() * 6f; pawn.Velocity = move.SavedVelocity;
        ((TdPlayerInput)((TdPlayerController)pawn.Controller).PlayerInput).UnityJumpHeld = true;
        move.UpdateVaultMovement();
        Add("VaultJumpStarts", pawn.MovementState == TdPawn.EMovement.MOVE_Falling && pawn.Velocity.Z > 0, "state=" + pawn.MovementState);
        initialY = maximumY = pawn.Location.ToUnityPos().y; startTime = previousTime;
    }
    static void Add(string name, bool passed, string detail)
    {
        report.results.Add(new CollisionProbe.Result { name = name, passed = passed, detail = detail });
        if (!passed) report.failures++;
    }
    static void Finish(Exception e)
    {
        EditorApplication.update -= Tick;
        if (e != null) Add("Harness", false, e.ToString());
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Evidence", Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT") ?? "jump-survival.json"), JsonUtility.ToJson(report, true));
        EditorApplication.Exit(report.failures == 0 ? 0 : 2);
    }
}
