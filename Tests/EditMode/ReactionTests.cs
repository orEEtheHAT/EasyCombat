using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class ReactionTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private Health a,b;
        private T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); cleanup.Add(asset); return asset; }
        private Health Actor(string name,Vector3 position = default)
        { var actor = new GameObject(name); cleanup.Add(actor); actor.transform.position = position; return actor.AddComponent<Health>(); }
        private ReactionDefinition Rule(Health owner,ReactionTrigger trigger,ReactionTarget target,params CombatReactionAction[] actions)
        {
            var rule = Asset<ReactionDefinition>(); rule.Trigger = trigger; rule.Target = target; rule.Actions = actions;
            var component = owner.gameObject.GetComponent<CombatReactions>() ?? owner.gameObject.AddComponent<CombatReactions>();
            Assert.That(component.Configure(new[] { rule }),Is.True); return rule;
        }
        private ReactionHealingAction Heal(float amount)
        { var definition = Asset<HealingDefinition>(); definition.Terms = new[] { new HealingTerm(HealingBasis.Fixed,amount) }; var action = Asset<ReactionHealingAction>(); action.Healing = definition; return action; }
        private ReactionDamageAction Reflect()
        { var action = Asset<ReactionDamageAction>(); action.FractionOfHealthDamage = 1; return action; }
        [SetUp] public void Setup() { a = Actor("A"); b = Actor("B"); }
        [TearDown] public void Cleanup()
        { for (var i = cleanup.Count-1; i>=0;i--) if (cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        [Test] public void LifestealRunsAfterDamageNotificationsAndUsesActualLoss()
        {
            a.ApplyDamage(new DamageInfo(50));
            var heal = Heal(0); heal.Healing.Terms = new[] { new HealingTerm(HealingBasis.TriggeringHealthDamage,.1f) };
            Rule(a,ReactionTrigger.DamageDealt,ReactionTarget.Owner,heal);
            var deathNotified = false; b.Died += _ => deathNotified = true;
            var sawCompletedDeath = false; a.Healed += _ => sawCompletedDeath = deathNotified;
            b.ApplyDamage(new DamageInfo(1000,a.gameObject));
            Assert.That(a.Current,Is.EqualTo(60)); Assert.That(sawCompletedDeath,Is.True);
        }
        [Test] public void DefaultReflectionCannotReflectAReflection()
        {
            Rule(a,ReactionTrigger.DamageTaken,ReactionTarget.Attacker,Reflect());
            Rule(b,ReactionTrigger.DamageTaken,ReactionTarget.Attacker,Reflect());
            b.ApplyDamage(new DamageInfo(10,a.gameObject));
            Assert.That(a.Current,Is.EqualTo(90)); Assert.That(b.Current,Is.EqualTo(90));
        }
        [Test] public void OptedInReflectionChainsStopAtHardDepthLimit()
        {
            var ar = Rule(a,ReactionTrigger.DamageTaken,ReactionTarget.Attacker,Reflect()); ar.AllowReactionChains = true; ar.MaximumProcsPerResolution = 100;
            var br = Rule(b,ReactionTrigger.DamageTaken,ReactionTarget.Attacker,Reflect()); br.AllowReactionChains = true; br.MaximumProcsPerResolution = 100;
            var before = CombatReactionQueue.DroppedActions;
            b.ApplyDamage(new DamageInfo(10,a.gameObject));
            Assert.That(a.Current,Is.EqualTo(60)); Assert.That(b.Current,Is.EqualTo(50));
            Assert.That(CombatReactionQueue.DroppedActions,Is.GreaterThan(before));
        }
        [Test] public void PerParticipantCooldownIsIndependentOfHealingRecipient()
        {
            a.ApplyDamage(new DamageInfo(50)); var c = Actor("C");
            var rule = Rule(a,ReactionTrigger.DamageDealt,ReactionTarget.Owner,Heal(5)); rule.CooldownScope = ReactionCooldownScope.Target; rule.Cooldown = 10;
            var clock = new ManualCombatClock();
            using (new CombatExecutionScope(clock:clock))
            {
                b.ApplyDamage(new DamageInfo(1,a.gameObject)); b.ApplyDamage(new DamageInfo(1,a.gameObject)); c.ApplyDamage(new DamageInfo(1,a.gameObject));
                Assert.That(a.Current,Is.EqualTo(60)); clock.Advance(10); b.ApplyDamage(new DamageInfo(1,a.gameObject)); Assert.That(a.Current,Is.EqualTo(65));
            }
        }
        [Test] public void TagsAreCapturedAndPeriodicHitsAreOptIn()
        {
            a.ApplyDamage(new DamageInfo(50)); var tag = Asset<CombatTagDefinition>(); var attack = Asset<AttackDefinition>(); attack.Tags = new[] { tag }; attack.Damage = 1;
            var rule = Rule(a,ReactionTrigger.DamageDealt,ReactionTarget.Owner,Heal(5)); rule.RequiredTags = new[] { tag };
            var shot = attack.RollDamage(a.gameObject,default,Vector3.forward,0,1); attack.Tags[0] = null;
            b.ApplyDamage(shot); Assert.That(a.Current,Is.EqualTo(55));
            b.ApplyDamage(new DamageInfo(1,a.gameObject,isPeriodic:true,origin:shot.Origin)); Assert.That(a.Current,Is.EqualTo(55));
            rule.AllowPeriodic = true; b.ApplyDamage(new DamageInfo(1,a.gameObject,isPeriodic:true,origin:shot.Origin)); Assert.That(a.Current,Is.EqualTo(60));
        }
        [Test] public void AreaIsOneResolutionAndHealsOnlyAfterAllTargets()
        {
            a.ApplyDamage(new DamageInfo(50)); a.transform.position = Vector3.left*5;
            b.transform.position = Vector3.zero; b.gameObject.AddComponent<SphereCollider>();
            var c = Actor("C",Vector3.right); c.gameObject.AddComponent<SphereCollider>(); Physics.SyncTransforms();
            var area = Asset<AreaDefinition>(); area.Radius = 2;
            Rule(a,ReactionTrigger.DamageDealt,ReactionTarget.Owner,Heal(5));
            var cBeforeHeal = 0f; a.Healed += _ => cBeforeHeal = c.Current;
            new AreaDamageQuery().Apply(Vector3.zero,Vector3.forward,new DamageInfo(10,a.gameObject),new AreaSettings(area),a.transform);
            Assert.That(a.Current,Is.EqualTo(55)); Assert.That(b.Current,Is.EqualTo(90)); Assert.That(cBeforeHeal,Is.EqualTo(90));
        }
        [Test] public void DeathExplosionSurvivesOwnerDisableButNotPoolReuse()
        {
            a.transform.position = Vector3.left*5; b.transform.position = Vector3.zero;
            var c = Actor("C",Vector3.right); c.gameObject.AddComponent<SphereCollider>(); Physics.SyncTransforms();
            var action = Asset<ReactionDamageAction>(); action.FixedDamage = 10; action.Area = Asset<AreaDefinition>(); action.Area.Radius = 2;
            Rule(b,ReactionTrigger.Death,ReactionTarget.Owner,action); b.Died += _ => b.gameObject.SetActive(false);
            b.ApplyDamage(new DamageInfo(100,a.gameObject)); Assert.That(c.Current,Is.EqualTo(90));
            b.gameObject.SetActive(true); b.ResetToFull();
            b.Died += _ => b.ResetToFull();
            b.ApplyDamage(new DamageInfo(100,a.gameObject)); Assert.That(c.Current,Is.EqualTo(90));
        }
        [Test] public void ParentRootAndReactionClassificationArePreserved()
        {
            Rule(b,ReactionTrigger.DamageTaken,ReactionTarget.Attacker,Reflect());
            var shot = new DamageInfo(10,a.gameObject); DamageOrigin reflected = default;
            a.Damaged += result => reflected = result.Damage.Origin;
            b.ApplyDamage(shot);
            Assert.That(reflected.Action.RootId,Is.EqualTo(shot.Origin.Action.RootId));
            Assert.That(reflected.Action.ParentId,Is.EqualTo(shot.Origin.Action.Id)); Assert.That(reflected.Action.Kind,Is.EqualTo(CombatOriginKind.Reaction));
            Assert.That(reflected.WithStatus(Asset<StatusDefinition>()).Action.ReactionDepth,Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)] public void StatusReactionCanKeepOriginalAttackerAndHitScaledPower(bool sourceHasHealth)
        {
            var source=sourceHasHealth ? a.gameObject : new GameObject("Hazard"); if(!sourceHasHealth) cleanup.Add(source);
            var hp=Asset<HealthDefinition>(); hp.HitRules=Asset<HitRulesDefinition>(); hp.HitRules.AllowSelf=false; b.ApplyDefinition(hp);
            var controller=b.gameObject.AddComponent<StatusController>(); var status=Asset<StatusDefinition>();
            var tick=Asset<PeriodicDamageEffect>(); tick.Terms=new[] { new StatusDamageTerm(StatusDamageBasis.TriggeringHit,.5f) };
            status.TickEffects=new StatusTickEffect[] { tick }; var action=Asset<ReactionStatusAction>(); action.Status=status;
            var rule=Rule(b,ReactionTrigger.DamageTaken,ReactionTarget.Owner,action); rule.UseTriggerSource=true;
            var hit=new DamageInfo(10,source); b.ApplyDamage(hit); controller.Advance(1);
            Assert.That(b.Current,Is.EqualTo(85));
            var stack=controller.GetStack(status,0); Assert.That(stack.Origin.Actor.Id,Is.EqualTo(hit.Origin.Actor.Id)); Assert.That(stack.Origin.Action.ReactionDepth,Is.EqualTo(1));
        }
    }
}
