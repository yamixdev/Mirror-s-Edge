using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MEdge.Core;
using MEdge.Engine;
using MEdge.Source;
using UnityEditor;
using UnityEngine;
using CheckResult = MEdge.Source.DecFn.CheckResult;
using UObject = MEdge.Core.Object;

public sealed class ProbeVolume : VolumeProxy<Volume>
{
    public int SyncCount;
    public Action OnSync;
    protected override void SyncVolume(Volume volume) { SyncCount++; OnSync?.Invoke(); }
}

[InitializeOnLoad]
public static class ReviewProbe
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly List<UnityEngine.Object> Owned = new();
    static UWorld World;
    static int InitialGraphCount;

    static ReviewProbe()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("ReviewProbe.Pending", false))
            {
                SessionState.SetBool("ReviewProbe.Pending", false);
                RunChecks();
            }
        };
    }

    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            SessionState.SetBool("ReviewProbe.Pending", true);
            EditorApplication.EnterPlaymode();
            return;
        }
        RunChecks();
    }

    static void RunChecks()
    {
        var report = new CollisionProbe.Report();
        try
        {
            var material = new PhysicalMaterial();
            ((IDictionary)typeof(Asset).GetField("_physMat", PrivateStatic).GetValue(null))["Concrete"] = material;
            ((IDictionary)typeof(Asset).GetField("_Mat", PrivateStatic).GetValue(null))["Concrete"] = new Asset.DummyMaterial { PhysMaterial = material };
            World = MakeWorld("ReviewProbeWorld");
            InitialGraphCount = Graphs.Count;
            Case(report, "DestroyedVolumeUnregistersAndWorldTicks", () =>
            {
                var proxy = Make("Volume").AddComponent<ProbeVolume>();
                _ = proxy.UnrealVolume;
                Require(World.LowFrequencyUpdate.Count == 1, "Volume did not register");
                UnityEngine.Object.DestroyImmediate(proxy.gameObject);
                Require(World.LowFrequencyUpdate.Count == 0, "Destroyed volume callback remains registered");
                TickWorld();
                Require(World.WorldInfo.TimeSeconds > 0f, "World did not advance after destruction");
            });
            Case(report, "VolumeSelfRemovalDuringWorldUpdate", () =>
            {
                var proxy = Make("SelfRemovingVolume").AddComponent<ProbeVolume>();
                _ = proxy.UnrealVolume;
                proxy.OnSync = () => UnityEngine.Object.DestroyImmediate(proxy.gameObject);
                TickWorld();
                Require(World.LowFrequencyUpdate.Count == 0, "Self-removal left a callback");
                TickWorld();
            });
            Case(report, "CallbackRemovalPreservesNextVolume", () =>
            {
                var first = Make("FirstVolume").AddComponent<ProbeVolume>();
                var second = Make("SecondVolume").AddComponent<ProbeVolume>();
                _ = first.UnrealVolume; _ = second.UnrealVolume;
                int previous = second.SyncCount;
                first.OnSync = () => UnityEngine.Object.DestroyImmediate(first.gameObject);
                TickWorld(); TickWorld(); TickWorld(); TickWorld();
                Require(second.SyncCount == previous + 3, "Next registered volume was skipped");
            });
            Case(report, "DestroyAllVolumesDuringWorldUpdate", () =>
            {
                var volumes = new List<ProbeVolume>();
                for (int i = 0; i < 20; i++)
                {
                    var proxy = Make("RemovedTogether" + i).AddComponent<ProbeVolume>();
                    _ = proxy.UnrealVolume; volumes.Add(proxy);
                }
                volumes[0].OnSync = () => { foreach (var volume in volumes) UnityEngine.Object.DestroyImmediate(volume.gameObject); };
                TickWorld();
                Require(World.LowFrequencyUpdate.Count == 0, "Destroyed volumes left callbacks");
                TickWorld();
            });
            Case(report, "NonLoopingAnimationUsesLastPose", () => CheckAnimation(false));
            Case(report, "LoopingClipKeepsLastPoseForNonLoopingRequest", () => CheckAnimation(true));
            Case(report, "DestroyedAnimatorLeavesNoGraphCacheEntry", () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    var obj = Make("Animator" + i);
                    var graph = AnimationCleanup.GetGraph(obj.AddComponent<Animator>());
                    Require(graph.graph.IsValid(), "New graph is invalid");
                    UnityEngine.Object.DestroyImmediate(obj);
                    Require(!graph.graph.IsValid() && Graphs.Count == InitialGraphCount, "Destroyed animator retained in graph cache");
                }
            });
            Case(report, "RemovedCleanupCanRecreateGraph", () =>
            {
                var obj = Make("ReusableAnimator");
                var animator = obj.AddComponent<Animator>();
                var first = AnimationCleanup.GetGraph(animator);
                UnityEngine.Object.DestroyImmediate(obj.GetComponent<AnimationCleanup>());
                var second = AnimationCleanup.GetGraph(animator);
                Require(second.graph.IsValid() && !first.graph.IsValid(), "Cache returned a destroyed graph");
                Require(Graphs.Count == InitialGraphCount + 1, "Graph recreation duplicated cache ownership");
            });
            Case(report, "ExternallyDestroyedGraphIsRebuilt", () =>
            {
                var animator = Make("ExternallyDestroyedGraph").AddComponent<Animator>();
                var first = AnimationCleanup.GetGraph(animator);
                first.graph.Destroy();
                Require(AnimationCleanup.GetGraph(animator).graph.IsValid(), "Invalid cached graph was reused");
            });
            foreach (int count in new[] { 16, 17, 65, 129 })
            {
                int amount = count;
                Case(report, "RaycastAllHits" + amount, () => CheckDenseTrace(amount, false));
                Case(report, "BoxCastAllHits" + amount, () => CheckDenseTrace(amount, true));
            }
            Case(report, "OverlapAll65Colliders", CheckDenseOverlap);
            Case(report, "PhysicsVolumePointQueryIncludesOnlyVolumes", CheckVolumePointQuery);
            Case(report, "RotatedVolumeContainsPointAndDoesNotBlock", CheckRotatedVolumePoint);
            Case(report, "DestroyVolumeAfterWorldDoesNotCreateWorld", () =>
            {
                var previousWorld = World;
                var detachedWorld = MakeWorld("WorldDestroyedFirst");
                var proxy = Make("SurvivingVolume").AddComponent<ProbeVolume>();
                _ = proxy.UnrealVolume;
                UnityEngine.Object.DestroyImmediate(detachedWorld.gameObject);
                UnityEngine.Object.DestroyImmediate(proxy.gameObject);
                SetWorld(previousWorld);
                Require(UnityEngine.Object.FindObjectsByType<UWorld>().Length == 1, "Destroying volume created another world");
            });
        }
        catch (Exception error)
        {
            report.results.Add(new CollisionProbe.Result { name = "Harness", passed = false, detail = error.ToString() });
            report.failures++;
        }
        finally
        {
            Cleanup();
            if (World != null) UnityEngine.Object.DestroyImmediate(World.gameObject);
            var file = Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT") ?? "review-red.json";
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence", file)), JsonUtility.ToJson(report, true));
            Debug.Log("REVIEW_PROBE " + JsonUtility.ToJson(report));
            EditorApplication.Exit(report.failures == 0 ? 0 : 2);
        }
    }

    static void Case(CollisionProbe.Report report, string name, Action action)
    {
        World.LowFrequencyUpdate.Clear();
        typeof(UWorld).GetField("_lowFreqIndx", PrivateInstance).SetValue(World, 0);
        ResetBuffer("_hitCache"); ResetBuffer("_colliderCache"); ResetBuffer("_sortCache");
        try
        {
            action();
            report.results.Add(new CollisionProbe.Result { name = name, passed = true, detail = "Behavior verified" });
        }
        catch (Exception error)
        {
            report.failures++;
            report.results.Add(new CollisionProbe.Result { name = name, passed = false, detail = (error.InnerException ?? error).ToString() });
        }
        finally { Cleanup(); SetWorld(World); World.LowFrequencyUpdate.Clear(); }
    }

    static void Cleanup()
    {
        for (int i = Owned.Count - 1; i >= 0; i--) if (Owned[i] != null) UnityEngine.Object.DestroyImmediate(Owned[i]);
        Owned.Clear(); Physics.SyncTransforms();
    }
    static GameObject Make(string name) { var obj = new GameObject(name); Owned.Add(obj); return obj; }
    static UWorld MakeWorld(string name)
    {
        var world = new GameObject(name).AddComponent<UWorld>();
        world.enabled = false; SetWorld(world);
        typeof(UWorld).GetProperty("WorldInfo").SetValue(world, new WorldInfo { PhysicsVolume = new DefaultPhysicsVolume() });
        world.DrawDebugTraces = false;
        return world;
    }
    static void SetWorld(UWorld world) => typeof(UWorld).GetField("_instance", PrivateStatic).SetValue(null, world);
    static void TickWorld() => typeof(UWorld).GetMethod("Update", PrivateInstance).Invoke(World, null);
    static IDictionary Graphs => (IDictionary)typeof(AnimationCleanup).GetField("AllGraphs", PrivateStatic).GetValue(null);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void ResetBuffer(string name)
    {
        var field = typeof(UWorld).GetField(name, PrivateStatic);
        field.SetValue(null, Array.CreateInstance(field.FieldType.GetElementType(), 16));
    }

    static void CheckAnimation(bool importedLoop)
    {
        var root = Make("SyntheticAnimation");
        var bone = Make("bone"); bone.transform.parent = root.transform;
        var animator = root.AddComponent<Animator>();
        var avatar = AvatarBuilder.BuildGenericAvatar(root, ""); Owned.Add(avatar); animator.avatar = avatar;
        var clip = new AnimationClip { name = "PoseProbe", frameRate = 30f }; Owned.Add(clip);
        clip.SetCurve("bone", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 1f, 1f, 3f));
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = importedLoop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var sequence = new AnimSequence { SequenceName = "PoseProbe", SequenceLength = 2f, NumFrames = 31 };
        var set = new AnimSet(); set.TrackBoneNames.Add("bone"); set.Sequences.Add(sequence);
        typeof(UWorld).GetNestedType("PawnLink", BindingFlags.NonPublic).GetMethod("FixRefsForAnimation", PrivateStatic)
            .Invoke(null, new object[] { new[] { clip }, set, animator });
        AnimNode.BoneAtom atom = default;
        sequence.GetBoneAtom(ref atom, 0, 2f, false, false);
        Require(Math.Abs(atom.Translation.X + 300f) < .1f, "Non-looping final pose was " + atom.Translation.X + ", expected -300");
        sequence.GetBoneAtom(ref atom, 0, 2f, true, false);
        Require(Math.Abs(atom.Translation.X + 100f) < .1f, "Looping boundary did not return initial pose");
        Require(clip.wrapMode == WrapMode.Default, "Pose sampling did not restore clip wrap mode");
    }

    static unsafe void CheckDenseTrace(int count, bool boxCast)
    {
        for (int i = 0; i < count; i++)
        {
            var obj = Make("Obstacle" + i); obj.transform.position = new Vector3(i, 2f, 0f);
            obj.AddComponent<BoxCollider>().size = new Vector3(.2f, 1f, 1f);
        }
        Physics.SyncTransforms();
        var start = new Vector3(-2f, 2f, 0f); var delta = new Vector3(count + 3f, 0f, 0f);
        var extent = boxCast ? new Vector3(.1f, .1f, .1f) : default;
        var all = boxCast ? Physics.BoxCastAll(start, extent, delta.normalized, Quaternion.identity, delta.magnitude) : Physics.RaycastAll(start, delta.normalized, delta.magnitude);
        CheckResult.Clear(); int mem = 0, actual = 0; float lastTime = -1f;
        for (var hit = World.MultiLineCheck(ref mem, (start + delta).ToUnrealPos(), start.ToUnrealPos(), extent.ToUnrealPos(), (uint)UObject.ETraceFlags.TRACE_World, null); hit != null; hit = hit->Next)
        {
            Require(hit->Time >= lastTime, "Hit order is not sorted by distance"); lastTime = hit->Time; actual++;
        }
        Require(all.Length == count, "Fixture did not hit all obstacles");
        Require(actual == all.Length, "Returned " + actual + " hits, expected " + all.Length);
    }

    static unsafe void CheckDenseOverlap()
    {
        for (int i = 0; i < 65; i++)
        {
            var obj = Make("Overlap" + i); obj.transform.position = new Vector3((i % 9 - 4) * .12f, 2f, (i / 9 - 3) * .12f);
            obj.AddComponent<BoxCollider>().size = Vector3.one * .04f;
        }
        Physics.SyncTransforms();
        var center = new Vector3(0f, 2f, 0f); var extent = Vector3.one;
        var full = Physics.OverlapBox(center, extent);
        CheckResult.Clear(); int mem = 0, actual = 0;
        for (var hit = World.ActorPointCheck(ref mem, center.ToUnrealPos(), extent.ToUnrealPos(), (uint)UObject.ETraceFlags.TRACE_World); hit != null; hit = hit->Next) actual++;
        Require(full.Length == 65, "Fixture did not overlap all obstacles");
        Require(actual == full.Length, "Returned " + actual + " overlaps, expected " + full.Length);
    }

    static unsafe void CheckVolumePointQuery()
    {
        var volume=Make("PointVolume");var trigger=volume.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=Vector3.one*2;
        var proxy=volume.AddComponent<UnityTdSwingVolume>();
        var solid=Make("SolidAtSamePoint");solid.AddComponent<BoxCollider>().size=Vector3.one;
        Physics.SyncTransforms();CheckResult.Clear();int mem=0,count=0;
        for(var hit=World.ActorPointCheck(ref mem,default,default,(uint)UObject.ETraceFlags.TRACE_PhysicsVolumes);hit!=null;hit=hit->Next) {
            Require(hit->Actor==proxy.UnrealVolume,"Physics volume query returned solid geometry");count++;
        }
        Require(count==1,"Point inside a volume was not detected");
        CheckResult.Clear();mem=0;
        Require(World.ActorPointCheck(ref mem,new Vector3(3,0,0).ToUnrealPos(),default,(uint)UObject.ETraceFlags.TRACE_PhysicsVolumes)==null,"Point outside volume returned a hit");
    }

    static unsafe void CheckRotatedVolumePoint()
    {
        var root=Make("RotatedPointVolume");root.transform.position=new Vector3(4,2,3);root.transform.rotation=Quaternion.Euler(0,45,0);
        var trigger=root.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(4,2,.2f);
        var proxy=root.AddComponent<UnityTdBalanceWalkVolume>();Physics.SyncTransforms();CheckResult.Clear();int mem=0;
        var inside=root.transform.TransformPoint(new Vector3(1,0,0));
        var hit=World.ActorPointCheck(ref mem,inside.ToUnrealPos(),default,(uint)UObject.ETraceFlags.TRACE_PhysicsVolumes);
        Require(hit!=null,"Rotated volume point missing");
        Require(!hit->Component.BlockActors,"Trigger was mapped as a solid blocker");
        CheckResult result=default;
        Require(!World.PointCheck(hit->Component,ref result,inside.ToUnrealPos(),default,default),"Encompasses rejected inside point");
        var outside=root.transform.TransformPoint(new Vector3(0,0,.3f));
        Require(World.PointCheck(hit->Component,ref result,outside.ToUnrealPos(),default,default),"Encompasses accepted point outside rotated collider");
    }
}
