using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MEdge.Core;
using MEdge.Engine;
using MEdge.Source;
using MEdge.TdGame;
using UnityEditor;
using UnityEngine;
using UEActor = MEdge.Engine.Actor;
using CheckResult = MEdge.Source.DecFn.CheckResult;

public static class CollisionProbe
{
    [Serializable] public class Result { public string name; public bool passed; public string detail; }
    [Serializable] public class Report { public List<Result> results = new(); public int failures; }
    static readonly List<GameObject> Objects = new();
    static UWorld World;

    public static void Run()
    {
        var report = new Report();
        try
        {
            // Audio and rendering assets have no bearing on query geometry.
            var physicalMaterial = new PhysicalMaterial();
            ((IDictionary)typeof(Asset).GetField("_physMat", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))["Concrete"] = physicalMaterial;
            ((IDictionary)typeof(Asset).GetField("_Mat", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))["Concrete"] = new Asset.DummyMaterial { PhysMaterial = physicalMaterial };
            var worldObject = new GameObject("CollisionProbeWorld");
            Objects.Add(worldObject);
            World = worldObject.AddComponent<UWorld>();
            World.enabled = false;
            typeof(UWorld).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, World);
            typeof(UWorld).GetProperty("WorldInfo").SetValue(World, new WorldInfo { PhysicsVolume = new DefaultPhysicsVolume() });
            World.DrawDebugTraces = false;

            RunCase(report, "DownwardAlongWallHitsFloor", () =>
            {
                var hit = Trace(new Vector3(.25f, 1f, 0f), new Vector3(0f, -.3f, 0f));
                return (hit.Actor.Name.ToString() == "Floor" && hit.Normal.Z > .9f && hit.Time > .1f, Describe(hit));
            });
            RunCase(report, "ObliqueWallContactHasHorizontalNormal", () =>
            {
                var hit = Trace(new Vector3(.25f, 1f, 0f), new Vector3(.1f, -.3f, 0f));
                return (hit.Actor.Name.ToString() == "Wall" && Math.Abs(hit.Normal.Z) < .01f, Describe(hit));
            });
            RunCase(report, "HorizontalMotionStopsAtWall", () =>
            {
                var hit = Trace(new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f));
                return (hit.Actor.Name.ToString() == "Wall" && hit.Time > .1f && hit.Time < .4f && Math.Abs(hit.Normal.Z) < .01f, Describe(hit));
            });
            RunCase(report, "RepeatedBlockedStepsKeepHeight", () => ExerciseSteps(false));
            RunCase(report, "LowStepRemainsClimbable", () => ExerciseSteps(true));
            RunCase(report, "MoveAwayFromOverlappingWall", () => NoHit(new Vector3(.25f, 1f, 0f), new Vector3(-.2f, 0f, 0f)));
            RunCase(report, "MoveUpAlongOverlappingWall", () => NoHit(new Vector3(.25f, 1f, 0f), new Vector3(0f, .3f, 0f)));
            RunCase(report, "MoveAlongOverlappingFloor", () => NoHit(new Vector3(0f, .87f, 0f), new Vector3(0f, 0f, .3f)));
            RunCase(report, "DownwardOverlappingFloorStillBlocks", () =>
            {
                var hit = Trace(new Vector3(0f, .87f, 0f), new Vector3(0f, -.3f, 0f));
                return (hit.Actor.Name.ToString() == "Floor" && hit.Normal.Z > .9f && hit.Time == 0f, Describe(hit));
            });
            RunCase(report, "MeshWallContactHasHorizontalNormal", () =>
            {
                var wall = GameObject.Find("Wall");
                UnityEngine.Object.DestroyImmediate(wall.GetComponent<BoxCollider>());
                wall.transform.localScale = new Vector3(1f, 4f, 10f);
                wall.AddComponent<MeshCollider>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                Physics.SyncTransforms();
                var hit = Trace(new Vector3(.25f, 1f, 0f), new Vector3(.1f, -.3f, 0f));
                return (hit.Actor.Name.ToString() == "Wall" && Math.Abs(hit.Normal.Z) < .01f, Describe(hit));
            });
        }
        catch (Exception error)
        {
            report.results.Add(new Result { name = "Harness", passed = false, detail = error.ToString() });
            report.failures++;
        }
        finally
        {
            foreach (var obj in Objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence", Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT") ?? "collision-red.json"));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("COLLISION_PROBE " + JsonUtility.ToJson(report));
            EditorApplication.Exit(report.failures == 0 ? 0 : 2);
        }
    }

    static void RunCase(Report report, string name, Func<(bool passed, string detail)> action)
    {
        var floor = MakeBox("Floor", new Vector3(0f, -.5f, 0f), new Vector3(10f, 1f, 10f));
        var wall = MakeBox("Wall", new Vector3(1f, 2f, 0f), new Vector3(1f, 4f, 10f));
        Physics.SyncTransforms();
        try
        {
            var result = action();
            report.results.Add(new Result { name = name, passed = result.passed, detail = result.detail });
            if (!result.passed) report.failures++;
        }
        catch (Exception error)
        {
            report.results.Add(new Result { name = name, passed = false, detail = error.ToString() });
            report.failures++;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(floor);
            UnityEngine.Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
        }
    }

    static GameObject MakeBox(string name, Vector3 center, Vector3 size)
    {
        var obj = new GameObject(name);
        obj.transform.position = center;
        obj.AddComponent<BoxCollider>().size = size;
        return obj;
    }

    static (bool passed, string detail) ExerciseSteps(bool lowStep)
    {
        if (lowStep)
        {
            var wall = GameObject.Find("Wall");
            wall.transform.position = new Vector3(1f, .1f, 0f);
            wall.GetComponent<BoxCollider>().size = new Vector3(1f, .2f, 10f);
            Physics.SyncTransforms();
        }
        var pawn = new TdPawn
        {
            WorldInfo = World.WorldInfo,
            PhysicsVolume = World.WorldInfo.PhysicsVolume,
            HeadVolume = World.WorldInfo.PhysicsVolume,
            Physics = UEActor.EPhysics.PHYS_Walking,
            bCollideWorld = true,
            bCollideActors = false,
            bBlockActors = true,
            MaxStepHeight = 35f,
            MovementState = TdPawn.EMovement.MOVE_Walking,
            Location = new Vector3(lowStep ? 0f : .25f, .9f, 0f).ToUnrealPos()
        };
        pawn.CylinderComponent.CollisionHeight = 88f;
        pawn.Moves[1] = new TdMove_Walking { PawnOwner = pawn };
        pawn.CollisionComponent.ConditionalAttach(null, pawn, MEdge.Core.Object.Matrix.Identity);
        pawn.CylinderComponent.UpdateBounds();
        float minY = pawn.Location.ToUnityPos().y, maxY = minY;
        for (int i = 0; i < 120; i++)
        {
            CheckResult.Clear();
            var delta = new Vector3(.04f, 0f, 0f).ToUnrealPos();
            var hit = new CheckResult(1f);
            World.MoveActor(pawn, delta, pawn.Rotation, 0, ref hit);
            if (hit.Time < 1f)
                pawn.stepUp(new MEdge.Core.Object.Vector(0f, 0f, -1f), delta.SafeNormal(), delta * (1f - hit.Time), ref hit);
            minY = Math.Min(minY, pawn.Location.ToUnityPos().y);
            maxY = Math.Max(maxY, pawn.Location.ToUnityPos().y);
        }
        var end = pawn.Location.ToUnityPos();
        return (lowStep ? end.x > .6f && maxY > 1.05f : maxY - minY < .025f && end.x <= .251f,
            $"end={end} heightRange={minY:F6}..{maxY:F6}");
    }

    static (bool passed, string detail) NoHit(Vector3 start, Vector3 delta)
    {
        var hit = Trace(start, delta, true);
        return (hit.Actor == null && hit.Time == 1f, Describe(hit));
    }

    static unsafe CheckResult Trace(Vector3 start, Vector3 delta, bool allowMissing = false)
    {
        CheckResult.Clear();
        int memory = 0;
        var hit = World.MultiLineCheck(ref memory, (start + delta).ToUnrealPos(), start.ToUnrealPos(),
            new Vector3(.3f, .88f, .3f).ToUnrealPos(), (uint)MEdge.Core.Object.ETraceFlags.TRACE_World, null);
        if (hit == null)
        {
            if (allowMissing) return new CheckResult(1f);
            throw new Exception("Expected a blocking hit");
        }
        return *hit;
    }

    static string Describe(CheckResult hit) => $"actor={hit.Actor?.Name} time={hit.Time:F6} normal=({hit.Normal.X:F4},{hit.Normal.Y:F4},{hit.Normal.Z:F4}) penetrating={hit.bStartPenetrating}";
}
