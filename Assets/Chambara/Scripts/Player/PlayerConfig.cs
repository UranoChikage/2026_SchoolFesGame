using UnityEngine;

namespace Chambara
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Chambara/PlayerStats")]
    public class PlayerConfig : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("1•bŠÔ‚É‚Ç‚ê‚¾‚¯i‚Ş‚©")]
        public float moveSpeed = 0.3f;

        [Header("Attack")]
        public int attackStartup = 20;
        public int attackActive = 1;
        public int attackRecovery = 20;

        [Header("Guard")]
        public int guardStartup = 5;
        public int guardActive = 2;
        public int guardRecovery = 5;

        [Header("Stun")]
        public int parriedStunframe = 50;
        public int clahedStunframe = 30;
    }
}
