using UnityEngine;

namespace Common
{
    /// <summary>
    /// SDKに直接触る部分。SDKを差し替えるときはこれを実装したクラスを新しく作る。
    /// 各メソッドは「index が有効」という前提で呼ばれるので、接続チェックは不要。
    /// </summary>
    public interface ISwitchInputBackend
    {
        int ConnectedCount { get; }
        Vector3 Acceleration(int index);
        Vector3 AngularVelocity(int index);
        Vector2 Stick(int index);
        bool TryGetDirection(int index, out Vector3 x, out Vector3 y, out Vector3 z);
        bool ReadButton(int index, SwitchButton button);

        /// <summary>コントローラー設定画面を出す。対応しないバックエンドは何もしなくてよい</summary>
        void ShowControllerSupport();
    }
}