using UnityEngine;
using UnityEngine.InputSystem;

namespace Chambara
{
    /// <summary>
    /// キーボードでのデバッグ入力
    /// 方向キーを押す → その方向に振る / ガードキー + 方向キーを押し続ける → その方向に構える
    /// 1P: QWE/A D/ZXC + 左Shift　2P: テンキー789/4 6/123 + テンキー0
    /// 前進/後退　1P: R / F　2P: ↑ / ↓
    /// 突き　1P: S　2P: テンキー5
    /// </summary>
    public class DebugInputContext : MonoBehaviour, IChambaraInputContext
    {
        // 上=0から時計回り
        static readonly Key[][] DirKeys =
        {
            new[] { Key.W, Key.E, Key.D, Key.C, Key.X, Key.Z, Key.A, Key.Q },
            new[] { Key.Numpad8, Key.Numpad9, Key.Numpad6, Key.Numpad3, Key.Numpad2, Key.Numpad1, Key.Numpad4, Key.Numpad7 },
        };
        static readonly Key[] GuardKeys = { Key.LeftShift, Key.Numpad0 };
        static readonly Key[] FrontKeys = { Key.R, Key.UpArrow };
        static readonly Key[] BackKeys  = { Key.F, Key.DownArrow };
        static readonly Key[] ThrustKeys = { Key.S, Key.Numpad5 };

        int playerIndex;
        int pose = -1;
        int pendingSwing = -1;
        bool pendingThrust;
        bool front, back;
        
        public void Init(int playerIndex) => this.playerIndex = playerIndex;

        public PlayerInput Read()
        {
            var input = new PlayerInput { pose = pose, swing = pendingSwing, thrust = pendingThrust, isFront = front, isBack = back };
            pendingSwing = -1;
            pendingThrust = false;
            return input;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            Key[] keys = DirKeys[playerIndex];
            bool guard = kb[GuardKeys[playerIndex]].isPressed;
            front = kb[FrontKeys[playerIndex]].isPressed;
            back  = kb[BackKeys[playerIndex]].isPressed;
            if (!guard && kb[ThrustKeys[playerIndex]].wasPressedThisFrame) pendingThrust = true;
            pose = -1;
            for (int i = 0; i < keys.Length; i++)
            {
                if (guard && kb[keys[i]].isPressed) pose = i;
                if (!guard && kb[keys[i]].wasPressedThisFrame) pendingSwing = i;
            }
        }
    }
}
