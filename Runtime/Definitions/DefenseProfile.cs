using System;
using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum DefenseStage { BeforeResistance, AfterResistance }
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct DefenseSlot
    {
        public DefenseLayerDefinition Layer;
        public DefenseStage Stage;
        public bool DisabledAtStart;
        public DefenseSlot(DefenseLayerDefinition layer, DefenseStage stage = DefenseStage.AfterResistance)
        { Layer = layer; Stage = stage; DisabledAtStart = false; }
    }
    [CreateAssetMenu(menuName = "EasyCombat/Defense Profile", fileName = "DefenseProfile")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class DefenseProfile : ScriptableObject
    {
        public const int MaximumLayers = 8;
        public DefenseSlot[] Layers = Array.Empty<DefenseSlot>();
        public bool IsValid
        {
            get
            {
                if (Layers == null || Layers.Length > MaximumLayers) return false;
                foreach (var slot in Layers) if (slot.Layer == null || !slot.Layer.IsValid
                    || (slot.Stage != DefenseStage.BeforeResistance && slot.Stage != DefenseStage.AfterResistance)) return false;
                return true;
            }
        }
    }
}
