using UnityEngine;

namespace Chambara
{
    public class DebugInputContext : MonoBehaviour, IChambaraInputContext
    {
        [SerializeField] private GameObject player; 


        [SerializeField] ChambaraInputType inputType;
        public ChambaraInputType InputType => inputType;

        [SerializeField] int attackIndex;   
        public int AttackIndex => attackIndex;

        private KeyCode[] attackKeys = {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5,
            KeyCode.Alpha6,
            KeyCode.Alpha7,
            KeyCode.Alpha8,
        };

        private void Start()
        {
            if(player != null)
            {
                var playerComponent = player.GetComponent<ISetInputContext>();
                if (playerComponent != null)
                {
                    playerComponent.SetInputContext(this);
                }
                else
                {
                    Debug.LogError("ISetInputContextをアタッチしてない");
                }
            }
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.Space))
            {
                inputType = ChambaraInputType.Guard;
                return;
            }
            for (int i = 0; i < attackKeys.Length; i++)
            {
                if (Input.GetKeyDown(attackKeys[i]))
                {
                    inputType = ChambaraInputType.Attack;
                    attackIndex = i;
                    return;
                }
            }
            inputType = ChambaraInputType.Idle;
        }
    }
}
