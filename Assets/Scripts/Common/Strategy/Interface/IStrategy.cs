using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// ストラテジーパターンのインターフェース
    /// </summary>
    public interface IStrategy<T>
    {
        /// <summary>
        /// ストラテジーのキー
        /// </summary>
        T Key { get; }
        /// <summary>
        /// ストラテジーを実行する
        /// </summary>
        void Execute(GameObject owner);
    }
}