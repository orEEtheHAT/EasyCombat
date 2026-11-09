using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using EasyCombat.Editor;

namespace EasyCombat.Samples.Editor
{
    public static class DeliveryRouteBuilder
    {
        [MenuItem("EasyCombat/Delivery/Create Test Route")]
        public static void Create()
        {
            if(string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            { EditorUtility.DisplayDialog("Delivery Route","Сначала сохрани текущую сцену, затем создай полигон. Она останется открытой.","OK"); return; }
            CreateScene(DeliveryKitBuilder.Build());
        }
        public static string CreateScene(string kit)
        {
            var original=SceneManager.GetActiveScene();
            bool single=Application.isBatchMode && string.IsNullOrEmpty(original.path) && !original.isDirty;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,single ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene); var demo=new GameObject("Delivery test drive").AddComponent<DeliveryRoute>();
                GameObject Spawn(string prefab,Vector3 at) { var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(kit+"/"+prefab+".prefab"),scene); go.transform.position=at; return go; }
                Material ColorMaterial(string name,Color color)
                {
                    var shader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    var mat=new Material(shader){color=color}; if(mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor",color);
                    AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(kit+"/"+name+".mat")); return mat;
                }
                var roadMat=ColorMaterial("Road",new Color(.14f,.2f,.23f)); var mudMat=ColorMaterial("Mud",new Color(.37f,.22f,.13f));
                var wood=ColorMaterial("CartWood",new Color(.72f,.47f,.25f)); var rim=ColorMaterial("WheelColor",new Color(.14f,.17f,.19f)); var beam=ColorMaterial("ConeColor",new Color(.17f,.9f,.94f));
                GameObject Box(string name,Vector3 pos,Vector3 size,Material mat)
                {var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.position=pos; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=mat; return go;}
                demo.Road=Box("Road",new Vector3(0,-.5f,3),new Vector3(18,1,36),roadMat).transform;
                Box("Left-wheel bump",new Vector3(-.75f,.07f,-1),new Vector3(.7f,.14f,.5f),wood);
                var mud=Spawn("MudVolume",new Vector3(0,0,3.6f)); var decal=Box("Mud patch",mud.transform.position+Vector3.up*.005f,new Vector3(6,.01f,8),mudMat); Object.DestroyImmediate(decal.GetComponent<Collider>());
                var rig=Spawn("LoadedCart",new Vector3(0,.02f,-4)); demo.Wheels=rig.GetComponentsInChildren<WheelContactSource>(); demo.Turret=rig.GetComponentInChildren<MovementField>();
                demo.Cart=demo.Wheels[0].GetComponentInParent<Rigidbody>(); demo.Parcel=rig.transform.Find("Parcel").GetComponent<Rigidbody>();
                foreach(var render in rig.GetComponentsInChildren<MeshRenderer>()) render.sharedMaterial=render.name=="Wheel visual" ? rim : wood;
                var enemy=Spawn("NavMeshEnemy",new Vector3(1,.12f,5)); demo.Agent=enemy.GetComponent<NavMeshAgent>(); demo.Agent.enabled=false; demo.Speed=enemy.GetComponent<NavMeshSpeedAdapter>();
                var visual=GameObject.CreatePrimitive(PrimitiveType.Capsule); Object.DestroyImmediate(visual.GetComponent<Collider>()); visual.transform.SetParent(enemy.transform,false);
                visual.transform.localPosition=Vector3.up; visual.GetComponent<Renderer>().sharedMaterial=beam;
                demo.Outline=demo.Turret.gameObject.AddComponent<LineRenderer>(); demo.Outline.sharedMaterial=beam; demo.Outline.widthMultiplier=.04f; demo.Outline.useWorldSpace=true;
                demo.View=new GameObject("Camera").AddComponent<Camera>(); demo.View.transform.position=new Vector3(9,10,-15); demo.View.transform.LookAt(new Vector3(0,0,-1)); demo.View.clearFlags=CameraClearFlags.SolidColor; demo.View.backgroundColor=new Color(.06f,.08f,.11f);
                var path=AssetDatabase.GenerateUniqueAssetPath(kit+"/DeliveryRoute.unity");
                if(!EditorSceneManager.SaveScene(scene,path)) throw new System.InvalidOperationException("Could not save DeliveryRoute.");
                AssetDatabase.SaveAssets(); return path;
            }
            finally { if(!single) { EditorSceneManager.CloseScene(scene,true); if(original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original); } }
        }
    }
}
