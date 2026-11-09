using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// 試合（2本先取）に勝ったときの演出
    /// </summary>
    public class WinnerResult : MonoBehaviour
    {
        // winner: 勝ったプレイヤー（0:1P 1:2P）　score1 / score2: 最終スコア
        public void Play(int winner, int score1, int score2)
        {
            Debug.Log($"{winner + 1}P の勝ち！ {score1} - {score2}");
        }
    }
}
