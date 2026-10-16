using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    public enum CombatTemplateKind { MixedFirePoisonWeapon,FireballAndGround,ShieldedEnemy,MeleeAbility,FreezeBuildup,Lifesteal,PoisonCombination,BossBodyPart }
    public static class CombatTemplates
    {
        public static ScriptableObject Create(CombatTemplateKind kind,string parent="Assets/EasyCombatTemplates")
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before creating templates.");
            parent=(parent ?? "").Replace('\\','/').TrimEnd('/');
            if(parent!="Assets" && !parent.StartsWith("Assets/",StringComparison.Ordinal) || parent.Contains("..")) throw new ArgumentException("Template folder must be under Assets.");
            if(kind<CombatTemplateKind.MixedFirePoisonWeapon || kind>CombatTemplateKind.BossBodyPart) throw new ArgumentOutOfRangeException(nameof(kind));
            CombatAssetLibrary.EnsureFolder(parent); var root=AssetDatabase.GenerateUniqueAssetPath(parent+"/"+kind);
            CombatAssetLibrary.EnsureFolder(root); var builder=new Builder(root,parent+"/Common"); var result=builder.Build(kind);
            foreach(var asset in builder.Created) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            var issues=CombatAssetLibrary.Issues(result); if(issues.Count>0) throw new InvalidOperationException("Template validation failed: "+string.Join("; ",issues));
            Selection.activeObject=result; EditorGUIUtility.PingObject(result); return result;
        }
        private sealed class Builder
        {
            private readonly string root,common;
            internal readonly List<UnityEngine.Object> Created=new List<UnityEngine.Object>();
            internal Builder(string root,string common) { this.root=root; this.common=common; }
            private T Asset<T>(string name) where T:ScriptableObject
            {
                var category=CombatAssetLibrary.Category(typeof(T)); var folder=root+"/"+(category>=0 ? CombatAssetLibrary.Folders[category] : "Definitions"); CombatAssetLibrary.EnsureFolder(folder);
                var value=ScriptableObject.CreateInstance<T>(); value.name=name; AssetDatabase.CreateAsset(value,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+name+".asset")); Created.Add(value); return value;
            }
            private DamageTypeDefinition Type(string name,Color color)
            {
                var folder=common+"/DamageTypes"; CombatAssetLibrary.EnsureFolder(folder); var path=folder+"/"+name+".asset";
                var existing=AssetDatabase.LoadMainAssetAtPath(path);
                if(existing!=null) { if(existing is DamageTypeDefinition type) return type; throw new InvalidOperationException("Common type path is occupied: "+path); }
                var created=ScriptableObject.CreateInstance<DamageTypeDefinition>(); created.name=created.DisplayName=name; created.Color=color; AssetDatabase.CreateAsset(created,path); Created.Add(created); return created;
            }
            private DamageTypeDefinition Fire=>Type("Fire",new Color(1,.35f,.1f));
            private DamageTypeDefinition Poison=>Type("Poison",new Color(.55f,.9f,.15f));
            private DamageTypeDefinition Physical=>Type("Physical",new Color(.8f,.85f,.9f));
            private StatusDefinition Dot(string name,DamageTypeDefinition type,float damage,float duration,int stacks)
            {
                var tick=Asset<PeriodicDamageEffect>(name+"Tick"); tick.DamageType=type; tick.DamagePerStack=damage;
                var status=Asset<StatusDefinition>(name); status.DisplayName=name; status.Duration=duration; status.TickInterval=1; status.MaximumStacks=stacks;
                status.Stacking=StatusStacking.Independent; status.TickEffects=new StatusTickEffect[] { tick }; return status;
            }
            private Material FireMaterial()
            {
                var folder=common+"/Visuals"; CombatAssetLibrary.EnsureFolder(folder); var path=folder+"/Fire.mat"; var existing=AssetDatabase.LoadMainAssetAtPath(path);
                if(existing!=null) { if(existing is Material material) return material; throw new InvalidOperationException("Material path is occupied."); }
                var shader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"); if(shader==null) throw new InvalidOperationException("No supported unlit shader found.");
                var result=new Material(shader) { name="Fire",color=new Color(1,.35f,.1f) }; if(result.HasProperty("_BaseColor")) result.SetColor("_BaseColor",result.color);
                AssetDatabase.CreateAsset(result,path); Created.Add(result); return result;
            }
            private DamageProjectile Projectile()
            {
                var folder=root+"/Prefabs"; CombatAssetLibrary.EnsureFolder(folder); var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                try
                {
                    go.name="FireProjectile"; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.localScale=Vector3.one*.24f;
                    go.GetComponent<Renderer>().sharedMaterial=FireMaterial(); go.AddComponent<DamageProjectile>();
                    return PrefabUtility.SaveAsPrefabAsset(go,folder+"/FireProjectile.prefab").GetComponent<DamageProjectile>();
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            private DamageArea Field()
            {
                var folder=root+"/Prefabs"; CombatAssetLibrary.EnsureFolder(folder); var go=new GameObject("FireGround");
                try
                {
                    go.AddComponent<DamageArea>(); var disk=GameObject.CreatePrimitive(PrimitiveType.Cylinder); disk.name="Radius Visual"; disk.transform.SetParent(go.transform,false);
                    UnityEngine.Object.DestroyImmediate(disk.GetComponent<Collider>()); disk.transform.localScale=new Vector3(2,.01f,2); disk.GetComponent<Renderer>().sharedMaterial=FireMaterial();
                    var visual=go.AddComponent<DamageAreaVisual>(); visual.RadiusRoot=disk.transform; visual.ReferenceRadius=1;
                    return PrefabUtility.SaveAsPrefabAsset(go,folder+"/FireGround.prefab").GetComponent<DamageArea>();
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            internal ScriptableObject Build(CombatTemplateKind kind)
            {
                switch(kind)
                {
                    case CombatTemplateKind.MixedFirePoisonWeapon:
                    {
                        var attack=Asset<AttackDefinition>("FirePoisonWeapon"); attack.DamageChannels=new[] { new DamageChannel(Fire,2,4),new DamageChannel(Poison,7,9) };
                        attack.CriticalChance=.1f; attack.OnHitStatuses=new[] { new StatusApplication(Dot("Burn",Fire,3,4,3),.25f) }; return attack;
                    }
                    case CombatTemplateKind.FireballAndGround:
                    {
                        var burn=Dot("Burn",Fire,3,4,3); var ground=Asset<AreaDefinition>("GroundArea"); ground.Radius=3;
                        var tick=Asset<AttackDefinition>("GroundPulse"); tick.Delivery=DamageDelivery.Area; tick.Area=ground; tick.DamageType=Fire; tick.Damage=2; tick.OnHitStatuses=new[] { new StatusApplication(burn) };
                        var group=Asset<AreaOverlapGroupDefinition>("FireGroundOverlap"); group.Mode=AreaOverlapMode.OnePerTarget; group.Interval=1;
                        var pulse=Asset<AreaPulseDefinition>("GroundLifetime"); pulse.Attack=tick; pulse.Duration=6; pulse.Interval=1; pulse.ApplyImmediately=false; pulse.OverlapGroup=group;
                        var spawn=Asset<AreaSpawnDefinition>("SpawnGround"); spawn.Pulses=pulse; spawn.Prefab=Field();
                        var blast=Asset<AreaDefinition>("Explosion"); blast.Radius=3;
                        var attack=Asset<AttackDefinition>("Fireball"); attack.Delivery=DamageDelivery.Projectile; attack.MinimumDamage=10; attack.MaximumDamage=15; attack.DamageType=Fire;
                        attack.CriticalChance=.1f; attack.ProjectilePrefab=Projectile(); attack.ProjectileSpeed=12; attack.Area=blast; attack.ImpactArea=spawn; attack.OnHitStatuses=new[] { new StatusApplication(burn,.3f) }; return attack;
                    }
                    case CombatTemplateKind.ShieldedEnemy:
                    {
                        var armor=Asset<FlatArmorLayer>("Armor"); armor.Reduction=2; var shield=Asset<ShieldLayer>("Shield"); shield.Capacity=75; shield.RechargePerSecond=5; shield.DelayAfterAbsorption=2;
                        var defense=Asset<DefenseProfile>("Protection"); defense.Layers=new[] { new DefenseSlot(armor),new DefenseSlot(shield) };
                        var resistance=Asset<ResistanceProfile>("Resistances"); resistance.Configure(new DamageResistance(Fire,.5f),new DamageResistance(Poison,-.25f));
                        var hp=Asset<HealthDefinition>("ShieldedEnemy"); hp.MaximumHealth=250; hp.Defense=defense; hp.Resistances=resistance; return hp;
                    }
                    case CombatTemplateKind.MeleeAbility:
                    {
                        var stamina=Asset<ResourceDefinition>("Stamina"); stamina.Maximum=50; stamina.RegenerationPerSecond=8; stamina.DelayAfterSpend=1;
                        var resources=Asset<ResourceProfile>("MeleeResources"); resources.Resources=new[] { stamina };
                        var attack=Asset<AttackDefinition>("SlashDamage"); attack.MinimumDamage=15; attack.MaximumDamage=20; attack.DamageType=Physical; attack.CriticalChance=.15f;
                        var melee=Asset<MeleeDefinition>("SlashWindow"); melee.Attack=attack; melee.Duration=.6f; melee.Windows=new[] { new MeleeWindow(.15f,.4f) }; melee.Radius=.3f;
                        var action=Asset<AbilityMeleeAction>("StartSlash"); action.Melee=melee;
                        var ability=Asset<AbilityDefinition>("Slash"); ability.TargetMode=AbilityTargetMode.Direction; ability.MaximumCharges=0; ability.Cooldown=.6f; ability.Costs=new[] { new ResourceCost(stamina,10) }; ability.Actions=new CombatAbilityAction[] { action }; return ability;
                    }
                    case CombatTemplateKind.FreezeBuildup:
                    {
                        var ice=Type("Ice",new Color(.3f,.75f,1)); var frozen=Asset<StatusDefinition>("Frozen"); frozen.Stacking=StatusStacking.RefreshDuration; frozen.MaximumStacks=1; frozen.Duration=2;
                        frozen.Restrictions=CombatRestriction.Move|CombatRestriction.Turn|CombatRestriction.Attack|CombatRestriction.Cast|CombatRestriction.Jump;
                        var action=Asset<ReactionStatusAction>("ApplyFreeze"); action.Status=frozen;
                        var buildup=Asset<BuildupDefinition>("FreezeBuildup"); buildup.FilterType=true; buildup.DamageType=ice; buildup.Threshold=100; buildup.PerHealthDamage=2; buildup.TriggerCooldown=3; buildup.Actions=new CombatReactionAction[] { action }; return buildup;
                    }
                    case CombatTemplateKind.Lifesteal:
                    {
                        var healing=Asset<HealingDefinition>("LifestealHealing"); healing.Terms=new[] { new HealingTerm(HealingBasis.TriggeringHealthDamage,.1f) };
                        var action=Asset<ReactionHealingAction>("LifestealAction"); action.Healing=healing;
                        var reaction=Asset<ReactionDefinition>("Lifesteal"); reaction.Trigger=ReactionTrigger.DamageDealt; reaction.Target=ReactionTarget.Owner; reaction.MaximumProcsPerResolution=32; reaction.Actions=new CombatReactionAction[] { action }; return reaction;
                    }
                    case CombatTemplateKind.PoisonCombination:
                    {
                        var poison=Dot("Poison",Poison,2,6,5); var burst=Dot("PoisonBurst",Poison,25,.1f,1); burst.TickInterval=.1f;
                        var attack=Asset<AttackDefinition>("PoisonWeapon"); attack.Damage=5; attack.DamageType=Poison; attack.OnHitStatuses=new[] { new StatusApplication(poison) };
                        var combination=Asset<StatusCombinationDefinition>("ThreeStacksToBurst"); combination.Consume=poison; combination.Stacks=3; combination.Apply=burst;
                        var action=Asset<ReactionStatusCombinationAction>("ConsumePoison"); action.Combination=combination;
                        var reaction=Asset<ReactionDefinition>("PoisonCombo"); reaction.Trigger=ReactionTrigger.DamageDealt; reaction.Target=ReactionTarget.Victim; reaction.DamageType=Poison; reaction.Actions=new CombatReactionAction[] { action }; return reaction;
                    }
                    default:
                    {
                        var core=Asset<HealthDefinition>("BossCore"); core.MaximumHealth=1000; var local=Asset<HealthDefinition>("PartDurability"); local.MaximumHealth=100;
                        var burst=Asset<AttackDefinition>("BreakDamage"); burst.Damage=50; burst.DamageType=Physical;
                        var part=Asset<BodyPartDefinition>("BossPart"); part.Health=local; part.CoreDamageFraction=.5f; part.CoreBreakAttack=burst; return part;
                    }
                }
            }
        }
    }
    public sealed class CombatTemplatesWindow:EditorWindow
    {
        private CombatTemplateKind kind;
        private string parent="Assets/EasyCombatTemplates";
        [MenuItem("EasyCombat/Tools/Recipe Templates")]
        public static void Open() { var window=GetWindow<CombatTemplatesWindow>(); window.titleContent=new GUIContent("Combat Templates"); window.minSize=new Vector2(510,230); window.Show(); }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Готовые схемы механик",EditorStyles.boldLabel);
            kind=(CombatTemplateKind)EditorGUILayout.EnumPopup("Шаблон",kind); parent=EditorGUILayout.TextField("Папка",parent);
            EditorGUILayout.HelpBox("Каждый запуск создаёт отдельную папку и сохраняет связи между SO. Типы урона общие в Common внутри выбранной папки. Для существующих типов твоей игры замени ссылки в новых SO. Визуал огненного шара/лужи — заменяемые примитивы.",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if(GUILayout.Button("Создать и выбрать основной SO",GUILayout.Height(32)))
                    try { CombatTemplates.Create(kind,parent); } catch(Exception exception) { Debug.LogException(exception); }
        }
    }
}
