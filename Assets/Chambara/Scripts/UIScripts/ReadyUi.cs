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
            // ラウンドごとに Ready になるので、毎回流す
            if (state == GameState.Ready) AnimationStart();
        }

        private void OnDestroy()
        {
            if (ChambaraManager.Instance != null) ChambaraManager.Instance.OnGameStateChanged -= OnGameStateChanged;
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

        public void End()
        {
            canvasGroup.alpha = 0f;
        }
    }
}
