using System;
using System.IO;
using System.Linq;
using MEdge;
using MEdge.Engine;
using MEdge.Source;
using MEdge.TdGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Camera = UnityEngine.Camera;
using Texture2D = UnityEngine.Texture2D;
using Material = UnityEngine.Material;
using Light = UnityEngine.Light;

[InitializeOnLoad]
public static class FullBodyProbe
{
    static readonly CollisionProbe.Report report = new();
    static TdPlayerPawn pawn;
    static int frames;
    static SkinnedMeshRenderer full, upper, lower;
    static Camera owner, external;
    static RenderTexture target;
    static Texture2D texture;
    static FullBodyProbe() {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("FullBodyProbe.Pending", false)) {
                SessionState.SetBool("FullBodyProbe.Pending", false); Start();
            }
        };
    }
    public static void Run() { SessionState.SetBool("FullBodyProbe.Pending", true); EditorApplication.EnterPlaymode(); }
    static void Start() {
        try {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0,-.5f,0); floor.transform.localScale = new Vector3(20,1,20);
            new GameObject("Spawn").AddComponent<SpawnPoint>().transform.position = Vector3.up;
            owner = new GameObject("Main Camera").AddComponent<Camera>(); owner.tag = "MainCamera";
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40,-30,0);
            UWorld.EnsureStart(); pawn = UWorld.Instance.WorldInfo._allActors.OfType<TdPlayerPawn>().First();
            EditorApplication.update += Tick;
        } catch (Exception e) { Finish(e); }
    }
    static SkinnedMeshRenderer Renderer(MEdge.Engine.SkeletalMesh mesh) {
        Asset.UScriptToUnity.TryGetValue(mesh, out var obj); return (SkinnedMeshRenderer)obj;
    }
    static void Tick() {
        if (!EditorApplication.isPlaying || ++frames < 90) return;
        EditorApplication.update -= Tick;
        try {
            UWorld.Instance.enabled = false;
            full = Renderer(pawn.Mesh3p.SkeletalMesh); upper = Renderer(pawn.Mesh1p.SkeletalMesh); lower = Renderer(pawn.Mesh1pLowerBody.SkeletalMesh);
            Add("FullModelHasFaceTexture", full.sharedMaterials.Any(m=>m.name=="MI_Faith_Lowres_Face" && m.mainTexture), string.Join(",",full.sharedMaterials.Select(m=>m.name)));
            Add("FullModelHasClothesAndTeethTextures", new[]{"MI_Faith_Lowres_Upper","MI_Faith_Lowres_Lower","faithTeeth"}.All(n=>full.sharedMaterials.Any(m=>m.name==n && m.mainTexture)), "three textured materials");
            Add("FullModelContainsHead", full.bones.Any(b=>b.name.Contains("Head",StringComparison.OrdinalIgnoreCase)), string.Join(",",full.bones.Where(b=>b.name.Contains("Head",StringComparison.OrdinalIgnoreCase)).Select(b=>b.name)));
            Add("FullBodyRootKeepsNativeScale", Vector3.Distance(full.rootBone.lossyScale,Vector3.one)<.01f, "root scale="+full.rootBone.lossyScale);
            float height=full.bones.First(b=>b.name=="Head").position.y-full.bones.First(b=>b.name=="LeftFoot").position.y;
            Add("FullBodyMatchesHumanHeight", height>1.2f && height<1.9f, "head-to-foot height="+height);
            external = new GameObject("External Camera").AddComponent<Camera>(); external.enabled=false;
            var baked = new Mesh(); full.BakeMesh(baked);
            var center = full.transform.TransformPoint(baked.bounds.center);
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"Evidence/fullbody-geometry.txt"),"bounds="+baked.bounds+" scale="+full.transform.lossyScale+" center="+center);
            float distance = Mathf.Max(2.5f,height*1.3f);
            external.transform.position = center + new Vector3(-distance*.55f,.3f,distance); external.transform.LookAt(center);
            external.nearClipPlane=.05f; external.fieldOfView=50;
            target = new RenderTexture(960,720,24); texture = new Texture2D(960,720,TextureFormat.RGB24,false);
            Capture(external,"fullbody-standing");
            external.transform.position = center + new Vector3(distance*.55f,.3f,-distance); external.transform.LookAt(center);
            Capture(external,"fullbody-back");
            external.transform.position = center + new Vector3(-distance*.55f,.3f,distance); external.transform.LookAt(center);
            var red = new Material(Shader.Find("Unlit/Color")) {color=Color.red};
            var blue = new Material(Shader.Find("Unlit/Color")) {color=Color.blue};
            full.sharedMaterials=full.sharedMaterials.Select(_=>red).ToArray();
            upper.sharedMaterials=upper.sharedMaterials.Select(_=>blue).ToArray(); lower.sharedMaterials=lower.sharedMaterials.Select(_=>blue).ToArray();
            var pixels=Capture(external,"fullbody-external-mask");
            Add("ExternalCameraShowsWholeModel", Red(pixels)>1000, "full body pixels="+Red(pixels));
            Add("ExternalCameraHidesCroppedModels", Blue(pixels)==0, "1P pixels="+Blue(pixels));
            Camera.CameraCallback observe = cam => {
                if(cam==owner) {
                    Add("OwnerBodyIsShadowOnly", full.enabled && full.shadowCastingMode==ShadowCastingMode.ShadowsOnly, "enabled="+full.enabled+" mode="+full.shadowCastingMode);
                    Add("OwnerHasFirstPersonModels", upper.enabled && lower.enabled, "upper="+upper.enabled+" lower="+lower.enabled);
                    Add("CroppedModelsDoNotCastDuplicateShadows", upper.shadowCastingMode==ShadowCastingMode.Off && lower.shadowCastingMode==ShadowCastingMode.Off, "upper="+upper.shadowCastingMode+" lower="+lower.shadowCastingMode);
                } else if(cam==external) {
                    Add("ExternalHasVisibleFullBody", full.enabled && full.shadowCastingMode==ShadowCastingMode.On && !upper.enabled && !lower.enabled, "full="+full.enabled+" mode="+full.shadowCastingMode+" 1P="+upper.enabled+","+lower.enabled);
                }
            };
            Camera.onPreRender += observe;
            try {
                var originalRotation=owner.transform.rotation;
                owner.transform.rotation=originalRotation*Quaternion.Euler(55,0,0);
                pixels=Capture(owner,"fullbody-owner-mask");
                owner.transform.rotation=originalRotation;
                Add("OwnerDoesNotSeeFullModel", Red(pixels)==0, "full body pixels="+Red(pixels));
                Add("OwnerStillSeesOwnLegs", Blue(pixels)>100, "1P pixels="+Blue(pixels));
                Capture(external,"fullbody-external-after-owner");
            } finally { Camera.onPreRender-=observe; }
            Add("RenderRestoresExternalAppearance", full.enabled && !upper.enabled && !lower.enabled && full.shadowCastingMode==ShadowCastingMode.On, "default full="+full.enabled+" 1P="+upper.enabled+","+lower.enabled);
            var visibility = full.transform.parent.GetComponent<PlayerBodyVisibility>();
            var nestedTarget = new RenderTexture(160,120,24);
            Camera.CameraCallback nested = cam => {
                if(cam!=owner) return;
                var oldTarget = external.targetTexture; external.targetTexture=nestedTarget;
                try { external.Render(); }
                finally { external.targetTexture=oldTarget; }
                Add("NestedCameraRestoresOwnerVisibility", upper.enabled && lower.enabled && full.shadowCastingMode==ShadowCastingMode.ShadowsOnly, "owner state restored after nested external render");
            };
            Camera.onPreRender+=nested;
            try { Capture(owner,"fullbody-nested-owner"); }
            finally { Camera.onPreRender-=nested; UnityEngine.Object.DestroyImmediate(nestedTarget); }
            int mask = owner.cullingMask;
            visibility.enabled=false;
            full.enabled=false; upper.enabled=false;lower.enabled=false;
            Capture(external,"fullbody-detached");
            Add("DisabledVisibilityUnsubscribes", !full.enabled && !upper.enabled && !lower.enabled, "camera does not override renderer state after detach");
            visibility.enabled=true;
            var replacement = new GameObject("Replacement Owner Camera").AddComponent<Camera>(); replacement.enabled=false;
            replacement.transform.SetPositionAndRotation(owner.transform.position,owner.transform.rotation*Quaternion.Euler(55,0,0));
            visibility.SetOwnerCamera(replacement);
            pixels=Capture(replacement,"fullbody-replacement-owner");
            Add("ReplacementCameraGetsFirstPersonModel", Red(pixels)==0 && Blue(pixels)>100, "full="+Red(pixels)+" 1P="+Blue(pixels));
            pixels=Capture(owner,"fullbody-old-owner");
            Add("PreviousOwnerBecomesExternalCamera", Blue(pixels)==0, "1P pixels="+Blue(pixels));
            Add("CameraLayersUnchanged", owner.cullingMask==mask, "culling mask="+owner.cullingMask);
            Finish(null);
        } catch (Exception e) { Finish(e); }
    }
    static Color32[] Capture(Camera cam,string name) {
        var previousTarget=cam.targetTexture; var previousActive=RenderTexture.active;
        var previousFlags=cam.clearFlags; var previousBackground=cam.backgroundColor; float aspect=cam.aspect;
        try {
            cam.targetTexture=target; cam.aspect=4f/3f; cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;
            cam.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,960,720),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"Evidence",name+".png"),texture.EncodeToPNG());
            return texture.GetPixels32();
        } finally { cam.targetTexture=previousTarget; cam.aspect=aspect; cam.clearFlags=previousFlags;cam.backgroundColor=previousBackground;RenderTexture.active=previousActive; }
    }
    static int Red(Color32[] pixels) => pixels.Count(c=>c.r>128&&c.g<30&&c.b<30);
    static int Blue(Color32[] pixels) => pixels.Count(c=>c.b>128&&c.g<30&&c.r<30);
    static void Add(string name,bool passed,string detail) {
        if(!passed) report.failures++; report.results.Add(new CollisionProbe.Result{name=name,passed=passed,detail=detail});
    }
    static void Finish(Exception e) {
        EditorApplication.update-=Tick;
        if(e!=null) Add("FullBodySetup",false,e.ToString());
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"Evidence",Environment.GetEnvironmentVariable("COLLISION_PROBE_REPORT")??"fullbody.json"),JsonUtility.ToJson(report,true));
        EditorApplication.Exit(report.failures==0?0:2);
    }
}
