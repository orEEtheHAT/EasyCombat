using UnityEngine;
using UnityEngine.AI;

namespace EasyCombat
{
    [RequireComponent(typeof(NavMeshAgent),typeof(MovementInfluences)),DisallowMultipleComponent]
    [AddComponentMenu("EasyCombat/Environment/NavMesh Speed Adapter")]
    public sealed class NavMeshSpeedAdapter : MonoBehaviour
    {
        private NavMeshAgent agent;
        private MovementInfluences effects;
        private float basis,last;
        private bool pendingBase;
        // AI can change gait while slowed. Use this property even when the new base equals the effective speed.
        public float BaseSpeed { get=>basis; set { if(CombatMath.Finite(value) && value>=0) { basis=value; pendingBase=!isActiveAndEnabled || !agent; if(!pendingBase) Apply(); } } }
        private void OnEnable() { agent=GetComponent<NavMeshAgent>(); effects=GetComponent<MovementInfluences>(); if(!pendingBase) basis=agent.speed; pendingBase=false; Apply(); }
        public void Refresh() { if(isActiveAndEnabled && agent) { if(agent.speed!=last) basis=agent.speed; Apply(); } }
        private void Apply() { last=basis*effects.Multiplier; if(agent.speed!=last) agent.speed=last; }
        private void LateUpdate()=>Refresh();
        private void OnDisable() { if(agent && agent.speed==last) agent.speed=basis; }
    }
}
