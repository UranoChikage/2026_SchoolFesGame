using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// 試合（2本先取）の決着演出のポーズの数値（BodyRig が使う）
    /// 勝った方は剣を持った右腕を突き上げて喜び、負けた方は崩れ落ちて両手で顔を覆って泣く
    /// </summary>
    [CreateAssetMenu(fileName = "ResultPose", menuName = "Chambara/ResultPose")]
    public class ResultPoseConfig : ScriptableObject
    {
        [Header("勝ち（剣を持った右腕を突き上げる。左手は腰）")]
        [Tooltip("頭のてっぺんより上に剣を握った手を上げる高さ")]
        public float victoryRaise = 0.15f;
        [Tooltip("上げた手を胴の横からさらに外へ出す距離")]
        public float victorySpread = 0.05f;
        [Tooltip("上げた剣を外側へ傾ける角度")]
        public float victoryBladeTilt = 15f;
        [Tooltip("ガッツポーズで手を突き上げ直す高さ")]
        public float victoryPumpHeight = 0.06f;
        [Tooltip("ガッツポーズの速さ")]
        public float victoryPumpSpeed = 5f;
        [Tooltip("胸を張って反る角度")]
        public float victoryLeanBack = 8f;
        [Tooltip("上を向く角度")]
        public float victoryHeadUp = 12f;

        [Header("負け（崩れ落ちて、両手で顔を覆って泣く）")]
        [Tooltip("崩れ落ちるまでの間（秒）")]
        public float defeatDelay = 0.4f;
        [Tooltip("崩れ落ちて沈む深さ")]
        public float defeatSink = 0.3f;
        [Tooltip("崩れ落ちるバネの硬さ（大きいほど速く落ちる）")]
        public float defeatSinkStiffness = 120f;
        [Tooltip("崩れ落ちるバネの減衰（小さいほど落ちた後に弾む）")]
        public float defeatSinkDamping = 9f;
        [Tooltip("前かがみになる角度")]
        public float defeatBend = 35f;
        [Tooltip("うつむく角度")]
        public float defeatHeadDrop = 20f;
        [Tooltip("顔の前に出す手と顔の隙間")]
        public float defeatHandGap = 0.03f;

        [Header("負け：しゃくり上げ")]
        [Tooltip("崩れ落ちてから震え始めるまで（秒）")]
        public float sobStartDelay = 0.6f;
        [Tooltip("胴の揺れ（度）")]
        public float sobAngle = 5f;
        [Tooltip("体の上下")]
        public float sobHeight = 0.02f;
        [Tooltip("震える速さ")]
        public float sobSpeed = 7f;

        [Header("負け：剣を落とす")]
        [Tooltip("落とした剣の位置（Player 基準。x:右 y:地面からの高さ z:前）")]
        public Vector3 droppedSwordPos = new Vector3(0.35f, 0.02f, 0.3f);
        [Tooltip("落とした剣の向き（度）")]
        public float droppedSwordYaw = -30f;
        [Tooltip("剣が地面に落ちるまでの時間（秒）")]
        public float swordDropTime = 0.35f;

        [Header("共通")]
        [Tooltip("決着のポーズへ手が動く速さ")]
        public float handSpeed = 10f;
    }
}
