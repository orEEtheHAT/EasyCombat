using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Statuses/Combination",fileName="StatusCombination")]
    public sealed class StatusCombinationDefinition : ScriptableObject
    {
        public StatusDefinition Consume;
        [Range(1,32)] public int Stacks=1;
        public StatusDefinition Apply;
        public bool IsValid => Consume!=null && Apply!=null && Consume!=Apply && Stacks>=1 && Stacks<=32 && Apply.IsValid;
    }
}
