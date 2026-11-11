using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// キャラ選択画面のプレビュー用モデルの動きの数値（SelectModelMotion が使う）
    /// </summary>
    [CreateAssetMenu(fileName = "SelectModelMotion", menuName = "Chambara/SelectModelMotion")]
    public class SelectModelMotionConfig : ScriptableObject
    {
        [Header("待機（浮遊）")]
        [Tooltip("上下に浮く幅")]
        public float breathHeight = 0.02f;
        [Tooltip("浮く速さ")]
        public float breathSpeed = 3f;

        [Header("決定時のジャンプ")]
        public float jumpHeight = 0.25f;
        [Tooltip("跳んでから着地するまで（実時間）")]
        public float jumpDuration = 0.35f;
        [Tooltip("剣を決めたときも跳ぶ")]
        public bool jumpOnSword = true;
        [Tooltip("ジャンプ中に縦軸で何回転するか（マイナスで逆回り、0で回らない）")]
        public float jumpSpins = 1f;

        [Header("表情（BodyFace があるキャラのみ）")]
        [Tooltip("選んでいる最中の顔")]
        public BodyFace.Face selectingFace = BodyFace.Face.Normal;
        [Tooltip("キャラと剣の両方を決めたときの顔")]
        public BodyFace.Face readyFace = BodyFace.Face.Happy;
    }
}
