using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// FallingRectBackground の設定。配色ごとにアセットを分けて差し替えられる。
    /// 再生中に値を変えるとその場で反映される。
    /// </summary>
    [CreateAssetMenu(fileName = "FallingRectBackgroundSettings", menuName = "Chambara/Falling Rect Background Settings")]
    public class FallingRectBackgroundSettings : ScriptableObject
    {
        [Header("調整用パラメータ")]
        [Tooltip("同時に存在する長方形の数")]
        [Range(10, 150)] public int count = 60;
        [Tooltip("全体の落下速度にかける倍率")]
         public float speedMultiplier = 1f;
        [Tooltip("0:形が変わらない / 1:開始と終了の形がまったく異なる")]
        [Range(0f, 1f)] public float deformAmount = 1f;

        [Header("ランダム範囲(Canvas 単位 = 参照解像度の px)")]
        public Vector2 widthRange = new Vector2(6f, 70f);
        public Vector2 heightRange = new Vector2(30f, 170f);
        public Vector2 speedRange = new Vector2(40f, 160f);
        public Vector2 alphaRange = new Vector2(0.15f, 0.7f);
        [Tooltip("画面上端から、どれだけ上の範囲でばらけて生成するか")]
        public float spawnAboveMargin = 200f;

        [Header("見た目")]
        public Color backgroundColor = new Color(0.078f, 0.071f, 0.169f);
        [Tooltip("長方形の色(ランダムに1色選ばれる。アルファは無視される)")]
        public Color[] colors = { Color.white };

        [Header("時間")]
        [Tooltip("Time.timeScale の影響を受けない(ヒットストップ等で止まらない)")]
        public bool useUnscaledTime = true;
        public float maxDeltaTime = 0.05f;
    }
}
