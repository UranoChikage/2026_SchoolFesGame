using System.Collections;
using UnityEngine;

namespace Chambara
{
    public class Player : MonoBehaviour, ISetInputContext
    {
        [SerializeField] PlayerStats playerStats;


        [SerializeField] Transform startPosition;
        [SerializeField] Transform endPosition;

        IChambaraInputContext inputContext;
        bool isAttacking = false;

        void Start()
        {
            transform.position = startPosition.position;
        }

        private void Update()
        {
            InputCheck();
        }

        public void Init(Transform startPos,Transform endPos)
        {
            startPosition = startPos;
            endPosition = endPos;
        }

        public void SetInputContext(IChambaraInputContext inputContext)
        {
            this.inputContext = inputContext;
        }

        private void InputCheck()
        {
            if (inputContext == null) return;

            if (!isAttacking && inputContext.InputType == ChambaraInputType.Attack)
            {
                Debug.Log("PlayerAttck" + inputContext.AttackIndex.ToString());
                isAttacking = true;
                StartCoroutine(AttackDelay());
            }
        }
        private IEnumerator AttackDelay()
        {
            yield return new WaitForSeconds(playerStats.AttackCooldown);
            isAttacking = false;
        }

    }
}
