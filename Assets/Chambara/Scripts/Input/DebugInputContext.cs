using UnityEngine;
using UnityEngine.InputSystem;

namespace Chambara
{
    public class DebugInputContext : MonoBehaviour, IChambaraInputContext
    {
        [SerializeField] private GameObject player; 


        [SerializeField] ChambaraInputType inputType;
        public ChambaraInputType InputType => inputType;

        [SerializeField] int attackIndex;   
        public int AttackIndex => attackIndex;

        InputSystem_Actions inputActions;
        private InputAction[] attackActions;

        private void Awake()
        {
            inputActions = new InputSystem_Actions();

            var chambara = inputActions.Chambara;
            attackActions = new InputAction[] {
                chambara._1,
                chambara._2,
                chambara._3,
                chambara._4,
                chambara._5,
                chambara._6,
                chambara._7,
                chambara._8,
            };
        }

        private void OnEnable()
        {
            inputActions.Chambara.Enable();
        }

        private void OnDisable()
        {
            inputActions.Chambara.Disable();
        }

        private void OnDestroy()
        {
            inputActions.Dispose();
        }

        private void Start()
        {
            if (player != null)
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
            if (inputActions.Chambara.Guard.IsPressed())
            {
                inputType = ChambaraInputType.Guard;
                return;
            }
            for (int i = 0; i < attackActions.Length; i++)
            {
                if (attackActions[i].WasPressedThisFrame())
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
