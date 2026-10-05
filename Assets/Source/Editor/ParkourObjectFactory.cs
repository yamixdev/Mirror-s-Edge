using System.Collections.Generic;
using MEdge.Source;
using UnityEditor;
using UnityEngine;

namespace MEdge.EditorTools
{
    public static class ParkourObjectFactory
    {
        public const string AssetFolder = "Assets/Prefabs/Parkour";
        public static readonly string[] PrefabNames = { "BalanceBeam", "SwingBar", "Ladder", "Zipline" };

        [MenuItem("GameObject/Mirror's Edge/Балка для баланса", false, 10)]
        static void BeamMenu() => Place(ParkourObjectGeometry.ObjectKind.BalanceBeam);
        [MenuItem("GameObject/Mirror's Edge/Перекладина для раскачивания", false, 11)]
        static void SwingMenu() => Place(ParkourObjectGeometry.ObjectKind.SwingBar);
        [MenuItem("GameObject/Mirror's Edge/Лестница", false, 12)]
        static void LadderMenu() => Place(ParkourObjectGeometry.ObjectKind.Ladder);
        [MenuItem("GameObject/Mirror's Edge/Трос для зиплайна", false, 13)]
        static void ZiplineMenu() => Place(ParkourObjectGeometry.ObjectKind.Zipline);
        [MenuItem("GameObject/Mirror's Edge/Прикрепить геометрию к выбранному Volume", false, 20)]
        static void AttachMenu()
        {
            var selected=Selection.activeGameObject;
            if(!selected) return;
            ParkourObjectGeometry.ObjectKind kind;
            if(selected.TryGetComponent<UnityTdBalanceWalkVolume>(out _))kind=ParkourObjectGeometry.ObjectKind.BalanceBeam;
            else if(selected.TryGetComponent<UnityTdSwingVolume>(out _))kind=ParkourObjectGeometry.ObjectKind.SwingBar;
            else if(selected.TryGetComponent<UnityTdLadderVolume>(out _))kind=ParkourObjectGeometry.ObjectKind.Ladder;
            else if(selected.TryGetComponent<UnityTdZiplineVolume>(out _))kind=ParkourObjectGeometry.ObjectKind.Zipline;
            else {Debug.LogWarning("Выберите объект с Balance, Swing, Ladder или Zipline Volume.");return;}
            Undo.RegisterFullObjectHierarchyUndo(selected,"Прикрепить геометрию");
            var geometry=selected.GetComponent<ParkourObjectGeometry>();
            if(!geometry)geometry=Undo.AddComponent<ParkourObjectGeometry>(selected);
            geometry.Kind=kind;geometry.Material=DefaultMaterial();
            geometry.Thickness=kind==ParkourObjectGeometry.ObjectKind.BalanceBeam?.18f:.06f;
            Rebuild(geometry,true);
        }

        static void Place(ParkourObjectGeometry.ObjectKind kind)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"{AssetFolder}/{PrefabNames[(int)kind]}.prefab");
            var root=prefab ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : Create(kind);
            Undo.RegisterCreatedObjectUndo(root,"Добавить объект паркура");
            if(SceneView.lastActiveSceneView)root.transform.position=SceneView.lastActiveSceneView.pivot;
            Selection.activeGameObject=root;
        }

        static Material DefaultMaterial()
        {
            const string path=AssetFolder+"/ParkourRed.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material)return material;
            System.IO.Directory.CreateDirectory(AssetFolder);
            AssetDatabase.Refresh();
            material=new Material(Shader.Find("Standard")){name="ParkourRed",color=new Color(.8f,.045f,.025f)};
            material.SetFloat("_Glossiness",.3f);AssetDatabase.CreateAsset(material,path);return material;
        }

        public static GameObject Create(ParkourObjectGeometry.ObjectKind kind)
        {
            var root=new GameObject(PrefabNames[(int)kind]);
            var geometry=root.AddComponent<ParkourObjectGeometry>();geometry.Kind=kind;geometry.Material=DefaultMaterial();
            switch(kind)
            {
                case ParkourObjectGeometry.ObjectKind.BalanceBeam:
                    root.AddComponent<UnityTdBalanceWalkVolume>().SplineControls=new List<Vector3>{new(0,0,-2),new(0,0,2)};break;
                case ParkourObjectGeometry.ObjectKind.SwingBar:
                    root.AddComponent<UnityTdSwingVolume>().bThickGrip=false;geometry.Thickness=.06f;break;
                case ParkourObjectGeometry.ObjectKind.Ladder:
                    root.AddComponent<UnityTdLadderVolume>().SplineControls=new List<Vector3>{new(0,-2,0),new(0,2,0)};geometry.Thickness=.04f;break;
                case ParkourObjectGeometry.ObjectKind.Zipline:
                    var zip=root.AddComponent<UnityTdZiplineVolume>();zip.SplineControls=new List<Vector3>{new(0,0,-3),new(0,-1,3)};zip.LandingStrip=70f;geometry.Thickness=.04f;break;
            }
            Rebuild(geometry);return root;
        }

        // Can be called by isolated verification without modifying a user's scene.
        public static void GeneratePrefabs()
        {
            for(int i=0;i<PrefabNames.Length;i++)
            {
                var root=Create((ParkourObjectGeometry.ObjectKind)i);
                PrefabUtility.SaveAsPrefabAsset(root,$"{AssetFolder}/{PrefabNames[i]}.prefab");
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }

        public static void Rebuild(ParkourObjectGeometry geometry,bool recordUndo=false)
        {
            if(geometry.GeneratedGeometry) {
                if(recordUndo)Undo.DestroyObjectImmediate(geometry.GeneratedGeometry.gameObject);
                else Object.DestroyImmediate(geometry.GeneratedGeometry.gameObject);
            }
            var container=new GameObject("Geometry");container.transform.SetParent(geometry.transform,false);geometry.GeneratedGeometry=container.transform;
            var trigger=geometry.GetComponent<BoxCollider>();
            if(!trigger)trigger=recordUndo?Undo.AddComponent<BoxCollider>(geometry.gameObject):geometry.gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;
            switch(geometry.Kind)
            {
                case ParkourObjectGeometry.ObjectKind.BalanceBeam:
                    var beam=geometry.GetComponent<UnityTdBalanceWalkVolume>();
                    if(!beam || beam.SplineControls.Count<2)break;
                    for(int i=1;i<beam.SplineControls.Count;i++)
                        Segment(geometry,beam.SplineControls[i-1]-Vector3.up*geometry.Thickness*.5f,beam.SplineControls[i]-Vector3.up*geometry.Thickness*.5f,geometry.Thickness,true,false,"Beam");
                    FitTrigger(trigger,beam.SplineControls,new Vector3(.16f,1.8f,.02f),Vector3.up*.9f);break;
                case ParkourObjectGeometry.ObjectKind.SwingBar:
                    Segment(geometry,Vector3.left*geometry.BarLength*.5f,Vector3.right*geometry.BarLength*.5f,geometry.Thickness,true,true,"Grip");
                    trigger.center=new Vector3(0,-.65f,0);trigger.size=new Vector3(geometry.BarLength,1.5f,2.6f);break;
                case ParkourObjectGeometry.ObjectKind.Ladder:
                    var ladder=geometry.GetComponent<UnityTdLadderVolume>();
                    if(!ladder || ladder.SplineControls.Count<2)break;
                    for(int i=1;i<ladder.SplineControls.Count;i++)foreach(float side in new[]{-1f,1f}) {
                        var offset=Vector3.right*geometry.LadderWidth*.5f*side;
                        Segment(geometry,ladder.SplineControls[i-1]+offset,ladder.SplineControls[i]+offset,geometry.Thickness,false,true,"Rail");
                    }
                    foreach(var point in ladder.EnumSteps()) Segment(geometry,point-Vector3.right*geometry.LadderWidth*.5f,point+Vector3.right*geometry.LadderWidth*.5f,geometry.Thickness,false,true,"Rung");
                    // One thin surface per section prevents individual rung tops
                    // catching the pawn's axis-aligned collision box while climbing.
                    for(int i=1;i<ladder.SplineControls.Count;i++) {
                        var surface=new GameObject("LadderCollision");surface.transform.SetParent(container.transform,false);
                        var delta=ladder.SplineControls[i]-ladder.SplineControls[i-1];
                        // Keep the backing surface behind the grips. The legacy
                        // cylinder is queried as a world-aligned box in Unity;
                        // after rotation its corners need this small clearance.
                        surface.transform.localPosition=(ladder.SplineControls[i]+ladder.SplineControls[i-1])*.5f+Vector3.forward*.15f;
                        surface.transform.localRotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);
                        surface.AddComponent<BoxCollider>().size=new Vector3(geometry.LadderWidth+geometry.Thickness,delta.magnitude,geometry.Thickness);
                    }
                    FitTrigger(trigger,ladder.SplineControls,new Vector3(geometry.LadderWidth+.3f,.3f,.8f),Vector3.back*.3f);break;
                case ParkourObjectGeometry.ObjectKind.Zipline:
                    var zip=geometry.GetComponent<UnityTdZiplineVolume>();
                    if(!zip || zip.SplineControls.Count<2)break;
                    for(int i=1;i<zip.SplineControls.Count;i++)Segment(geometry,zip.SplineControls[i-1],zip.SplineControls[i],geometry.Thickness,false,true,"Cable");
                    FitTrigger(trigger,zip.SplineControls,new Vector3(.65f,1.6f,.1f),Vector3.down*.7f);break;
            }
            EditorUtility.SetDirty(geometry);EditorUtility.SetDirty(trigger);
            if(recordUndo)Undo.RegisterCreatedObjectUndo(container,"Обновить объект паркура");
            if(PrefabUtility.IsPartOfPrefabInstance(geometry))PrefabUtility.RecordPrefabInstancePropertyModifications(geometry);
        }

        static void FitTrigger(BoxCollider trigger,List<Vector3> points,Vector3 padding,Vector3 offset)
        {
            var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);
            trigger.center=bounds.center+offset;trigger.size=bounds.size+padding;
        }

        static void Segment(ParkourObjectGeometry geometry,Vector3 start,Vector3 end,float thickness,bool solid,bool round,string name)
        {
            var delta=end-start;if(delta.sqrMagnitude<.000001f)return;
            var part=GameObject.CreatePrimitive(round?PrimitiveType.Cylinder:PrimitiveType.Cube);part.name=name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(geometry.GeneratedGeometry,false);part.transform.localPosition=(start+end)*.5f;
            part.transform.localRotation=Quaternion.FromToRotation(round?Vector3.up:Vector3.forward,delta.normalized);
            part.transform.localScale=round ? new Vector3(thickness,delta.magnitude*.5f,thickness) : new Vector3(thickness,thickness,delta.magnitude);
            part.GetComponent<MeshRenderer>().sharedMaterial=geometry.Material;
            if(solid) {var collider=part.AddComponent<BoxCollider>();collider.size=round?new Vector3(1,2,1):Vector3.one;}
        }
    }

    [CustomEditor(typeof(ParkourObjectGeometry))]
    public class ParkourObjectGeometryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var selected=(ParkourObjectGeometry)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Thickness"),new GUIContent("Толщина, м"));
            if(selected.Kind==ParkourObjectGeometry.ObjectKind.SwingBar)EditorGUILayout.PropertyField(serializedObject.FindProperty("BarLength"),new GUIContent("Длина перекладины, м"));
            if(selected.Kind==ParkourObjectGeometry.ObjectKind.Ladder)EditorGUILayout.PropertyField(serializedObject.FindProperty("LadderWidth"),new GUIContent("Ширина лестницы, м"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Material"),new GUIContent("Материал"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Точки пути меняются в компоненте Volume или его ручками в Scene. После изменения точек, толщины или длины нажмите кнопку ниже. Перемещение и поворот всего объекта работают сразу.",MessageType.Info);
            if(GUILayout.Button("Обновить геометрию и зону взаимодействия")) {
                var geometry=(ParkourObjectGeometry)target;Undo.RegisterFullObjectHierarchyUndo(geometry.gameObject,"Обновить объект паркура");ParkourObjectFactory.Rebuild(geometry,true);
            }
        }
    }
}
