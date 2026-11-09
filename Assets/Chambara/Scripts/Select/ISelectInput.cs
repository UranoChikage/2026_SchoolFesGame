using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// キャラ選択画面での1人分の入力元（デバッグ用キーボード、Joy-Conなど）
    /// 毎フレーム1回 Tick を呼んでから各値を読む
    /// </summary>
    public interface ISelectInput
    {
        void Tick(float unscaledDeltaTime);

        /// <summary>このフレームにカーソルを動かす方向（上下左右のどれか1つ。動かさないなら zero）</summary>
        Vector2Int Move { get; }
        bool Confirm { get; }
        bool Cancel { get; }
    }
}
