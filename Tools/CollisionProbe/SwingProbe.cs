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

[InitializeOnLoad]
public static class SwingProbe
{
    static readonly CollisionProbe.Report report = new();
    static TdPlayerPawn pawn;
    static GameObject first, second;
    static int ticks, stage, transfer;
    static float previousTime = -1, phaseTime;
    static float frontFoot;
    static float maxMovingHandError, maxMovingAngle;
    static readonly System.Collections.Generic.List<string> diagnostics = new();
    static SwingProbe() {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("SwingProbe.Pending", false)) {
                SessionState.SetBool("SwingProbe.Pending", false); Start();
            }
        };
    }
    public static void Run() { SessionState.SetBool("SwingProbe.Pending", true); EditorApplication.EnterPlaymode(); }
    static void Start() {
        try {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(40, 1, 40);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Parkour/SwingBar.prefab");
            first = UnityEngine.Object.Instantiate(prefab); first.transform.position = new Vector3(0, 5, 0);
            second = UnityEngine.Object.Instantiate(prefab); second.transform.position = new Vector3(0, 5, 2.4f);
            new GameObject("Spawn").AddComponent<SpawnPoint>().transform.position = Vector3.up;
            new GameObject("MainCamera").AddComponent<UnityEngine.Camera>().tag = "MainCamera";
            var light = new GameObject("Sun").AddComponent<UnityEngine.Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(40, -30, 0);
            Physics.SyncTransforms(); UWorld.EnsureStart(); pawn = UWorld.Instance.WorldInfo._allActors.OfType<TdPlayerPawn>().First();
            EditorApplication.update += Tick;
        } catch (Exception e) { Finish(e); }
    }
    static void Tick() {
        try {
            if (!EditorApplication.isPlaying || previousTime == pawn.WorldInfo.TimeSeconds) return;
            previousTime = pawn.WorldInfo.TimeSeconds; if (++ticks < 30) return;
            var swing = (TdMove_Swing)pawn.Moves[60];
            if (stage == 0) {
                var controller = (TdPlayerController)pawn.Controller; var input = (TdPlayerInput)controller.PlayerInput;
                controller.bDuck = 1; controller.bIgnoreButtonInput = 1; input.StopCrouch();
                Add("ReleaseCrouchDuringIgnoredInput", controller.bDuck == 0, "bDuck=" + controller.bDuck);
                controller.bIgnoreButtonInput = 0; controller.bDuck = 0;
                Add("FallClipExistsInBothSets", pawn.Mesh1p.FindAnimSequence("JumpSlow") != null && pawn.Mesh3p.FindAnimSequence("JumpSlow") != null, "JumpSlow available for both cameras");
                Add("JumpAirKeeps1PAndResolves3P", pawn.Mesh1p.FindAnimSequence("JumpAir")?.SequenceName == "JumpAir" && pawn.Mesh3p.FindAnimSequence("JumpAir")?.SequenceName == "JumpSlow", "Imported 1P clip preserved; missing 3P counterpart resolved");
                diagnostics.Add("Clips: " + string.Join(",", Resources.LoadAll<AnimationClip>("AS_C1P_Unarmed").Where(c => c.name.Contains("swing", StringComparison.OrdinalIgnoreCase) || c.name.Contains("jump", StringComparison.OrdinalIgnoreCase)).Select(c => c.name)));
                swing.Volume = (TdSwingVolume)first.GetComponent<VolumeProxy>().UnrealVolume;
                pawn.SetMove(TdPawn.EMovement.MOVE_Falling); pawn.SetLocation(new Vector3(0, 3.8f, -.6f).ToUnrealPos()); pawn.SetRotation(default);
                pawn.Velocity = new Vector3(0, 2, 4).ToUnrealPos();
                float length = swing.Volume.UnityGripHalfLength;
                swing.Volume.UnityGripHalfLength = 20f;
                Add("TooShortBarCannotGrab", !swing.CanDoMove(), "20cm half length cannot fit two hands"); swing.Volume.UnityGripHalfLength = length;
                pawn.SetLocation(new Vector3(0, 3.8f, -2f).ToUnrealPos());
                Add("DistantBarCannotMagnetize", !swing.CanDoMove(), "grip farther than physical reach");
                pawn.SetLocation(new Vector3(0, 3.8f, -.6f).ToUnrealPos()); pawn.Velocity = new Vector3(0, 5, 1).ToUnrealPos();
                pawn.SetMove(TdPawn.EMovement.MOVE_Swing);
                Add("EntryKeepsTangentialVelocitySign", swing.SwingVelocity < 0, "angular velocity=" + swing.SwingVelocity);
                Enter(0); stage = 1; return;
            }
            if (stage == 1 || stage == 3) {
                if (previousTime - phaseTime < 2f) { if (pawn.MovementState == TdPawn.EMovement.MOVE_Swing) { swing.SwingAngle = 0; swing.SwingVelocity = 0; } return; }
                var label = stage == 1 ? "Center" : "Edge";
                Add(label + "EntersSwing", pawn.MovementState == TdPawn.EMovement.MOVE_Swing, "state=" + pawn.MovementState);
                Hands(label);
                if (stage == 1) {
                    var sources = first.GetComponentsInChildren<AudioSource>();
                    Asset.UScriptToUnity.TryGetValue(pawn.Mesh1p.SkeletalMesh, out var mesh);
                    var playing = UnityEngine.Object.FindObjectsByType<AudioSource>().Where(a => a.transform.IsChildOf(((SkinnedMeshRenderer)mesh).transform.parent) && a.isPlaying).ToArray();
                    Add("IdleSwingLoopQuiet", playing.Where(a => a.clip && (a.clip.name.Contains("Swing") || a.clip.name.Contains("CharacterRunWind"))).All(a => a.volume < .02f), string.Join(";", playing.Select(a => a.clip.name + "=" + a.volume)));
                    phaseTime = previousTime; stage = 8; return;
                }
                swing.SwingAngle = .35f; swing.SwingVelocity = 3f;
                // Start an unobstructed, real flight from the centre of the first bar.
                Enter(0); stage = 4; return;
            }
            if (stage == 8 || stage == 9) {
                swing.SwingAngle = stage == 8 ? .6f : -.6f; swing.SwingVelocity = 0;
                if (previousTime - phaseTime < .5f) return;
                Hands(stage == 8 ? "FrontArc" : "BackArc");
                Asset.UScriptToUnity.TryGetValue(pawn.Mesh1p.SkeletalMesh, out var mesh);
                float foot = ((SkinnedMeshRenderer)mesh).transform.parent.GetComponentsInChildren<Transform>().First(b => b.name == "LeftFoot").position.z;
                if (stage == 8) { frontFoot = foot; stage = 9; phaseTime = previousTime; return; }
                Add("LegsFollowSwingArc", Mathf.Abs(frontFoot - foot) > .3f, "front foot=" + frontFoot + "; back foot=" + foot);
                swing.SwingAngle = 0; swing.SwingVelocity = 4f; stage = 11; phaseTime = previousTime; return;
            }
            if (stage == 11) {
                Asset.UScriptToUnity.TryGetValue(pawn.Mesh1p.SkeletalMesh, out var mesh);
                var bones = ((SkinnedMeshRenderer)mesh).transform.parent.GetComponentsInChildren<Transform>();
                if (previousTime - phaseTime > .2f) {
                    foreach (var name in new[] { "LeftHandMiddle1", "RightHandMiddle1" }) {
                        var contact = first.transform.InverseTransformPoint(bones.First(b => b.name == name).position);
                        maxMovingHandError = Mathf.Max(maxMovingHandError, new Vector2(contact.y, contact.z).magnitude);
                    }
                    maxMovingAngle = Mathf.Max(maxMovingAngle, Mathf.Abs(swing.SwingAngle));
                }
                if (previousTime - phaseTime < 3f) return;
                Add("HandsRemainAttachedWhileMoving", pawn.MovementState == TdPawn.EMovement.MOVE_Swing && maxMovingHandError < .08f && maxMovingAngle > .6f, "maximum palm distance=" + maxMovingHandError + "; arc=" + maxMovingAngle);
                Add("ShimmyStopsBeforeBarEnd", !swing.CanShimmy(160f), "finite bar reserves room for both hands");
                Enter(1.45f); stage = 3; return;
            }
            if (stage == 4) {
                if (previousTime - phaseTime < .4f) return;
                swing.SwingAngle = .35f; swing.SwingVelocity = 3f;
                unsafe { MEdge.Core.Object.Vector location; swing.GetPawnLocation(&location, swing.SwingAngle); pawn.SetLocation(location); }
                pawn.HandleMoveAction(TdPawn.EMovementAction.MA_Jump);
                diagnostics.Add("Launch: " + pawn.Velocity.ToUnityPos());
                Add("Transfer" + transfer + "LaunchesFromBar", pawn.MovementState == TdPawn.EMovement.MOVE_SwingJump, "state=" + pawn.MovementState);
                Add("Transfer" + transfer + "ReleasesHandIK", pawn.LeftHandWorldIKController.StrengthTarget == 0 && pawn.RightHandWorldIKController.StrengthTarget == 0, "both hand controllers blend out for flight");
                phaseTime = previousTime; stage = 5; return;
            }
            if (stage == 5) {
                diagnostics.Add("Flight " + (previousTime - phaseTime).ToString("F3") + " " + pawn.Location.ToUnityPos() + " " + pawn.Velocity.ToUnityPos() + " " + pawn.MovementState);
                if (pawn.MovementState == TdPawn.EMovement.MOVE_Swing && swing.Volume == second.GetComponent<VolumeProxy>().UnrealVolume) {
                    Add("Transfer" + transfer + "CatchesNextBar", true, "elapsed=" + (previousTime - phaseTime) + "; angular velocity=" + swing.SwingVelocity); phaseTime = previousTime; stage = 6; return;
                }
                if (previousTime - phaseTime > 2f) { Add("Transfer" + transfer + "CatchesNextBar", false, "state=" + pawn.MovementState); Finish(null); }
            }
            if (stage == 6 && previousTime - phaseTime > .4f) {
                Hands("NextBar" + transfer);
                diagnostics.Add("3P IK: " + (pawn.Mesh3p.FindSkelControl("LeftHandWorldIKController") != null));
                if (transfer == 0) { transfer++; second.transform.position = new Vector3(0, 5, 3.2f); Physics.SyncTransforms(); Enter(0); stage = 4; return; }
                if (transfer == 1) { transfer++; second.transform.position = new Vector3(0, 5, 2.4f); second.transform.rotation = Quaternion.Euler(0, 35, 0); Physics.SyncTransforms(); Enter(0); stage = 4; return; }
                // Actual native auto-move detection at a ledge beside the grip.
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 3.5f, .6f); wall.transform.localScale = new Vector3(3, 3, .2f); Physics.SyncTransforms();
                Enter(0); swing.Volume = (TdSwingVolume)first.GetComponent<VolumeProxy>().UnrealVolume; pawn.SetMove(TdPawn.EMovement.MOVE_Swing);
                pawn.Velocity = Vector3.forward.ToUnrealPos() * 4; swing.CheckAutoMoves();
                Add("AttachedSwingCannotBecomeVault", pawn.MovementState == TdPawn.EMovement.MOVE_Swing, "state=" + pawn.MovementState);
                Finish(null);
            }
        } catch (Exception e) { Finish(e); }
    }
    static void Enter(float x) {
        pawn.SetMove(TdPawn.EMovement.MOVE_Falling); pawn.ActiveMovementVolume = null;
        // Leave the old physics volume before a new approach to the same bar.
        pawn.SetLocation(new Vector3(-5, 1, -5).ToUnrealPos()); pawn.SetZone(false, true);
        pawn.SetRotation(default); pawn.Controller.SetRotation(default);
        pawn.SetLocation(new Vector3(x, 3.8f, -.6f).ToUnrealPos()); pawn.Velocity = new Vector3(0, 2, 4).ToUnrealPos(); pawn.Acceleration = default;
        pawn.SetZone(false, true); phaseTime = previousTime;
        diagnostics.Add("Approach x=" + x + "; zone=" + pawn.PhysicsVolume.GetType().Name + "; active=" + pawn.ActiveMovementVolume?.GetType().Name);
    }
    static void Hands(string label) {
        Asset.UScriptToUnity.TryGetValue(pawn.Mesh1p.SkeletalMesh, out var mesh);
        var render = (SkinnedMeshRenderer)mesh;
        var bones = render.transform.parent.GetComponentsInChildren<Transform>();
        var swing = (TdMove_Swing)pawn.Moves[60];
        Vector3 center = swing.SwingLocation.ToUnityPos();
        foreach (var name in new[] { "LeftHand", "RightHand" }) {
            var bone = bones.First(b => b.name == name); var bar = swing.Volume == second.GetComponent<VolumeProxy>().UnrealVolume ? second : first;
            var local = bar.transform.InverseTransformPoint(bone.position);
            var contact = bar.transform.InverseTransformPoint(bones.First(b => b.name == name + "Middle1").position);
            float radial = new Vector2(contact.y, contact.z).magnitude;
            Add(label + name + "TouchesFiniteBar", radial < .05f && Mathf.Abs(contact.x) < 1.5f, "wrist=" + local + "; finger=" + contact + "; grip=" + center);
        }
        var camera = new GameObject("Observer").AddComponent<UnityEngine.Camera>(); camera.enabled = false;
        camera.transform.position = center + new Vector3(4, 1, -3); camera.transform.LookAt(center - Vector3.up * .6f);
        var target = new RenderTexture(640, 360, 24); var texture = new UnityEngine.Texture2D(640, 360, TextureFormat.RGB24, false);
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "Evidence", "swing-" + label + ".png"), texture.EncodeToPNG());
        RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
    }
    static void Add(string name, bool passed, string detail) { report.results.Add(new CollisionProbe.Result { name = name, passed = passed, detail = detail }); if (!passed) report.failures++; }
    static void Finish(Exception e) {
        EditorApplication.update -= Tick; if (e != null) Add("Harness", false, e.ToString());
        var name = Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT") ?? "swing.json";
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Evidence", name), JsonUtility.ToJson(report, true));
        File.WriteAllLines(Path.Combine(Directory.GetCurrentDirectory(), "Evidence", name + ".timeline.txt"), diagnostics);
        EditorApplication.Exit(report.failures == 0 ? 0 : 2);
    }
}
