using System;

namespace EasyCombat
{
    public enum CombatOriginKind { Attack, Status, Area, Reaction, Environment, Redirection, BodyPart, Ability }
    /// <summary>Small immutable tag snapshot; no allocation on emission or during filtering.</summary>
    public readonly struct CombatTagSet
    {
        public const int Capacity = 8;
        private readonly CombatTagDefinition t0,t1,t2,t3,t4,t5,t6,t7;
        public CombatTagSet(CombatTagDefinition[] tags)
        {
            if (tags != null && tags.Length > Capacity) throw new ArgumentException("At most eight tags per action.",nameof(tags));
            t0 = tags?.Length > 0 ? tags[0] : null; t1 = tags?.Length > 1 ? tags[1] : null;
            t2 = tags?.Length > 2 ? tags[2] : null; t3 = tags?.Length > 3 ? tags[3] : null;
            t4 = tags?.Length > 4 ? tags[4] : null; t5 = tags?.Length > 5 ? tags[5] : null;
            t6 = tags?.Length > 6 ? tags[6] : null; t7 = tags?.Length > 7 ? tags[7] : null;
        }
        public bool Contains(CombatTagDefinition tag) => tag != null && (tag == t0 || tag == t1 || tag == t2 || tag == t3 || tag == t4 || tag == t5 || tag == t6 || tag == t7);
        public bool ContainsAll(CombatTagDefinition[] tags)
        { if (tags != null) foreach (var tag in tags) if (!Contains(tag)) return false; return true; }
        public bool ContainsAny(CombatTagDefinition[] tags)
        { if (tags != null) foreach (var tag in tags) if (Contains(tag)) return true; return false; }
    }
    public readonly struct CombatActionOrigin
    {
        public readonly long Id, RootId, ParentId;
        public readonly int Depth;
        public readonly int ReactionDepth;
        public readonly CombatOriginKind Kind;
        public readonly CombatTagSet Tags;
        public CombatActionOrigin(long id, long root, long parent, int depth, CombatOriginKind kind, CombatTagSet tags = default, int reactionDepth = 0)
        { Id = id; RootId = root; ParentId = parent; Depth = depth; Kind = kind; Tags = tags; ReactionDepth = reactionDepth; }
        public static CombatActionOrigin Root(CombatOriginKind kind = CombatOriginKind.Attack, CombatTagSet tags = default)
        { var id = CombatEvents.NextId(); return new CombatActionOrigin(id,id,0,0,kind,tags); }
        public CombatActionOrigin Child(CombatOriginKind kind) => Id == 0 ? Root(kind,Tags)
            : new CombatActionOrigin(CombatEvents.NextId(),RootId,Id,Depth+1,kind,Tags,ReactionDepth+(kind == CombatOriginKind.Reaction ? 1 : 0));
    }
}
