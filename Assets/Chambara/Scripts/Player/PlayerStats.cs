using UnityEngine;

namespace Chambara
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Chambara/PlayerStats")]
    public class PlayerStats : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 0.3f;

        [Header("Attack")]
        [SerializeField] float attackCooldown = 0.5f;
        public float AttackCooldown => attackCooldown;
    }
}
