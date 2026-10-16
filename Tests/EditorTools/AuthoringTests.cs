using System;
using System.Collections.Generic;
using EasyCombat.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class DiagnosticGraphNode:ScriptableObject { public DiagnosticGraphNode Next; }
    public sealed class AuthoringTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private string folder;
        [TearDown] public void Cleanup()
        {
            for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear();
            if(!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets/EasyCombatAuthoringTest_",StringComparison.Ordinal) && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }
        [Test] public void DependencyAnalysisReportsCyclesWithoutRecursingForever()
        {
            var a=ScriptableObject.CreateInstance<DiagnosticGraphNode>(); var b=ScriptableObject.CreateInstance<DiagnosticGraphNode>(); cleanup.Add(a); cleanup.Add(b);
            a.name="A"; b.name="B"; a.Next=b; b.Next=a;
            var report=CombatDependencyAnalyzer.Analyze(new[] { a }); Assert.That(report.Assets.Count,Is.EqualTo(2)); Assert.That(report.Cycles,Is.EqualTo(1)); Assert.That(report.Issues[0].Message,Does.Contain("A"));
        }
        [Test] public void EveryTemplateIsValidAndCommonDamageTypesAreReused()
        {
            folder="Assets/EasyCombatAuthoringTest_"+Guid.NewGuid().ToString("N"); AttackDefinition mixed=null; HealthDefinition shielded=null;
            foreach(CombatTemplateKind kind in Enum.GetValues(typeof(CombatTemplateKind)))
            {
                var root=CombatTemplates.Create(kind,folder); var report=CombatDependencyAnalyzer.Analyze(new[] { root });
                Assert.That(report.Cycles,Is.Zero,kind.ToString()); Assert.That(report.Issues.FindAll(issue=>issue.Error),Is.Empty,kind.ToString());
                if(kind==CombatTemplateKind.MixedFirePoisonWeapon) mixed=(AttackDefinition)root;
                if(kind==CombatTemplateKind.ShieldedEnemy) shielded=(HealthDefinition)root;
            }
            Assert.That(shielded.Resistances.GetReduction(mixed.DamageChannels[0].Type),Is.EqualTo(.5f));
            var second=CombatTemplates.Create(CombatTemplateKind.MixedFirePoisonWeapon,folder);
            Assert.That(AssetDatabase.GetAssetPath(second),Is.Not.EqualTo(AssetDatabase.GetAssetPath(mixed)));
        }
    }
}
