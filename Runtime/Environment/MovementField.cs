using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [RequireComponent(typeof(CombatAreaTrigger)),DisallowMultipleComponent,AddComponentMenu("EasyCombat/Environment/Continuous Slow Field")]
    public sealed class MovementField : MonoBehaviour,ICombatTransient
    {
        public AreaTriggerDefinition SensorProfile;
        public MovementInfluenceDefinition Effect;
        public Transform Owner;
        public bool StartOnEnable=true;
        private CombatAreaTrigger sensor;
        private readonly Dictionary<long,MovementInfluences> targets=new Dictionary<long,MovementInfluences>(32);
        private bool started;
        private void Awake()=>sensor=GetComponent<CombatAreaTrigger>();
        private void Start() { started=true; if(StartOnEnable) Fire(); }
        private void OnEnable()
        {
            sensor=GetComponent<CombatAreaTrigger>(); sensor.Contact+=Contact; sensor.Stopped+=ReleaseAll;
            if(started && StartOnEnable) Fire();
        }
        public bool Fire()
        {
            if(!isActiveAndEnabled || !SensorProfile || SensorProfile.EnterAttack || SensorProfile.StayAttack || SensorProfile.ExitAttack || !Effect || !Effect.IsValid) return false;
            return sensor.Begin(SensorProfile,Owner ? Owner.gameObject : gameObject);
        }
        private void Contact(AreaContactEvent e)
        {
            if(e.Kind==AreaContactKind.Exit) { if(targets.TryGetValue(e.Actor.Id,out var old)) { if(old) old.Release(this); targets.Remove(e.Actor.Id); } return; }
            if(!e.Receiver) return;
            if(!targets.TryGetValue(e.Actor.Id,out var receiver) || !receiver)
            {
                receiver=e.Receiver.GetComponentInParent<MovementInfluences>();
                if(!receiver || !receiver.isActiveAndEnabled) return;
                targets[e.Actor.Id]=receiver;
            }
            receiver.Set(this,Effect);
        }
        public void Cease() { if(sensor) sensor.Stop(); ReleaseAll(); }
        private void ReleaseAll() { foreach(var target in targets.Values) if(target) target.Release(this); targets.Clear(); }
        public void CancelForRestore() { if(sensor) sensor.CancelForRestore(); ReleaseAll(); }
        private void OnDisable() { if(sensor) { sensor.Contact-=Contact; sensor.Stopped-=ReleaseAll; } Cease(); }
    }
}
