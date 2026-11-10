#if EASYCOMBAT_CPU_CAPTURE
using Unity.Profiling;

namespace EasyCombat
{
    internal static class CombatCpuMarkers
    {
        internal static readonly ProfilerMarker TraceContacts = new ProfilerMarker("EasyCombat.TraceContacts");
        internal static readonly ProfilerMarker AreaSelect = new ProfilerMarker("EasyCombat.AreaSelect");
        internal static readonly ProfilerMarker ResolveDamage = new ProfilerMarker("EasyCombat.ResolveDamage");
        internal static readonly ProfilerMarker ProjectileBatch = new ProfilerMarker("EasyCombat.ProjectileBatch");
        internal static readonly ProfilerMarker TickStatuses = new ProfilerMarker("EasyCombat.TickStatuses");
        internal static readonly ProfilerMarker ApplyStatus = new ProfilerMarker("EasyCombat.ApplyStatus");
        internal static readonly ProfilerMarker OverlapFields = new ProfilerMarker("EasyCombat.OverlapFields");
        internal static readonly ProfilerMarker FieldTick = new ProfilerMarker("EasyCombat.FieldTick");
    }
}
#endif
