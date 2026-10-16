using System;
using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct CombatActorIdentity
    {
        public readonly long Id;
        public readonly string Name;
        public CombatActorIdentity(long id, string name) { Id = id; Name = name; }
        public static CombatActorIdentity Capture(GameObject actor)
        {
            if (actor == null) return default;
            var health = actor.GetComponentInParent<Health>();
            return health != null ? health.Identity : new CombatActorIdentity(-Math.Abs((long)actor.GetInstanceID()) - 1, actor.name);
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct DamageOrigin
    {
        public readonly CombatActorIdentity Actor;
        public readonly AttackDefinition Attack;
        public readonly StatusDefinition Status;
        public readonly string AttackName, StatusName;
        public readonly long CastId;
        public readonly CombatActionOrigin Action;
        public readonly bool HasTeam;
        public readonly int Team;
        public DamageOrigin(CombatActorIdentity actor, AttackDefinition attack = null, long castId = 0,
            StatusDefinition status = null, bool hasTeam = false, int team = 0, CombatActionOrigin action = default)
        {
            Actor = actor; Attack = attack; Status = status; CastId = castId;
            AttackName = attack != null ? attack.Label : null; StatusName = status != null ? status.Label : null;
            HasTeam = hasTeam; Team = team;
            Action = action.Id != 0 ? action : new CombatActionOrigin(castId,castId,0,0,
                status != null ? CombatOriginKind.Status : attack != null ? CombatOriginKind.Attack : CombatOriginKind.Environment,
                new CombatTagSet(attack != null ? attack.Tags : null));
        }
        private DamageOrigin(in DamageOrigin origin, StatusDefinition status)
        {
            Actor = origin.Actor; Attack = origin.Attack; AttackName = origin.AttackName;
            CastId = origin.CastId; Status = status; StatusName = status != null ? status.Label : null;
            HasTeam = origin.HasTeam; Team = origin.Team;
            Action = origin.Action.Child(CombatOriginKind.Status);
        }
        public DamageOrigin WithStatus(StatusDefinition status) => new DamageOrigin(in this, status);
        private DamageOrigin(in DamageOrigin origin,CombatActionOrigin action)
        {
            Actor=origin.Actor; Attack=origin.Attack; Status=origin.Status; AttackName=origin.AttackName; StatusName=origin.StatusName;
            CastId=origin.CastId; HasTeam=origin.HasTeam; Team=origin.Team; Action=action;
        }
        public DamageOrigin Child(CombatOriginKind kind)=>new DamageOrigin(in this,Action.Child(kind));
        public static DamageOrigin Capture(GameObject source, AttackDefinition attack = null)
        {
            var team = source != null ? source.GetComponentInParent<CombatTeam>() : null;
            return new DamageOrigin(CombatActorIdentity.Capture(source), attack, CombatEvents.NextId(), null, team != null, team != null ? team.Team : 0);
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct CombatRecord
    {
        public readonly long Sequence;
        public readonly double Time;
        public readonly CombatActorIdentity Target;
        public readonly DamageResult Result;
        public readonly Health TargetHealth;
        public CombatRecord(long sequence, double time, CombatActorIdentity target, DamageResult result, Health targetHealth = null)
        { Sequence = sequence; Time = time; Target = target; Result = result; TargetHealth = targetHealth; }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public static class CombatEvents
    {
        private static long sequence;
        private static Action<CombatRecord> handlers;
        private static Action<CombatRecord>[] listeners = Array.Empty<Action<CombatRecord>>();
        public static int SubscriberCount => listeners.Length;
        public static event Action<CombatRecord> DamageResolved
        {
            add { handlers += value; CacheListeners(); }
            remove { handlers -= value; CacheListeners(); }
        }
        private static void CacheListeners()
        {
            if (handlers == null) { listeners = Array.Empty<Action<CombatRecord>>(); return; }
            var delegates = handlers.GetInvocationList();
            listeners = new Action<CombatRecord>[delegates.Length];
            for (var i = 0; i < delegates.Length; i++) listeners[i] = (Action<CombatRecord>)delegates[i];
        }
        // IDs keep increasing with disabled domain reload so cached identities cannot collide.
        internal static long NextId() => ++sequence;
        internal static void Publish(CombatActorIdentity target, DamageResult result, Health targetHealth = null)
        {
            if(CombatSaveSystem.IsRestoring) return;
            var snapshot = CombatExecutionScope.Current?.IsolateGlobalEvents==true ? Array.Empty<Action<CombatRecord>>() : listeners;
            var source=result.Damage.Source!=null ? result.Damage.Source.GetComponentInParent<Health>() : null;
            if(source!=null && source.Identity.Id!=result.Damage.Origin.Actor.Id) source=null;
            if (snapshot.Length == 0 && (targetHealth==null || targetHealth.ResolvedSubscriberCount==0)
                && (source==null || source.ResolvedSubscriberCount==0)) return;
            var record = new CombatRecord(NextId(), CombatClock.Now, target, result,targetHealth);
            for (var i = 0; i < snapshot.Length; i++)
            {
                // EditMode-created components may never receive OnDestroy. Prune their delegates on dispatch.
                if(snapshot[i].Target is UnityEngine.Object subscriber && subscriber==null) { DamageResolved-=snapshot[i]; continue; }
                try { snapshot[i](record); }
                catch (Exception exception) { Debug.LogException(exception); } // Observability must not cancel death/gameplay events.
            }
            if(targetHealth!=null) targetHealth.PublishResolved(in record);
            if(source!=null && source!=targetHealth) source.PublishResolved(in record);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscribers() { handlers = null; listeners = Array.Empty<Action<CombatRecord>>(); }
    }

    /// <summary>Optional bounded history. Rendering and text formatting belong to consumers.</summary>
    [DisallowMultipleComponent, AddComponentMenu("EasyCombat/Combat Journal")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class CombatJournal : MonoBehaviour
    {
        [SerializeField, Min(1)] private int capacity = 256;
        [SerializeField] private Health targetFilter;
        private CombatRecord[] records;
        private int next, count;
        public int Count => count;
        public int Capacity => records != null ? records.Length : Mathf.Max(1, capacity);
        public long Revision { get; private set; }
        public void Configure(int size, Health onlyTarget = null)
        {
            capacity = Mathf.Clamp(size, 1, 16384); targetFilter = onlyTarget;
            records = new CombatRecord[capacity]; next = count = 0; Revision++;
        }
        private void OnEnable()
            => StartRecording();
        public void StartRecording()
        {
            if (records == null) Configure(capacity, targetFilter);
            CombatEvents.DamageResolved -= Record;
            CombatEvents.DamageResolved += Record;
        }
        public void StopRecording() => CombatEvents.DamageResolved -= Record;
        private void OnDisable() => StopRecording();
        private void OnDestroy() => StopRecording();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        {
            // Also rebind when both domain and scene reload are disabled in Enter Play Mode options.
            foreach (var journal in FindObjectsByType<CombatJournal>(FindObjectsSortMode.None))
                if (journal.isActiveAndEnabled) { journal.Clear(); journal.StartRecording(); }
        }
        private void Record(CombatRecord record)
        {
            if (targetFilter != null && targetFilter.Identity.Id != record.Target.Id) return;
            records[next] = record; next = (next + 1) % records.Length;
            count = Mathf.Min(count + 1, records.Length); Revision++;
        }
        public CombatRecord GetNewest(int index)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            return records[(next - 1 - index + records.Length) % records.Length];
        }
        public void Clear() { if (records != null) Array.Clear(records, 0, records.Length); next = count = 0; Revision++; }
    }
}
