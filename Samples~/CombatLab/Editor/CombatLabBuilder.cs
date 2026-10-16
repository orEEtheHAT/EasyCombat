using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EasyCombat.Samples.Editor
{
    public static class CombatLabBuilder
    {
        private static string root;
        private static T Asset<T>(string category,string name) where T:ScriptableObject
        {
            var folder=root+"/Definitions/"+category;
            if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(root+"/Definitions",category);
            var value=ScriptableObject.CreateInstance<T>(); value.name=name; AssetDatabase.CreateAsset(value,folder+"/"+name+".asset"); return value;
        }
        private static Material Material(string name,Color color)
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var value=new Material(shader) { name=name,color=color }; if(value.HasProperty("_BaseColor")) value.SetColor("_BaseColor",color);
            AssetDatabase.CreateAsset(value,root+"/"+name+".mat"); return value;
        }
        private static GameObject Shape(string name,PrimitiveType primitive,Vector3 position,Vector3 scale,Material material,bool collider=true)
        {
            var go=GameObject.CreatePrimitive(primitive); go.name=name; go.transform.position=position; go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material; if(!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        [MenuItem("EasyCombat/Samples/Create Combat Lab")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene();
        }
        public static string CreateScene()
        {
            if(!AssetDatabase.IsValidFolder("Assets/EasyCombatSamples")) AssetDatabase.CreateFolder("Assets","EasyCombatSamples");
            root=AssetDatabase.GenerateUniqueAssetPath("Assets/EasyCombatSamples/CombatLab"); AssetDatabase.CreateFolder("Assets/EasyCombatSamples",Path.GetFileName(root));
            AssetDatabase.CreateFolder(root,"Definitions");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=Material("Floor",new Color(.055f,.08f,.13f)); var teal=Material("Targets",new Color(.05f,.8f,.7f));
            var orange=Material("Projectiles",new Color(1,.45f,.1f)); var blue=Material("Shield",new Color(.2f,.55f,1));
            Shape("Ground",PrimitiveType.Cube,new Vector3(0,-.3f,0),new Vector3(22,.5f,22),floor);
            var poisonType=Asset<DamageTypeDefinition>("DamageTypes","Poison"); poisonType.DisplayName="Poison";
            var physical=Asset<DamageTypeDefinition>("DamageTypes","Physical"); physical.DisplayName="Physical";
            var poisonTick=Asset<PeriodicDamageEffect>("StatusEffects","PoisonTick"); poisonTick.DamageType=poisonType; poisonTick.DamagePerStack=2;
            var poison=Asset<StatusDefinition>("Statuses","Poison"); poison.Duration=8; poison.TickInterval=1; poison.MaximumStacks=5; poison.TickEffects=new StatusTickEffect[] { poisonTick };
            var burstTick=Asset<PeriodicDamageEffect>("StatusEffects","BurstTick"); burstTick.DamageType=poisonType; burstTick.DamagePerStack=25;
            var burst=Asset<StatusDefinition>("Statuses","PoisonBurst"); burst.Duration=.1f; burst.TickInterval=.1f; burst.TickEffects=new StatusTickEffect[] { burstTick };
            var combination=Asset<StatusCombinationDefinition>("Combinations","ThreePoisonToBurst"); combination.Consume=poison; combination.Stacks=3; combination.Apply=burst;
            var combine=Asset<ReactionStatusCombinationAction>("Reactions","ConsumePoison"); combine.Combination=combination;
            var reaction=Asset<ReactionDefinition>("Reactions","PoisonCombo"); reaction.Trigger=ReactionTrigger.DamageDealt; reaction.Target=ReactionTarget.Victim;
            reaction.DamageType=poisonType; reaction.Actions=new CombatReactionAction[] { combine };
            var poisonShot=Asset<AttackDefinition>("Weapons","PoisonShot"); poisonShot.Damage=5; poisonShot.DamageType=poisonType; poisonShot.RoundsPerMinute=0;
            poisonShot.OnHitStatuses=new[] { new StatusApplication(poison) };
            var hitscan=Asset<AttackDefinition>("Weapons","WideHitscan"); hitscan.Damage=18; hitscan.DamageType=physical; hitscan.HitscanRadius=.25f;
            hitscan.CriticalChance=.3f; hitscan.CriticalMultiplier=2; hitscan.RoundsPerMinute=0; hitscan.ContactBehavior=ContactBehavior.PierceTargets; hitscan.MaximumTargets=4;
            var slashDamage=Asset<AttackDefinition>("Weapons","SlashDamage"); slashDamage.Damage=20; slashDamage.DamageType=physical;
            var slash=Asset<MeleeDefinition>("Melee","SlashWindow"); slash.Attack=slashDamage; slash.Duration=.8f; slash.Windows=new[] { new MeleeWindow(.1f,.7f) }; slash.Radius=.45f;
            var projectile=Shape("Projectile",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.16f,orange,false); projectile.AddComponent<DamageProjectile>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(projectile,root+"/Projectile.prefab").GetComponent<DamageProjectile>(); UnityEngine.Object.DestroyImmediate(projectile);
            var swarm=Asset<AttackDefinition>("Weapons","SlowPiercingSphere"); swarm.Delivery=DamageDelivery.Projectile; swarm.ProjectilePrefab=prefab; swarm.ProjectileSpeed=3;
            swarm.ProjectileRadius=.12f; swarm.ProjectileLifetime=7; swarm.Range=18; swarm.Damage=1; swarm.DamageType=physical;
            swarm.ContactBehavior=ContactBehavior.PierceTargets; swarm.MaximumTargets=3;
            var shield=Asset<ShieldLayer>("Defense","Shield"); shield.Capacity=75;
            var defense=Asset<DefenseProfile>("Defense","ShieldProfile"); defense.Layers=new[] { new DefenseSlot(shield) };
            var hp=Asset<HealthDefinition>("Health","Target"); hp.MaximumHealth=1000;
            var protectedHp=Asset<HealthDefinition>("Health","ShieldTarget"); protectedHp.MaximumHealth=1000; protectedHp.Defense=defense;
            var presentation=Asset<CombatPresentationDefinition>("Presentation","OutputRules"); presentation.Capacity=512; presentation.MaximumPerFrame=32;
            presentation.Rules=new[] { new CombatPresentationRule(CombatPresentationKind.Damage,.08f,12),new CombatPresentationRule(CombatPresentationKind.Shield,.08f,12) };
            var lab=new GameObject("Combat Lab").AddComponent<CombatLab>();
            lab.Hub=lab.gameObject.AddComponent<CombatPresentationHub>();
            var hubSerialized=new SerializedObject(lab.Hub); hubSerialized.FindProperty("definition").objectReferenceValue=presentation; hubSerialized.ApplyModifiedPropertiesWithoutUndo();
            lab.Journal=lab.gameObject.AddComponent<CombatJournal>();
            lab.Targets=new Health[8];
            for(var i=0;i<lab.Targets.Length;i++)
            {
                var angle=i*Mathf.PI/4; var target=Shape("Target "+(i+1),PrimitiveType.Capsule,new Vector3(Mathf.Sin(angle)*4,1,Mathf.Cos(angle)*4),Vector3.one,i==1 ? blue : teal);
                var health=target.AddComponent<Health>(); health.ApplyDefinition(i==1 ? protectedHp : hp); target.AddComponent<StatusController>();
                target.AddComponent<CombatPresentationRelay>().Hub=lab.Hub; lab.Targets[i]=health;
            }
            var player=Shape("Source",PrimitiveType.Cube,new Vector3(0,1,0),Vector3.one*.6f,blue,false); player.AddComponent<Health>(); player.AddComponent<CombatReactions>();
            lab.Emitter=player.AddComponent<DamageEmitter>(); lab.Emitter.Pool=lab.gameObject.AddComponent<ProjectilePool>(); lab.Emitter.AreaPool=lab.gameObject.AddComponent<DamageAreaPool>();
            lab.Emitter.Pool.Prefab=prefab; var poolSerialized=new SerializedObject(lab.Emitter.Pool); poolSerialized.FindProperty("maximumActive").intValue=1000; poolSerialized.ApplyModifiedPropertiesWithoutUndo();
            lab.Melee=player.AddComponent<CombatMelee>(); lab.Melee.OwnerRoot=player.transform; lab.Tip=new GameObject("Weapon anchor").transform; lab.Tip.SetParent(player.transform,false);
            player.AddComponent<CombatPresentationRelay>().Hub=lab.Hub;
            lab.Weapon=player.AddComponent<LineRenderer>(); lab.Weapon.sharedMaterial=orange; lab.Weapon.enabled=false;
            lab.Hitscan=hitscan; lab.PoisonShot=poisonShot; lab.Swarm=swarm; lab.PoisonReaction=reaction; lab.Poison=poison; lab.Burst=burst; lab.Slash=slash;
            var camera=new GameObject("Main Camera").AddComponent<Camera>(); camera.tag="MainCamera"; camera.orthographic=true; camera.orthographicSize=10;
            camera.transform.position=new Vector3(0,22,-10); camera.transform.LookAt(Vector3.zero); camera.transform.position+=camera.transform.right*-3.8f;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.035f,.06f); lab.View=camera;
            foreach(var guid in AssetDatabase.FindAssets("t:ScriptableObject",new[] { root })) EditorUtility.SetDirty(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
            if(!poisonShot.IsValid || !hitscan.IsValid || !slash.IsValid || !swarm.IsValid || !reaction.IsValid || !presentation.IsValid) throw new InvalidOperationException("Invalid sample recipe.");
            var path=root+"/CombatLab.unity"; EditorSceneManager.SaveScene(scene,path); AssetDatabase.SaveAssets(); Debug.Log("EASYCOMBAT_LAB_CREATED "+path); return path;
        }
        public static void BuildWebGL()
        {
            var path=CreateScene(); PlayerSettings.productName="EasyCombat Lab"; PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled; PlayerSettings.WebGL.dataCaching=false;
            var output=Environment.GetEnvironmentVariable("EASYCOMBAT_WEB_OUTPUT") ?? "Build/WebGL";
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[] { path },locationPathName=output,target=BuildTarget.WebGL,options=BuildOptions.Development });
            if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("WebGL build failed: "+report.summary.result);
            Debug.Log("EASYCOMBAT_WEBGL_BUILD_OK "+report.summary.totalSize);
        }
    }
}
