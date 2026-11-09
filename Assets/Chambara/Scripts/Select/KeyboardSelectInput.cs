using UnityEngine;
using UnityEngine.InputSystem;

namespace Chambara
{
    /// <summary>
    /// キャラ選択画面のデバッグ用キーボード入力
    /// 1P: WASD 移動 / Space 決定 / 左Shift 戻る
    /// 2P: テンキー8462 移動 / テンキー5 決定 / テンキー0 戻る
    /// 押しっぱなしにすると一定間隔で連続移動する
    /// </summary>
    public class KeyboardSelectInput : ISelectInput
    {
        // 上, 下, 左, 右
        static readonly Key[][] DirKeys =
        {
            new[] { Key.W, Key.S, Key.A, Key.D },
            new[] { Key.Numpad8, Key.Numpad2, Key.Numpad4, Key.Numpad6 },
        };
        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        static readonly Key[] ConfirmKeys = { Key.Space, Key.Numpad5 };
        static readonly Key[] CancelKeys = { Key.LeftShift, Key.Numpad0 };

        const float RepeatDelay = 0.4f;     // 押しっぱなしで連続移動が始まるまで
        const float RepeatInterval = 0.12f; // 連続移動の間隔

        readonly int player;
        int heldDir = -1;
        float repeatTimer;

        public Vector2Int Move { get; private set; }
        public bool Confirm { get; private set; }
        public bool Cancel { get; private set; }

        public KeyboardSelectInput(int player) => this.player = player;

        public void Tick(float dt)
        {
            Move = Vector2Int.zero;
            Confirm = Cancel = false;

            var kb = Keyboard.current;
            if (kb == null) return;

            Key[] keys = DirKeys[player];

            // 新しく押したキーを優先する
            for (int i = 0; i < keys.Length; i++)
            {
                if (kb[keys[i]].wasPressedThisFrame)
                {
                    heldDir = i;
                    repeatTimer = RepeatDelay;
                    Move = Dirs[i];
                }
            }

            if (Move == Vector2Int.zero && heldDir >= 0)
            {
                if (!kb[keys[heldDir]].isPressed)
                {
                    heldDir = -1;
                }
                else
                {
                    repeatTimer -= dt;
                    if (repeatTimer <= 0f)
                    {
                        repeatTimer += RepeatInterval;
                        Move = Dirs[heldDir];
                    }
                }
            }

            Confirm = kb[ConfirmKeys[player]].wasPressedThisFrame;
            Cancel = kb[CancelKeys[player]].wasPressedThisFrame;
        }
    }
}
