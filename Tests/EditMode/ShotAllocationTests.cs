using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class ShotAllocationTests
    {
        [Test] public void WarmShotNamesPreserveIdentityAndDoNotAllocate()
        {
            var actor=new GameObject("Source without Health"); var attack=ScriptableObject.CreateInstance<AttackDefinition>();
            try
            {
                attack.name="Fallback attack label"; attack.Damage=1;
                DamageOrigin Roll()=>attack.RollDamage(actor,Vector3.zero,Vector3.forward,0,1).Origin;
                var original=Roll(); Assert.AreEqual(actor.name,original.Actor.Name); Assert.AreEqual(attack.name,original.AttackName);
                for(int i=0;i<32;i++) Roll();
                Assert.IsTrue(CombatAllocationMeter.TryMeasure(()=>{ for(int i=0;i<1000;i++) Roll(); },out var events));
                Assert.AreEqual(0,events);
                actor.name="Renamed source"; attack.name="Renamed attack"; CombatNames.Invalidate(actor); CombatNames.Invalidate(attack);
                var renamed=Roll(); Assert.AreEqual(original.Actor.Id,renamed.Actor.Id); Assert.AreEqual("Renamed source",renamed.Actor.Name);
                Assert.AreEqual("Renamed attack",renamed.AttackName); Assert.AreEqual("Fallback attack label",original.AttackName);
                attack.DisplayName="Explicit"; Assert.AreEqual("Explicit",Roll().AttackName);
                var hp=actor.AddComponent<Health>(); Assert.AreEqual(hp.Identity.Id,Roll().Actor.Id,"Adding Health must supersede fallback identity.");
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(attack); }
        }
        [Test] public void AllocationMeterDistinguishesKnownAllocationsFromEmptyWork()
        {
            var keep=new byte[3][];
            Assert.IsTrue(CombatAllocationMeter.TryMeasure(()=>{},out var none)); Assert.AreEqual(0,none);
            Assert.IsTrue(CombatAllocationMeter.TryMeasure(()=>{for(int i=0;i<keep.Length;i++) keep[i]=new byte[128+i];},out var count));
            Assert.AreEqual(3,count); System.GC.KeepAlive(keep);
        }
    }
}
