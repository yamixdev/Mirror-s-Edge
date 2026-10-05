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
using UObject = MEdge.Core.Object;
using Material = UnityEngine.Material;
using Texture2D = UnityEngine.Texture2D;

[InitializeOnLoad]
public static class GameplayHandsProbe
{
    static int frames;
    static TdPlayerPawn pawn;
    static GameObject wall;
    static readonly CollisionProbe.Report report = new();
    static GameplayHandsProbe()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("GameplayHandsProbe.Pending", false)) {
                SessionState.SetBool("GameplayHandsProbe.Pending", false);
                Start();
            }
        };
    }
    public static void Run() { SessionState.SetBool("GameplayHandsProbe.Pending", true); EditorApplication.EnterPlaymode(); }
    static void Start()
    {
        try {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0,-.5f,0); floor.transform.localScale = new Vector3(20,1,20);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0,1.5f,.6f); wall.transform.localScale = new Vector3(10,3,.4f);
            var spawn = new GameObject("Spawn").AddComponent<SpawnPoint>();
            spawn.transform.position = Vector3.up;
            var camera = new GameObject("Main Camera").AddComponent<UnityEngine.Camera>();
            camera.tag = "MainCamera";
            UWorld.EnsureStart();
            pawn = UWorld.Instance.WorldInfo._allActors.OfType<TdPlayerPawn>().First();
            EditorApplication.update += Tick;
        } catch (Exception e) { Finish(e); }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try {
            if (++frames == 30) {
                pawn.MoveNormal = new Vector3(0,0,-1).ToUnrealDir();
                pawn.MoveLedgeNormal = Vector3.up.ToUnrealDir();
                pawn.MoveLedgeLocation = new Vector3(0,3,.4f).ToUnrealPos();
                var grab = (TdMove_Grab)pawn.Moves[3];
                float extent = grab.CalculateRelativeExtent(pawn.CylinderComponent.CollisionRadius);
                pawn.SetLocation(pawn.MoveLedgeLocation + pawn.MoveNormal * (pawn.CylinderComponent.CollisionRadius + extent) - new UObject.Vector(0,0,92.8f));
                pawn.SetMove(TdPawn.EMovement.MOVE_Grabbing, false, false);
            }
            if (frames < 120) return;
            EditorApplication.update -= Tick;
            var camera = UnityEngine.Camera.main;
            UWorld.Instance.enabled = false;
            Asset.UScriptToUnity.TryGetValue(pawn.Mesh1p.SkeletalMesh, out var unityMesh);
            var renderer = (SkinnedMeshRenderer)unityMesh;
            Asset.UScriptToUnity.TryGetValue(pawn.Mesh3p.SkeletalMesh, out var unityBody);
            var body = (SkinnedMeshRenderer)unityBody;
            // This compares the skinned 1P geometry to an independent baked reference.
            // Disable camera routing here so it cannot re-enable the actual mesh in the reference render.
            body.transform.parent.GetComponent<PlayerBodyVisibility>().enabled = false;
            body.enabled = false;
            var bones = renderer.transform.parent.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"Evidence/gameplay-hands-geometry.txt"),
                $"state={pawn.MovementState} camera={camera.transform.position} rot={camera.transform.rotation.eulerAngles} bounds={renderer.bounds}\n" +
                string.Join("\n", new[]{"EyeJoint","LeftHand","RightHand"}.Select(n=>n+"="+bones[n].position+" depth="+camera.transform.InverseTransformPoint(bones[n].position))));
            var red = new Material(Shader.Find("Unlit/Color")) { color = Color.red };
            renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => red).ToArray();
            var black = new Material(Shader.Find("Unlit/Color")) { color = Color.black };
            wall.GetComponent<Renderer>().sharedMaterial = black;
            var target = new RenderTexture(640,360,24);
            camera.targetTexture = target; camera.aspect=16f/9f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.blue;
            var texture = new Texture2D(640,360,TextureFormat.RGB24,false);
            var rotation = camera.transform.rotation;
            var baked = new Mesh();
            renderer.BakeMesh(baked);
            var reference = new GameObject("Baked pose reference");
            reference.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
            reference.transform.localScale = renderer.transform.lossyScale;
            reference.AddComponent<MeshFilter>().sharedMesh = baked;
            var referenceRenderer = reference.AddComponent<MeshRenderer>();
            referenceRenderer.sharedMaterials = renderer.sharedMaterials;
            referenceRenderer.enabled = false;
            foreach(float pitch in new[]{-25f,-15f,0f})
            foreach(float yaw in new[]{0f,45f})
            {
                camera.transform.rotation = Quaternion.AngleAxis(yaw,Vector3.up)*rotation*Quaternion.Euler(pitch,0,0);
                string name=$"GrabPitch{pitch}-Yaw{yaw}";
                renderer.enabled = true; referenceRenderer.enabled = false;
                int pixels=Capture(camera, texture, target, name);
                renderer.enabled = false; referenceRenderer.enabled = true;
                int expected=Capture(camera, texture, target, name+"-reference");
                bool passed = Math.Abs(pixels-expected) <= Math.Max(5, expected*.02f);
                if(yaw==0 && pitch<0) passed &= expected>1000;
                if(!passed) report.failures++;
                report.results.Add(new CollisionProbe.Result{name=name,passed=passed,detail=$"actual pixels={pixels}; baked reference={expected}; camera near={camera.nearClipPlane}"});
            }
            if(pawn.MovementState!=TdPawn.EMovement.MOVE_Grabbing) throw new Exception("Pawn left grab state");
            RenderTexture.active=null;
            Finish(null);
        } catch (Exception e) { Finish(e); }
    }
    static int Capture(UnityEngine.Camera camera, Texture2D texture, RenderTexture target, string name) {
        camera.Render(); RenderTexture.active=target;
        texture.ReadPixels(new Rect(0,0,640,360),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"Evidence",name+".png"),texture.EncodeToPNG());
        return texture.GetPixels32().Count(c=>c.r>128&&c.g<30&&c.b<30);
    }
    static void Finish(Exception e) {
        EditorApplication.update -= Tick;
        if(e!=null) { report.failures++;report.results.Add(new CollisionProbe.Result{name="GameplayHandsSetup",passed=false,detail=e.ToString()}); }
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"Evidence",Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT")??"gameplay-hands.json"),JsonUtility.ToJson(report,true));
        EditorApplication.Exit(report.failures==0?0:2);
    }
}
