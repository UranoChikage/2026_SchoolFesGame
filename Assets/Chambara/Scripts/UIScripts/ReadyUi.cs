using UnityEngine;

namespace Chambara
{
    public class ReadyUi : MonoBehaviour
    {
        [SerializeField] Animator readyAnimation;
        [SerializeField] CanvasGroup canvasGroup;

        private void Start()
        {
            ChambaraManager.Instance.OnGameStateChanged += OnGameStateChanged;
            canvasGroup.alpha = 0f;
        }

        private void OnGameStateChanged(GameState state)
        {
            if (state == GameState.Ready)
            {
                AnimationStart();
                ChambaraManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            }
        }

        void AnimationStart()
        {
            canvasGroup.alpha = 1f;
            readyAnimation.SetTrigger("Start");
        }

        public void Go()
        {
            ChambaraManager.Instance.StartGame();
        }
    }
}
