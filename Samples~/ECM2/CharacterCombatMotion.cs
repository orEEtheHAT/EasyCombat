using ECM2;
using UnityEngine;

namespace EasyCombat.Integrations
{
    [RequireComponent(typeof(Character)), AddComponentMenu("EasyCombat/Integrations/ECM2 Combat Motion")]
    public sealed class CharacterCombatMotion : MonoBehaviour,ICombatMotionReceiver
    {
        private Character character;
        public bool ApplyMotion(in CombatMotionIntent intent)
        {
            if (!isActiveAndEnabled || !intent.IsValid) return false;
            if (character==null) character=GetComponent<Character>();
            character.AddForce(intent.VelocityChange,ForceMode.VelocityChange); return true;
        }
    }
}
