using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace EasyCombat.Editor
{
    public static class DeliveryKitBuilder
    {
        [MenuItem("EasyCombat/Delivery/Create Cart Kit")]
        public static void Create() { Selection.activeObject=AssetDatabase.LoadMainAssetAtPath(Build()+"/Cart.prefab"); }
        public static string Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play mode before creating prefabs.");
            const string parent="Assets/EasyCombatDelivery"; CombatAssetLibrary.EnsureFolder(parent);
            string root=AssetDatabase.GenerateUniqueAssetPath(parent+"/CartKit"); CombatAssetLibrary.EnsureFolder(root);
            T Asset<T>(string name) where T:ScriptableObject
            {
                var path=root+"/Definitions/"+CombatAssetLibrary.Folders[CombatAssetLibrary.Category(typeof(T))];
                CombatAssetLibrary.EnsureFolder(path); var a=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a,path+"/"+name+".asset"); return a;
            }
            var impact=Asset<DamageTypeDefinition>("Impact"); impact.DisplayName="Impact";
            var boxProfile=Asset<ImpactDamageDefinition>("FragileParcel"); boxProfile.Measure=ImpactMeasure.NormalSpeed; boxProfile.SafeThreshold=3; boxProfile.DamageType=impact;
            var wheelProfile=Asset<ImpactDamageDefinition>("WheelWear"); wheelProfile.Measure=ImpactMeasure.SuspensionLoad; wheelProfile.SafeThreshold=650;
            wheelProfile.DamageByExcess=AnimationCurve.Linear(0,0,4000,45); wheelProfile.DamageType=impact;
            var field=Asset<MovementInfluenceDefinition>("SlowByHalf"); field.SpeedMultiplier=.5f; field.RollingDeceleration=0;
            var mud=Asset<MovementInfluenceDefinition>("Mud"); mud.RollingDeceleration=5; mud.SpeedMultiplier=.5f;
            var area=Asset<AreaDefinition>("EnemyCone"); area.Shape=AreaShape.Cone; area.ConeAngle=70; area.Radius=12; area.Targets=AreaTargetFilter.Enemies; area.RequireLineOfSight=true;
            var sensor=Asset<AreaTriggerDefinition>("ContinuousCone"); sensor.Duration=0; sensor.StayInterval=.1f; sensor.Area=area;
            var preview=EditorSceneManager.NewPreviewScene();
            GameObject Make(string name,GameObject owner=null)
            {
                var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,preview);
                if(owner) go.transform.SetParent(owner.transform,false);
                return go;
            }
            void Box(GameObject obj,Vector3 size,Vector3 offset)
            {
                var c=obj.AddComponent<BoxCollider>(); c.size=size; c.center=offset;
                var visual=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(visual,preview); Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.name="Visual"; visual.transform.SetParent(obj.transform,false); visual.transform.localPosition=offset; visual.transform.localScale=size;
            }
            GameObject Save(GameObject go,string name)
            {
                var prefab=PrefabUtility.SaveAsPrefabAsset(go,root+"/"+name+".prefab");
                if(!prefab) throw new System.InvalidOperationException("Could not create "+name); return prefab;
            }
            try
            {
                var cargo=Make("Parcel"); Box(cargo,Vector3.one*.45f,Vector3.zero); var parcelBody=cargo.AddComponent<Rigidbody>(); parcelBody.mass=2; parcelBody.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                var hp=cargo.AddComponent<Health>(); var durability=cargo.AddComponent<ImpactDurability>(); durability.Target=hp; durability.Profile=boxProfile;
                cargo.AddComponent<RigidbodyImpactSource>().Receiver=durability; var cargoAsset=Save(cargo,"Parcel");
                var gun=Make("Cone turret"); gun.AddComponent<CombatTeam>().Team=1; var fieldComponent=gun.AddComponent<MovementField>(); fieldComponent.SensorProfile=sensor; fieldComponent.Effect=field;
                var gunAsset=Save(gun,"ConeTurret");
                var zone=Make("Mud volume"); var trigger=zone.AddComponent<BoxCollider>(); trigger.isTrigger=true; trigger.size=new Vector3(6,1,8); zone.AddComponent<MovementSurface>().Effect=mud; Save(zone,"MudVolume");
                var enemy=Make("NavMesh enemy"); enemy.AddComponent<CapsuleCollider>().height=2; enemy.AddComponent<Health>(); enemy.AddComponent<StatusController>();
                enemy.AddComponent<CombatTeam>().Team=2; enemy.AddComponent<NavMeshSpeedAdapter>(); enemy.GetComponent<NavMeshAgent>().speed=6; Save(enemy,"NavMeshEnemy");
                var cart=Make("Cart"); var body=cart.AddComponent<Rigidbody>(); body.mass=100; Box(cart,new Vector3(1.4f,.2f,2.6f),new Vector3(0,.48f,0)); cart.AddComponent<CombatTeam>().Team=1;
                for(int side=-1;side<=1;side+=2)
                {
                    Box(Make("Side board",cart),new Vector3(.08f,.25f,2.6f),new Vector3(side*.68f,.68f,0));
                    Box(Make("End board",cart),new Vector3(1.4f,.25f,.08f),new Vector3(0,.68f,side*1.26f));
                }
                for(int i=0;i<4;i++)
                {
                    var wheel=Make("Wheel "+(i+1),cart); wheel.transform.localPosition=new Vector3((i%2==0 ? -1 : 1)*.77f,.5f,(i<2 ? -1 : 1)*.9f);
                    var wc=wheel.AddComponent<WheelCollider>(); wc.radius=.3f; wc.suspensionDistance=.18f; wc.mass=8;
                    var spring=wc.suspensionSpring; spring.spring=3500; spring.damper=420; wc.suspensionSpring=spring;
                    var whp=wheel.AddComponent<Health>(); var wear=wheel.AddComponent<ImpactDurability>(); wear.Target=whp; wear.Profile=wheelProfile;
                    var drag=wheel.AddComponent<RollingResistance>(); drag.Body=body; drag.LoadShare=.25f;
                    var contact=wheel.AddComponent<WheelContactSource>(); contact.Durability=wear; contact.Mud=drag;
                    var visual=GameObject.CreatePrimitive(PrimitiveType.Cylinder); SceneManager.MoveGameObjectToScene(visual,preview); Object.DestroyImmediate(visual.GetComponent<Collider>());
                    visual.name="Wheel visual"; visual.transform.SetParent(cart.transform,false); visual.transform.localScale=new Vector3(.6f,.09f,.6f);
                    visual.transform.position=wheel.transform.position; visual.transform.rotation=Quaternion.Euler(0,0,90); contact.WheelVisual=visual.transform;
                }
                var hold=Make("Cargo safe box",cart); var safe=hold.AddComponent<BoxCollider>(); safe.isTrigger=true; safe.size=new Vector3(1.45f,.9f,2.6f); safe.center=new Vector3(0,.98f,0);
                var mounted=(GameObject)PrefabUtility.InstantiatePrefab(gunAsset,preview); mounted.transform.SetParent(cart.transform,false); mounted.transform.localPosition=new Vector3(0,1,1.45f);
                mounted.GetComponent<MovementField>().Owner=cart.transform; PrefabUtility.RecordPrefabInstancePropertyModifications(mounted.GetComponent<MovementField>());
                var cartAsset=Save(cart,"Cart");
                // Independent sibling rigidbodies. The crate is never parented to the moving cart.
                var rig=Make("Loaded cart"); var vehicle=(GameObject)PrefabUtility.InstantiatePrefab(cartAsset,preview);
                vehicle.transform.SetParent(rig.transform,false); var parcel=(GameObject)PrefabUtility.InstantiatePrefab(cargoAsset,preview);
                parcel.transform.SetParent(rig.transform,false); parcel.transform.localPosition=new Vector3(0,.83f,0);
                var item=parcel.GetComponent<ImpactDurability>(); item.SafeHold=vehicle.transform.Find("Cargo safe box").GetComponent<BoxCollider>();
                PrefabUtility.RecordPrefabInstancePropertyModifications(item); Save(rig,"LoadedCart");
                foreach(var guid in AssetDatabase.FindAssets("t:ScriptableObject",new[] {root})) EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)));
                AssetDatabase.SaveAssets(); return root;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
