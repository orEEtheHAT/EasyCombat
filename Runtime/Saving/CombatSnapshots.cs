using System;

namespace EasyCombat
{
    [Flags] public enum SavedCombatModules { None=0,Resources=1,Stats=2,Statuses=4,Abilities=8,Buildup=16,Control=32,Reactions=64,Attribution=128,BodyPart=256,DamageModifiers=512,Redirector=1024,Team=2048,All=4095 }
    [Serializable] public sealed class CombatWorldSnapshot
    {
        public int Version=1;
        public string CatalogCompatibility;
        public CombatActorSnapshot[] Actors=Array.Empty<CombatActorSnapshot>();
    }
    [Serializable] public sealed class CombatActorSnapshot
    {
        public string Id;
        public SavedCombatModules Modules;
        public SavedHealth Health;
        public SavedResources Resources;
        public SavedStats Stats;
        public SavedStatuses Statuses;
        public SavedAbilities Abilities;
        public SavedBuildup Buildup;
        public SavedControl Control;
        public SavedReactions Reactions;
        public SavedAttribution Attribution;
        public SavedBodyPart BodyPart;
        public SavedDamageModifiers DamageModifiers;
        public SavedRedirector Redirector;
        public SavedTeam Team;
        internal void CaptureModules()
        {
            Modules=(Resources!=null ? SavedCombatModules.Resources : 0) | (Stats!=null ? SavedCombatModules.Stats : 0)
                | (Statuses!=null ? SavedCombatModules.Statuses : 0) | (Abilities!=null ? SavedCombatModules.Abilities : 0)
                | (Buildup!=null ? SavedCombatModules.Buildup : 0) | (Control!=null ? SavedCombatModules.Control : 0)
                | (Reactions!=null ? SavedCombatModules.Reactions : 0) | (Attribution!=null ? SavedCombatModules.Attribution : 0)
                | (BodyPart!=null ? SavedCombatModules.BodyPart : 0) | (DamageModifiers!=null ? SavedCombatModules.DamageModifiers : 0)
                | (Redirector!=null ? SavedCombatModules.Redirector : 0) | (Team!=null ? SavedCombatModules.Team : 0);
        }
    }
    [Serializable] public sealed class SavedHealth
    {
        public string Definition,ResistanceProfile,HitRules,DefenseProfile;
        public HealthSnapshot Health;
        public float ManualInvulnerability,HitInvulnerability;
        public bool Invulnerable;
        public SavedDefense[] Defenses=Array.Empty<SavedDefense>();
    }
    [Serializable] public struct SavedDefense { public string Definition; public DefenseStage Stage; public DefenseLayerState State; }
    [Serializable] public sealed class SavedResources { public string Profile; public SavedResource[] Values=Array.Empty<SavedResource>(); }
    [Serializable] public struct SavedResource
    { public string Definition,MaximumStat; public float Current,Maximum,InitialFraction,Regeneration,SpendDelay,RemainingDelay; }
    [Serializable] public sealed class SavedStats
    { public string Profile; public SavedStatValue[] Values=Array.Empty<SavedStatValue>(); public SavedModifier[] Modifiers=Array.Empty<SavedModifier>(); }
    [Serializable] public struct SavedStatValue { public string Definition; public float Value; }
    [Serializable] public struct SavedModifier { public long Group; public string Stat,Source; public StatModifierOperation Operation; public float Value; }
    [Serializable] public sealed class SavedStatuses { public SavedStatus[] Values=Array.Empty<SavedStatus>(); }
    [Serializable] public sealed class SavedStatus
    { public string Definition; public int Stacks; public bool Independent; public SavedStatusStack[] Records=Array.Empty<SavedStatusStack>(); }
    [Serializable] public sealed class SavedStatusStack
    {
        public long Id;
        public string Source;
        public SavedOrigin Origin;
        public double ExpiresIn,NextTickIn,Interval;
        public SavedStatusData Data;
    }
    [Serializable] public sealed class SavedStatusData
    {
        public float Duration,Interval,Power;
        public double PlannedTicks;
        public bool HasBudget;
        public SavedEffect[] Effects=Array.Empty<SavedEffect>();
    }
    [Serializable] public sealed class SavedEffect
    { public string Definition,OutgoingType; public float Value,OutgoingMultiplier; public bool HasOutgoingType; public SavedFormula Formula; }
    [Serializable] public sealed class SavedFormula
    { public bool Present; public string Source; public float Scale; public bool Critical; public SavedFormulaTerm[] Terms=Array.Empty<SavedFormulaTerm>(); }
    [Serializable] public struct SavedFormulaTerm
    { public string Type,Stat; public StatusDamageBasis Basis; public float Value,Multiplier,Limit; }
    [Serializable] public sealed class SavedActorIdentity
    { public string Reference,Name; public long HistoricalKey; }
    [Serializable] public sealed class SavedOrigin
    {
        public SavedActorIdentity Actor;
        public string Attack,Status;
        public long Cast,Action,Root,Parent;
        public int Depth,ReactionDepth,Team;
        public bool HasTeam;
        public CombatOriginKind Kind;
        public string[] Tags=Array.Empty<string>();
    }
    [Serializable] public sealed class SavedAbilities
    {
        public SavedAbility[] Values=Array.Empty<SavedAbility>();
        public float GlobalCooldown;
        public string Casting,Target;
        public float CastRemaining;
        public UnityEngine.Vector3 CastPoint;
        public SavedOrigin CastOrigin;
    }
    [Serializable] public struct SavedAbility { public string Definition; public int Charges; public float Cooldown,Recharge; }
    [Serializable] public sealed class SavedBuildup { public SavedMeter[] Values=Array.Empty<SavedMeter>(); }
    [Serializable] public struct SavedMeter { public string Definition; public float Value,Delay,Cooldown; }
    [Serializable] public sealed class SavedControl { public SavedRestriction[] Values=Array.Empty<SavedRestriction>(); }
    [Serializable] public struct SavedRestriction { public long Key; public CombatRestriction Restriction; }
    [Serializable] public sealed class SavedReactions { public SavedReaction[] Values=Array.Empty<SavedReaction>(); }
    [Serializable] public sealed class SavedReaction
    { public string Definition; public double ActorCooldown; public SavedReactionCooldown[] Cooldowns=Array.Empty<SavedReactionCooldown>(); }
    [Serializable] public struct SavedReactionCooldown { public SavedActorIdentity Actor; public long Action; public double Remaining; }
    [Serializable] public sealed class SavedAttribution
    { public string Definition; public bool Awarded; public SavedContribution[] Contributions=Array.Empty<SavedContribution>(); }
    [Serializable] public struct SavedContribution { public SavedActorIdentity Actor; public float Damage; public double Age; }
    [Serializable] public sealed class SavedBodyPart { public string Definition,Core; }
    [Serializable] public sealed class SavedDamageModifiers { public string Outgoing,Incoming; }
    [Serializable] public sealed class SavedRedirector { public string Definition,Guardian; }
    [Serializable] public sealed class SavedTeam { public int Value; }
    public readonly struct RestoredModifierHandle
    { public readonly string Actor; public readonly long SavedGroup; public readonly StatModifierHandle Handle; public RestoredModifierHandle(string actor,long group,StatModifierHandle handle) { Actor=actor; SavedGroup=group; Handle=handle; } }
    public readonly struct RestoredControlHandle
    { public readonly string Actor; public readonly long SavedKey; public readonly ControlHandle Handle; public RestoredControlHandle(string actor,long key,ControlHandle handle) { Actor=actor; SavedKey=key; Handle=handle; } }
    public sealed class CombatRestoreResult
    {
        public readonly System.Collections.Generic.List<RestoredModifierHandle> Modifiers=new System.Collections.Generic.List<RestoredModifierHandle>();
        public readonly System.Collections.Generic.List<RestoredControlHandle> Controls=new System.Collections.Generic.List<RestoredControlHandle>();
    }
}
