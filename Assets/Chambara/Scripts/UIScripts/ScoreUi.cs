using TMPro;
using UnityEngine;

namespace Chambara
{
    [System.Serializable]
    public class ScoreUiData
    {
        [SerializeField] private TextMeshProUGUI beforeScore;
        [SerializeField] private TextMeshProUGUI afterScore;

        [SerializeField] private Animator animator;
        private const string TriggerName = "Action";

        public void SetScore(int before, int after)
        {
            beforeScore.text = before.ToString();
            afterScore.text = after.ToString();
        }

        public void PlayAnimation()
        {
            animator.SetTrigger(TriggerName);
        }
    }

    public class ScoreUi : MonoBehaviour
    {
        [SerializeField] private ScoreUiData player1ScoreUi;
        [SerializeField] private ScoreUiData player2ScoreUi;

        [SerializeField] private GameObject parent;


        private void Start()
        {
            parent.SetActive(false);
        }

        //winner is 1 for player 1 and 2 for player 2
        public void UpdateScore(int score1,int score2,int winner)
        {
            parent.SetActive(true);

            if (winner == 1)
            {
                player1ScoreUi.SetScore(score1 - 1, score1);
                player1ScoreUi.PlayAnimation();

                player2ScoreUi.SetScore(score2, score2);
            }
            else if (winner == 2)
            {
                player2ScoreUi.SetScore(score2 - 1, score2);
                player2ScoreUi.PlayAnimation();

                player1ScoreUi.SetScore(score1, score1);
            }
        }
    }
}
