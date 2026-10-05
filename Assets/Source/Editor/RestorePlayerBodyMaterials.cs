namespace MEdge.Source.Editor
{
    using System;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>Rebuild local Unity materials from the owner's UE Viewer texture export.</summary>
    public static class RestorePlayerBodyMaterials
    {
        const string Folder = "Assets/LocalFaithBody/Resources/LocalFaith3P";
        static string ExportFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MirrorEdgeParkourData/Character/FullBody");

        [MenuItem("Tools/Mirror's Edge/Восстановить материалы полной модели")]
        public static void Restore()
        {
            if (!Directory.Exists(ExportFolder)) throw new DirectoryNotFoundException("Export CH_TKY_Crim_Fixer with UE Viewer to " + ExportFolder);
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            Create("MI_Faith_Lowres_Face", "Asia_Fixer_Face_C_shaded", "Faith_Body_N", "Faith_Body_S");
            Create("MI_Faith_Lowres_Upper", "Asia_Fixer_Upper_C_shaded", "Asia_Fixer_Upper_N", "Asia_Fixer_Upper_S");
            Create("MI_Faith_Lowres_Lower", "Asia_Fixer_Lower_C_shaded", "Asia_Fixer_Lower_N", "Asia_Fixer_Lower_S");
            Create("MI_Faith_Lowres_Glove", "Faith_Glove_C", null, null);
            Create("MI_Faith_Lowres_Hair", null, null, null, new Color(.018f,.012f,.009f));
            Create("M_Faith_Eyes", "CH_Dummy_eye_D", "TD_Eye2_Normal", null);
            Create("faithTeeth", "Teeth_D", "Teeth_N", null);
            var lashes = Create("unlitAlpha", "lashes3", null, null);
            lashes.SetFloat("_Mode", 1); lashes.SetFloat("_Cutoff", .4f);
            lashes.SetOverrideTag("RenderType", "TransparentCutout");
            lashes.EnableKeyword("_ALPHATEST_ON"); lashes.renderQueue = 2450;
            EditorUtility.SetDirty(lashes);
            AssetDatabase.SaveAssetIfDirty(lashes);
            Debug.Log("Full Faith body: restored 8 local materials from the installed game's texture export.");
        }

        static Material Create(string name, string diffuse, string normal, string specular, Color? tint = null)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) {
                material = new Material(Shader.Find("Standard (Specular setup)"));
                AssetDatabase.CreateAsset(material,path);
            }
            material.color = tint ?? Color.white;
            material.SetFloat("_Glossiness", .18f);
            material.SetFloat("_GlossMapScale", .25f);
            material.SetColor("_SpecColor", new Color(.12f,.12f,.12f));
            if (diffuse != null) material.mainTexture = Import(diffuse,false);
            if (normal != null) { material.SetTexture("_BumpMap",Import(normal,true));material.EnableKeyword("_NORMALMAP"); }
            if (specular != null) { material.SetTexture("_SpecGlossMap",Import(specular,false));material.EnableKeyword("_SPECGLOSSMAP"); }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static Texture2D Import(string name, bool normal)
        {
            string source = Directory.GetFiles(ExportFolder,name+".tga",SearchOption.AllDirectories).FirstOrDefault();
            if (source == null) throw new FileNotFoundException("Missing exported texture: " + name);
            string path = Folder + "/" + name + ".tga";
            File.Copy(source,path,true);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
