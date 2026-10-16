using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class CombatAssetRecord
    {
        public ScriptableObject Asset;
        public int Category;
        public string Path;
        public string Title;
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public static class CombatAssetLibrary
    {
        public static string DemoRoot => AssetDatabase.IsValidFolder("Assets/GamesFactory/Combat/Demo") ? "Assets/GamesFactory/Combat/Demo" : "Assets/EasyCombat";
        public static string DefinitionsRoot => DemoRoot + "/Definitions";
        public static readonly Type[] Types = { typeof(AttackDefinition), typeof(DamageTypeDefinition), typeof(HealthDefinition),
            typeof(ResistanceProfile), typeof(StatusDefinition), typeof(StatusTickEffect), typeof(CombatStatDefinition), typeof(AreaDefinition), typeof(AreaPulseDefinition), typeof(CombatStatsDefinition), typeof(AreaSpawnDefinition), typeof(DamageFormulaDefinition), typeof(AreaOverlapGroupDefinition), typeof(HitRulesDefinition), typeof(DefenseProfile), typeof(DefenseLayerDefinition), typeof(StatModifierDefinition), typeof(ResourceDefinition), typeof(ResourceProfile), typeof(HealingDefinition), typeof(CombatTagDefinition), typeof(ReactionDefinition), typeof(CombatReactionAction), typeof(DamageProtectionPolicy), typeof(DamageModifierProfile), typeof(StatusDefenseProfile), typeof(StatusCombinationDefinition), typeof(LifeRulesDefinition), typeof(AttributionDefinition), typeof(DamageRedirectionDefinition), typeof(BodyPartDefinition), typeof(BuildupDefinition), typeof(AbilityDefinition), typeof(CombatAbilityAction), typeof(MeleeDefinition), typeof(BeamDefinition), typeof(AreaTriggerDefinition) };
        public static readonly string[] Titles = { "Оружие", "Типы урона", "Здоровье", "Сопротивления", "Статусы", "Действия тика", "Характеристики", "Области AOE", "Импульсы областей", "Профили характеристик", "Создание областей", "Формулы урона", "Пересечения областей", "Правила попадания", "Профили защиты", "Слои защиты", "Наборы модификаторов", "Ресурсы", "Профили ресурсов", "Лечение", "Теги", "Реакции", "Действия реакций", "Пробивание защиты", "Модификаторы урона", "Защита от статусов", "Комбинации статусов", "Состояния жизни", "Учёт убийств", "Перенаправление урона", "Части тела", "Накопление эффектов", "Способности", "Действия способностей", "Ближний бой", "Непрерывные лучи", "Вход и выход из области" };
        public static readonly string[] Folders = { "Weapons", "DamageTypes", "HealthProfiles", "ResistanceProfiles", "Statuses", "StatusEffects", "Stats", "Areas", "AreaPulses", "StatProfiles", "AreaSpawns", "DamageFormulas", "AreaOverlapGroups", "HitRules", "DefenseProfiles", "DefenseLayers", "StatModifiers", "Resources", "ResourceProfiles", "Healing", "Tags", "Reactions", "ReactionActions", "DamageProtection", "DamageModifiers", "StatusDefense", "StatusCombinations", "LifeRules", "Attribution", "Redirection", "BodyParts", "Buildup", "Abilities", "AbilityActions", "Melee", "Beams", "AreaTriggers" };
        public static readonly string[] Codes = { "WP", "DM", "HP", "RS", "FX", "TK", "ST", "AO", "AP", "SP", "AS", "DF", "OG", "HR", "DP", "DL", "SM", "RE", "RP", "HE", "TG", "RX", "RA", "BP", "MD", "SD", "SC", "LF", "KC", "RD", "PT", "BU", "AB", "AA", "ML", "BM", "AT" };
        public static readonly Color[] Colors = {
            new Color(.30f,.87f,.78f), new Color(1f,.65f,.35f), new Color(.44f,.77f,1f),
            new Color(.71f,.65f,1f), new Color(.92f,.58f,.84f), new Color(.94f,.79f,.38f), new Color(.62f,.82f,.59f),
            new Color(1f,.55f,.35f), new Color(.50f,.79f,1f), new Color(.62f,.82f,.59f), new Color(1f,.70f,.30f), new Color(1f,.65f,.35f), new Color(.50f,.79f,1f),
            new Color(.71f,.65f,1f), new Color(.44f,.77f,1f), new Color(.30f,.87f,.78f),
            new Color(.62f,.82f,.59f), new Color(.4f,.7f,1f), new Color(.4f,.7f,1f), new Color(.4f,1f,.6f), new Color(.8f,.7f,.5f), new Color(.9f,.5f,.7f), new Color(.9f,.5f,.7f), new Color(1f,.7f,.4f), new Color(.7f,.8f,.4f), new Color(.7f,.6f,.9f), new Color(.9f,.6f,.4f), new Color(.9f,.4f,.5f), new Color(.7f,.8f,.9f), new Color(.4f,.8f,.9f), new Color(.9f,.65f,.5f), new Color(.5f,.8f,.95f), new Color(.6f,.6f,1f), new Color(.7f,.6f,1f), new Color(1f,.6f,.4f), new Color(.4f,.9f,1f), new Color(.4f,.9f,.6f)
        };

        public static int Category(Type type)
        {
            for (var i = 0; i < Types.Length; i++) if (Types[i].IsAssignableFrom(type)) return i;
            return -1;
        }
        public static string Folder(Type type)
        {
            var category = Category(type);
            if (category < 0) throw new ArgumentException("Unsupported combat definition type.", nameof(type));
            return DefinitionsRoot + "/" + Folders[category];
        }
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Asset folder must be inside Assets.", nameof(path));
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
        public static string DemoPath(Type type, string name) => Folder(type) + "/" + name + ".asset";

        public static List<CombatAssetRecord> Scan()
        {
            var records = new List<CombatAssetRecord>();
            var seen = new HashSet<string>();
            for (var category = 0; category < Types.Length; category++)
                foreach (var guid in AssetDatabase.FindAssets("t:" + Types[category].Name, new[] { "Assets" }))
                {
                    if (!seen.Add(guid)) continue;
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset == null || Category(asset.GetType()) < 0) continue;
                    records.Add(new CombatAssetRecord { Asset = asset, Category = Category(asset.GetType()), Path = path, Title = DisplayName(asset) });
                }
            records.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
            return records;
        }
        public static string DisplayName(ScriptableObject asset)
        {
            if (asset is AttackDefinition weapon) return weapon.Label;
            if (asset is DamageTypeDefinition type) return type.Label;
            if (asset is StatusDefinition status) return status.Label;
            if (asset is DefenseLayerDefinition layer && !string.IsNullOrWhiteSpace(layer.DisplayName)) return layer.DisplayName;
            if (asset is CombatStatDefinition stat && !string.IsNullOrWhiteSpace(stat.DisplayName)) return stat.DisplayName;
            return asset.name;
        }

        /// <summary>Move through AssetDatabase so GUIDs and every scene/SO reference survive.</summary>
        [MenuItem("EasyCombat/Organize Demo Definitions")]
        public static void OrganizeDemoAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before moving assets.");
            var demoRoot = DemoRoot + "/";
            var records = Scan();
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in records)
            {
                if (!record.Path.StartsWith(demoRoot, StringComparison.Ordinal)) continue;
                var target = Folder(record.Asset.GetType()) + "/" + Path.GetFileName(record.Path);
                if (record.Path == target) continue;
                if (!destinations.Add(target)) throw new InvalidOperationException("Two definitions have the same destination: " + target);
                if (AssetDatabase.LoadMainAssetAtPath(target) != null)
                    throw new InvalidOperationException("Destination already exists; nothing was overwritten: " + target);
            }
            foreach (var type in Types) EnsureFolder(Folder(type));
            var count = 0;
            foreach (var record in records)
            {
                if (!record.Path.StartsWith(demoRoot, StringComparison.Ordinal)) continue;
                var target = Folder(record.Asset.GetType()) + "/" + Path.GetFileName(record.Path);
                if (record.Path == target) continue;
                var before = AssetDatabase.AssetPathToGUID(record.Path);
                var error = AssetDatabase.MoveAsset(record.Path, target);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                if (AssetDatabase.AssetPathToGUID(target) != before) throw new InvalidOperationException("GUID changed during move: " + target);
                count++;
            }
            var oldFolder = DemoRoot + "/Attacks";
            if (AssetDatabase.IsValidFolder(oldFolder) && Directory.GetFileSystemEntries(oldFolder).Length == 0)
                AssetDatabase.DeleteAsset(oldFolder);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Combat] Organized {count} definitions; GUIDs preserved.");
        }

        public static ScriptableObject Create(Type type, string path)
        {
            if (type.IsAbstract || Category(type) < 0) throw new ArgumentException("Choose a concrete combat asset type.");
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Asset already exists: " + path);
            var asset = (ScriptableObject)ScriptableObject.CreateInstance(type);
            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }
        public static ScriptableObject Duplicate(ScriptableObject source)
        {
            var sourcePath = AssetDatabase.GetAssetPath(source);
            // A project-wide browser must keep variants in the original game's collection.
            var folder = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            var target = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + source.name + " Variant.asset");
            if (!AssetDatabase.CopyAsset(sourcePath, target)) throw new InvalidOperationException("Could not duplicate " + sourcePath);
            var result = AssetDatabase.LoadAssetAtPath<ScriptableObject>(target);
            var serialized = new SerializedObject(result);
            var label = serialized.FindProperty("DisplayName");
            if (label != null && !string.IsNullOrWhiteSpace(label.stringValue))
            {
                label.stringValue += " · вариант";
                serialized.ApplyModifiedProperties();
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        public static List<ScriptableObject> Dependencies(ScriptableObject source, bool reverse, List<CombatAssetRecord> library)
        {
            var result = new List<ScriptableObject>();
            var path = AssetDatabase.GetAssetPath(source);
            if (reverse)
            {
                foreach (var item in library)
                    if (item.Asset != source && Array.IndexOf(AssetDatabase.GetDependencies(item.Path, false), path) >= 0)
                        result.Add(item.Asset);
            }
            else
                foreach (var dependency in AssetDatabase.GetDependencies(path, false))
                {
                    if (dependency == path) continue;
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(dependency);
                    if (asset != null && Category(asset.GetType()) >= 0) result.Add(asset);
                }
            return result;
        }
        public static List<string> Issues(ScriptableObject asset)
        {
            var issues = new List<string>();
            if (asset is AttackDefinition weapon)
            {
                if (!weapon.IsValid) issues.Add("Некорректные параметры оружия — выстрел будет отклонён.");
                if (weapon.ChannelCount > DamageAmounts.Capacity) issues.Add("Один удар поддерживает до 8 частей урона.");
                if (weapon.OnHitStatuses != null)
                    foreach (var status in weapon.OnHitStatuses)
                        if (status.Status == null) issues.Add("В списке попадания есть пустой статус.");
            }
            if (asset is AbilityDefinition ability && !ability.IsValid) issues.Add("Способности нужны корректные сроки, стоимость и 1–8 действий.");
            if (asset is CombatAbilityAction abilityAction && !abilityAction.IsValid) issues.Add("Некорректное действие способности.");
            if (asset is MeleeDefinition melee && !melee.IsValid) issues.Add("Проверь пакет, радиус и упорядоченные окна взмаха.");
            if (asset is BeamDefinition beam && !beam.IsValid) issues.Add("Лучу нужен Hitscan-пакет и корректный интервал.");
            if (asset is AreaTriggerDefinition trigger && !trigger.IsValid) issues.Add("Проверь геометрию и пакеты входа/пребывания/выхода.");
            if (asset is BodyPartDefinition bodyPart && !bodyPart.IsValid) issues.Add("Части нужен корректный Health без Downed и доля передачи 0–1.");
            if (asset is BuildupDefinition buildup && !buildup.IsValid) issues.Add("Шкале нужен положительный порог и 1–8 корректных действий.");
            if (asset is DamageRedirectionDefinition redirection && !redirection.IsValid) issues.Add("Проверь долю, лимит и дистанцию перенаправления.");
            if (asset is LifeRulesDefinition life && !life.IsValid) issues.Add("Проверь нижний предел HP и параметры возрождения.");
            if (asset is AttributionDefinition attribution && !attribution.IsValid) issues.Add("Проверь окно учёта, долю помощи и бюджет участников.");
            if (asset is StatusCombinationDefinition combination && !combination.IsValid) issues.Add("Комбинации нужны два разных статуса и стоимость 1–32 живых стака.");
            if (asset is StatusDefenseProfile statusDefense && !statusDefense.IsValid) issues.Add("Проверь защиту от статусов: шанс, время и силу.");
            if (asset is DamageProtectionPolicy protection && !protection.IsValid) issues.Add("Проверь пробивание резистов (0–1).");
            if (asset is DamageModifierProfile damageModifiers && !damageModifiers.IsValid) issues.Add("Проверь до 16 правил урона и конверсию типов.");
            if (asset is ReactionDefinition reaction && !reaction.IsValid) issues.Add("Проверь условия реакции и 1–8 валидных действий.");
            if (asset is CombatReactionAction action && !action.IsValid) issues.Add("Некорректное действие реакции.");
            if (asset is StatModifierDefinition modifiers && !modifiers.IsValid) issues.Add("Нужны валидные модификаторы характеристик.");
            if (asset is ResourceDefinition resource && !resource.IsValid) issues.Add("Проверь ёмкость, начальное заполнение и регенерацию.");
            if (asset is ResourceProfile resources && !resources.IsValid) issues.Add("До 32 уникальных валидных ресурсов.");
            if (asset is HealingDefinition healing && !healing.IsValid) issues.Add("Проверь слагаемые лечения, крит, лимит и избыток.");
            if (asset is HealthDefinition health && !health.IsValid) issues.Add("Проверь максимальное HP, правила попадания и защиту.");
            if (asset is HitRulesDefinition rules && !rules.IsValid) issues.Add("Некорректные вероятности / длительности правил попадания.");
            if (asset is DefenseProfile defense && !defense.IsValid) issues.Add("В защите допустимо до 8 валидных слоёв.");
            if (asset is DefenseLayerDefinition layer && !layer.IsValid) issues.Add("Некорректные параметры слоя защиты.");
            if (asset is CombatStatsDefinition stats && !stats.IsValid) issues.Add("Профиль характеристик требует ключей и конечных неотрицательных значений.");
            if (asset is StatusTickEffect tickEffect && !tickEffect.IsValid) issues.Add("Некорректная формула действия тика.");
            if (asset is AreaDefinition area && !area.IsValid) issues.Add("Некорректная геометрия AOE.");
            if (asset is AreaSpawnDefinition spawn && !spawn.IsValid) issues.Add("Созданию области нужны валидный рецепт, префаб и параметры размещения.");
            if (asset is AreaPulseDefinition pulses && !pulses.IsValid) issues.Add("Нужна валидная атака Area, положительная длительность и интервал ≥ 0.01 с.");
            if (asset is DamageFormulaDefinition formula && !formula.IsValid) issues.Add("Нужны 1–8 корректных слагаемых общей формулы.");
            if (asset is AreaOverlapGroupDefinition group && !group.IsValid) issues.Add("Проверь режим и общий интервал пересечений ≥ 0.01 с.");
            if (asset is StatusDefinition effect)
            {
                if (!effect.IsValid) issues.Add("Проверь длительность, интервал, стаки и формулы действий. Для бюджета на всю длительность нужен хотя бы один тик.");
                if ((effect.TickEffects == null || effect.TickEffects.Length == 0)
                    && (effect.StatModifiers == null || effect.StatModifiers.Length == 0)
                    && (effect.ResistanceModifiers == null || effect.ResistanceModifiers.Length == 0) && effect.Restrictions == CombatRestriction.None)
                    issues.Add("Статус пока ничего не делает: добавь действие или модификатор.");
                if (effect.TickEffects != null)
                    foreach (var tick in effect.TickEffects) if (tick == null) issues.Add("В списке действий тика есть пустая ссылка.");
                if (effect.StatModifiers != null)
                    foreach (var stat in effect.StatModifiers) if (stat.Stat == null) issues.Add("У модификатора не выбран ключ характеристики.");
            }
            return issues;
        }
    }
}
