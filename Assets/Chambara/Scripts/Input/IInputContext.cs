using UnityEngine;

namespace Chambara
{
    public struct PlayerInput
    {
        public int pose;   // 今の構え方向（-1:なし）
        public int swing;  // 振った方向（-1:振っていない）

        public bool isFront;   // 前進ボタンを押しているか（押しっぱなしの状態を返す。押し直しの判定はPlayerBehaviour側）
        public bool isBack;    // 後退ボタンを押しているか

        public static readonly PlayerInput None = new PlayerInput { pose = -1, swing = -1 };
    }

    /// <summary>
    /// プレイヤー1人分の入力元（デバッグ用キーボード、Joy-Conなど）
    /// </summary>
    public interface IChambaraInputContext
    {
        void Init(int playerIndex);

        /// <summary>
        /// 今の入力を返す。振り入力は読んだら消費される
        /// </summary>
        PlayerInput Read();
    }

    /// <summary>
    /// 剣の向きをそのまま動かせる入力元（Joy-Conなど）。アイドル・移動中の剣に反映する
    /// </summary>
    public interface ISwordTracking
    {
        /// <summary>剣（pivot）のプレイヤー基準の回転。取れないときは false</summary>
        bool TryGetSwordRotation(out Quaternion rotation);
    }
}
