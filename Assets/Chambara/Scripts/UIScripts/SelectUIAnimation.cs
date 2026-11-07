using System.Collections;
using UnityEngine;

namespace Chambara
{
    public class SelectUIAnimation : MonoBehaviour
    {
        [SerializeField] RectTransform[] buttons;
        [SerializeField] Vector2 startPosOffset= new Vector2(0, -100);
        [SerializeField] float cooldown = 0.1f;
        [SerializeField] float moveDuration = 0.5f;

        Vector2[] initPoses;

        [Header("Debug")]
        [SerializeField] bool playAnimationOnStart = false;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            if(buttons.Length == 0) GetComponentButtons();

            if (playAnimationOnStart) PlayAnimation();
        }

        public void PlayAnimation()
        {
            initPoses = new Vector2[buttons.Length];

            float totalCooldown = 0f;

            bool[] useButtonindex = new bool[buttons.Length];

            for (int i = 0; i < buttons.Length; i++)
            {
                int j = 0;

                for(int k = 0; k < 1000; k++)
                {
                    j = Random.Range(0, buttons.Length);
                    if (!useButtonindex[j])
                    {
                        useButtonindex[j] = true;
                        break;
                    }
                }

                if (buttons[j] == null) continue;

                initPoses[j] = buttons[j].anchoredPosition;

                buttons[j].anchoredPosition += startPosOffset;

                StartCoroutine(PlayAnimationCoroutine(totalCooldown, j));
                totalCooldown += cooldown;
            }
        }

        IEnumerator PlayAnimationCoroutine(float startCooldown,int index)
        {
            yield return new WaitForSeconds(startCooldown);
            if (buttons[index] == null) yield break;
            float elapsedTime = 0f;
            float duration = moveDuration;
            Vector2 startPos = buttons[index].anchoredPosition;
            Vector2 targetPos = initPoses[index];
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                buttons[index].anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }
            buttons[index].anchoredPosition = targetPos;
        }

        void GetComponentButtons()
        {
            buttons = new RectTransform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i) is null) return;
                buttons[i] = transform.GetChild(i).GetComponent<RectTransform>();
            }
        }
    }
}
