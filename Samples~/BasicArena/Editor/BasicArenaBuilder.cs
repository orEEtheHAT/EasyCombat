using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EasyCombat.Samples.Editor
{
    public static class BasicArenaBuilder
    {
        private static string root;
        private static T Asset<T>(string category, string name) where T : ScriptableObject
        {
            var folder = root + "/" + category;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(root, category);
            var asset = ScriptableObject.CreateInstance<T>(); asset.name = name;
            AssetDatabase.CreateAsset(asset, folder + "/" + name + ".asset"); return asset;
        }
        private static Material Material(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, root + "/" + name + ".mat"); return material;
        }
        private static GameObject Shape(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(primitive); go.name = name;
            go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        [MenuItem("EasyCombat/Samples/Create Basic Arena")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene();
        }
        public static string CreateScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/EasyCombatSamples")) AssetDatabase.CreateFolder("Assets", "EasyCombatSamples");
            root = AssetDatabase.GenerateUniqueAssetPath("Assets/EasyCombatSamples/BasicArena");
            AssetDatabase.CreateFolder("Assets/EasyCombatSamples", Path.GetFileName(root));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = Material("Floor", new Color(.08f,.11f,.17f));
            var teal = Material("Teal", new Color(.1f,.8f,.7f));
            var orange = Material("Fire", new Color(1,.32f,.08f));
            var blue = Material("Shield", new Color(.25f,.55f,1));
            Shape("Ground", PrimitiveType.Cube, new Vector3(0,-.2f,5), new Vector3(20,.4f,22), floor);
            var fire = Asset<DamageTypeDefinition>("DamageTypes", "Fire"); fire.DisplayName = "Fire"; fire.Color = orange.color;
            var physical = Asset<DamageTypeDefinition>("DamageTypes", "Physical");
            var tick = Asset<PeriodicDamageEffect>("StatusEffects", "BurnTick"); tick.DamageType = fire; tick.DamagePerStack = 3;
            var burn = Asset<StatusDefinition>("Statuses", "Burn"); burn.Duration = 4; burn.TickInterval = 1;
            burn.MaximumStacks = 3; burn.TickEffects = new StatusTickEffect[] { tick };
            var explosion = Asset<AreaDefinition>("Areas", "Explosion"); explosion.Radius = 2.7f;
            var groundArea = Asset<AreaDefinition>("Areas", "FireGround"); groundArea.Radius = 2.7f;
            var pulseAttack = Asset<AttackDefinition>("Weapons", "FireGroundTick");
            pulseAttack.Delivery = DamageDelivery.Area; pulseAttack.Area = groundArea; pulseAttack.Damage = 2; pulseAttack.DamageType = fire;
            pulseAttack.OnHitStatuses = new[] { new StatusApplication(burn) };
            var pulses = Asset<AreaPulseDefinition>("Areas", "GroundPulses"); pulses.Attack = pulseAttack; pulses.Duration = 5; pulses.Interval = 1;
            var fieldObject = new GameObject("FireField"); fieldObject.AddComponent<DamageArea>();
            var disk = Shape("Visual", PrimitiveType.Cylinder, Vector3.zero, new Vector3(5.4f,.025f,5.4f), orange, false);
            disk.transform.SetParent(fieldObject.transform, false);
            var fieldPrefab = PrefabUtility.SaveAsPrefabAsset(fieldObject, root + "/FireField.prefab").GetComponent<DamageArea>();
            UnityEngine.Object.DestroyImmediate(fieldObject);
            var spawn = Asset<AreaSpawnDefinition>("Areas", "SpawnFireGround"); spawn.Pulses = pulses; spawn.Prefab = fieldPrefab;
            // Ignore actor colliders while projecting the field to the floor (built-in layer 0).
            spawn.GroundMask = 1;
            var projectileObject = Shape("Fireball", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .35f, orange, false);
            projectileObject.AddComponent<DamageProjectile>();
            var projectile = PrefabUtility.SaveAsPrefabAsset(projectileObject, root + "/Fireball.prefab").GetComponent<DamageProjectile>();
            UnityEngine.Object.DestroyImmediate(projectileObject);
            var laser = Asset<AttackDefinition>("Weapons", "WideHitscan"); laser.DisplayName = "Wide hitscan / piercing";
            laser.Damage = 18; laser.DamageType = physical; laser.HitscanRadius = .3f; laser.ContactBehavior = ContactBehavior.PierceTargets; laser.MaximumTargets = 3;
            var fireball = Asset<AttackDefinition>("Weapons", "Fireball"); fireball.DisplayName = "Fireball / explosion + ground";
            fireball.Delivery = DamageDelivery.Projectile; fireball.Damage = 12; fireball.DamageType = fire;
            fireball.ProjectileSpeed = 8; fireball.ProjectilePrefab = projectile; fireball.Area = explosion; fireball.ImpactArea = spawn;
            fireball.OnHitStatuses = new[] { new StatusApplication(burn) };
            var cone = Asset<AreaDefinition>("Areas", "Cone"); cone.Shape = AreaShape.Cone; cone.ConeAngle = 80; cone.Radius = 14;
            var melee = Asset<AttackDefinition>("Weapons", "ConeAttack"); melee.DisplayName = "Cone / immediate AOE"; melee.Delivery = DamageDelivery.Area;
            melee.Area = cone; melee.Damage = 10; melee.DamageType = physical;
            var armor = Asset<FlatArmorLayer>("Defense", "Armor"); armor.Reduction = 4;
            var shield = Asset<ShieldLayer>("Defense", "Shield"); shield.Capacity = 40; shield.RechargePerSecond = 8; shield.DelayAfterAbsorption = 2;
            var defense = Asset<DefenseProfile>("Defense", "ArmorAndShield"); defense.Layers = new[] { new DefenseSlot(armor), new DefenseSlot(shield) };
            var resist = Asset<ResistanceProfile>("Health", "FireResistance"); resist.Configure(new DamageResistance(fire,.5f));
            var healthProfiles = new HealthDefinition[3];
            for (var i = 0; i < 3; i++) healthProfiles[i] = Asset<HealthDefinition>("Health", "Target" + (i+1));
            healthProfiles[1].Defense = defense; healthProfiles[2].Resistances = resist;
            var arena = new GameObject("Basic Arena").AddComponent<BasicArena>();
            arena.Targets = new Health[3];
            for (var i = 0; i < 3; i++)
            {
                var actor = Shape(new[] { "01 Normal", "02 Armor + Shield", "03 Fire resistance" }[i], PrimitiveType.Capsule,
                    new Vector3(0,1,3+i*3), Vector3.one, i == 1 ? blue : teal);
                actor.layer = 2; // Ignore Raycast is explicitly included in attack masks, excluded only by ground projection.
                var health = actor.AddComponent<Health>(); health.ApplyDefinition(healthProfiles[i]);
                actor.AddComponent<StatusController>(); arena.Targets[i] = health;
            }
            var emitter = new GameObject("Emitter").AddComponent<DamageEmitter>(); emitter.transform.position = new Vector3(0,1,-2);
            emitter.Pool = arena.gameObject.AddComponent<ProjectilePool>(); emitter.Pool.Prefab = projectile;
            emitter.AreaPool = arena.gameObject.AddComponent<DamageAreaPool>();
            arena.Emitter = emitter; arena.Attacks = new[] { laser,fireball,melee }; emitter.Attack = laser;
            arena.Journal = arena.gameObject.AddComponent<CombatJournal>();
            var line = arena.gameObject.AddComponent<LineRenderer>(); line.sharedMaterial = teal; line.enabled = false;
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(12,16,-10); camera.transform.LookAt(new Vector3(-2,0,5));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.035f,.06f);
            camera.orthographic = true; camera.orthographicSize = 9; arena.View = camera;
            var light = new GameObject("Light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45,30,0);
            foreach (var path in AssetDatabase.FindAssets("t:ScriptableObject", new[] { root }))
                EditorUtility.SetDirty(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(path)));
            var scenePath = root + "/BasicArena.unity";
            EditorSceneManager.SaveScene(scene, scenePath); AssetDatabase.SaveAssets();
            if (!laser.IsValid || !fireball.IsValid || !melee.IsValid) throw new InvalidOperationException("Generated sample recipes are invalid.");
            Debug.Log("EASYCOMBAT_SAMPLE_CREATED " + scenePath); return scenePath;
        }
        public static void BuildWebGL()
        {
            var path = CreateScene();
            PlayerSettings.productName = "EasyCombat Basic Arena";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = false;
            var output = Environment.GetEnvironmentVariable("EASYCOMBAT_WEB_OUTPUT") ?? "Build/WebGL";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { path }, locationPathName = output,
                target = BuildTarget.WebGL, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("WebGL build failed: " + report.summary.result);
            Debug.Log("EASYCOMBAT_WEBGL_BUILD_OK " + report.summary.totalSize);
        }
    }
}
