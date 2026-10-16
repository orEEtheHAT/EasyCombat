using System;
using UnityEngine;

namespace EasyCombat
{
    /// <summary>Observer isolation without invocation-list allocations on the hot path.</summary>
    internal sealed class CombatSignal<T>
    {
        private Action<T> handlers;
        private Action<T>[] listeners = Array.Empty<Action<T>>();
        public void Add(Action<T> handler) { handlers += handler; Rebuild(); }
        public void Remove(Action<T> handler) { handlers -= handler; Rebuild(); }
        private void Rebuild()
        {
            if (handlers == null) { listeners = Array.Empty<Action<T>>(); return; }
            var delegates = handlers.GetInvocationList(); listeners = new Action<T>[delegates.Length];
            for (var i = 0; i < delegates.Length; i++) listeners[i] = (Action<T>)delegates[i];
        }
        public void Publish(T value)
        {
            var snapshot = listeners;
            for (var i = 0; i < snapshot.Length; i++)
                try { snapshot[i](value); } catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
