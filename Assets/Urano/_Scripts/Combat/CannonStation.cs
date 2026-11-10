using UnityEngine;
using UnityEngine.Serialization;
using Common;

namespace Pirates
{
    /// <summary>
    /// 1人の砲手ぶんの照準と発射。砲は見た目だけで、画面上のカーソルが指す位置へ砲弾を飛ばす（Wiiのポインタ風）。
    /// カーソルの動かし方：JoyConのジャイロ（Title の Select と同じ。Cancelで中央に戻す）／（JoyCon無し）マウスまたはキーボード。
    /// 1画面を全員で共有するので、砲手ごとに色付きのカーソルを画面に描く。
    /// </summary>
    [RequireComponent(typeof(CannonShooter))]
    [RequireComponent(typeof(AimController))]
    public class CannonStation : MonoBehaviour
    {
        [Header("プレイヤー")]
        [SerializeField]
        [Tooltip("使用するJoyConのインデックス（舵取りの1Pが0なら、砲手は1,2,3）")]
        int joyconIndex = 1;
        [SerializeField]
        [Tooltip("通常はOFFのまま。JoyConが無い／繋がっていても反応がないときは自動でマウスになる。ONにするとJoyConがあっても常にマウス（PCデバッグ用）")]
        [FormerlySerializedAs("forceKeyboard")]
        bool forceMouse = false;
        [SerializeField]
        [Tooltip("砲手番号(1〜9)。PC代替操作中は、この番号の数字キーを押した砲だけが操作される")]
        int stationNumber = 1;

        [Header("砲身（見た目）")]
        [SerializeField]
        [Tooltip("砲身の見た目（任意）。狙う位置へ向ける")]
        Transform barrel;

        [Header("JoyCon（ジャイロでカーソルを動かす）")]
        [SerializeField]
        [Tooltip("1度の回転あたりのカーソル移動量(px)の初期値。砲手全員で共有（最初に起動した砲の値が使われる）。Title の Select の感度とは別")]
        float sensitivity = 12000f;
        [SerializeField]
        [Tooltip("スティック左右で調整できる下限")]
        float sensitivityMin = 12000f;
        [SerializeField]
        [Tooltip("スティック左右で調整できる上限")]
        float sensitivityMax = 16000f;
        [SerializeField]
        [Tooltip("スティックを倒しきったときの1秒あたりの変化量")]
        float sensitivityStep = 500f;
        [SerializeField]
        [Tooltip("スティックのこれ以下の傾きは無視する")]
        float stickDeadZone = 0.3f;
        [SerializeField]
        [Tooltip("ローカル操作を許容する角度の広さ。1.41で±45度、2で±60度")]
        float yawRelaxFactor = 1f;
        [SerializeField]
        [Tooltip("重力推定の追従速度。小さいほど安定するが遅れる")]
        float gravityBlend = 0.02f;

        [Header("感度調整のリアクション")]
        [SerializeField]
        [Tooltip("感度1変化あたりのカーソルのスケール変化量")]
        float scaleReaction = 0.02f;
        [SerializeField]
        [Tooltip("スケールが元に戻る速さ(秒)")]
        float scaleRecovery = 0.12f;
        [SerializeField]
        [Tooltip("スケールの変化幅の上限")]
        float scaleLimit = 0.35f;

        [Header("カーソル表示")]
        [SerializeField]
        Color cursorColor = Color.red;
        [SerializeField]
        [Tooltip("カーソルの画像（任意）。未設定なら十字を描く。VFX/Textures/Crosshair.png など")]
        Texture2D cursorTexture;
        [SerializeField]
        float cursorSize = 40f;

        // PC代替操作で現在操作している砲手番号（全砲で共有）
        static int activeStationNumber = 1;

        // 砲手全員で共有するジャイロ感度（負＝未初期化。最初に起動した砲のInspector値で初期化する）
        static float sharedSensitivity = -1f;

        /// <summary>砲手全員で共有するジャイロ感度(px/度)。Titleの感度とは別</summary>
        public static float SharedSensitivity => sharedSensitivity;

        // 感度を変えたときのカーソルの拡大縮小（全員共有。1フレームに1回だけ戻す）
        static float reactionOffset;
        static float reactionVelocity;
        static int reactionFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic()
        {
            activeStationNumber = 1;
            sharedSensitivity = -1f;
            reactionOffset = 0f;
            reactionVelocity = 0f;
            reactionFrame = -1;
        }

        CannonShooter shooter;
        AimController aim;
        TrajectoryLine trajectory;
        Vector2 cursor;
        readonly GyroPointer pointer = new GyroPointer();
        string status = "";
        Quaternion barrelRotInStation = Quaternion.identity; // 砲身の初期姿勢（この砲を基準）
        ItemHolder items;

        /// <summary>この砲手の色（カーソル・予測線と同じ）。獲得通知などで使う</summary>
        public Color PlayerColor => cursorColor;

        void Awake()
        {
            shooter = GetComponent<CannonShooter>();
            items = ItemHolder.For(shooter);
            aim = GetComponent<AimController>();
            if (sharedSensitivity < 0f) sharedSensitivity = Mathf.Clamp(sensitivity, sensitivityMin, sensitivityMax);
            trajectory = GetComponent<TrajectoryLine>(); // 付いていれば予測線を出す（任意）
            if (trajectory != null) trajectory.SetColor(cursorColor);
            if (barrel != null) barrelRotInStation = Quaternion.Inverse(transform.rotation) * barrel.rotation;
        }

        void Start()
        {
            // 砲手ごとに初期位置をずらして重ならないようにする
            float x = Screen.width * (0.25f + 0.25f * ((Mathf.Max(1, stationNumber) - 1) % 3));
            cursor = new Vector2(x, Screen.height * 0.5f);
        }

        void Update()
        {
            SelectByNumberKey();
            RecoverReaction();

            bool joycon = !forceMouse && GameInput.IsJoyconConnected(joyconIndex);
            bool fire;

            if (joycon)
            {
                status = "JOYCON";
                fire = UpdateJoycon();
            }
            else if (activeStationNumber != stationNumber)
            {
                status = "waiting"; // 選ばれていない砲はPC操作を受け付けない
                fire = false;
            }
            else
            {
                status = "MOUSE";
                fire = UpdateMouse();
            }

            cursor.x = Mathf.Clamp(cursor.x, 0f, Screen.width);
            cursor.y = Mathf.Clamp(cursor.y, 0f, Screen.height);

            aim.SetScreenPosition(cursor);
            aim.Refresh();

            items.Tick(aim.AimPoint, aim.HasAim);

            if (aim.HasAim)
            {
                UpdateBarrel(aim.AimPoint);
                // アイテム所持中は発射ボタンで常にアイテムを使う。ビーム使用中はこの砲は撃てない
                if (fire && !items.TryUse() && !items.IsBeamActive) shooter.TryFireAt(aim.AimPoint);
            }

            // ビーム使用中はこの砲は撃てないので、予測線は出さない
            UpdateTrajectory(status != "waiting" && shooter.IsAlive && !items.IsBeamActive);
        }

        // 予測線：選ばれている砲だけ、カーソルが指す位置へ撃った弾道を表示する
        void UpdateTrajectory(bool active)
        {
            if (trajectory == null) return;

            if (active && aim.HasAim && shooter.TryGetLaunchVelocity(aim.AimPoint, out Vector3 velocity))
            {
                trajectory.Show(shooter.MuzzlePosition, velocity, aim.AimPoint.y, shooter.IsReady);
            }
            else
            {
                trajectory.Hide();
            }
        }

        // 数字キー（上段／テンキー）で操作する砲手を切り替える
        void SelectByNumberKey()
        {
            if (GameInput.StationSelectPressed(stationNumber)) activeStationNumber = stationNumber;
        }

        // Title の Select と同じ：角速度を積算して動かす。リセット（Cancel）でカーソルを画面中央に戻す
        bool UpdateJoycon()
        {
            if (GameInput.JoyconRecenterPressed(joyconIndex))
            {
                cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                pointer.Reset();
            }

            AdjustSensitivity();
            cursor += pointer.Rate(joyconIndex, yawRelaxFactor, gravityBlend) * sharedSensitivity * Time.deltaTime;
            return GameInput.JoyconFirePressed(joyconIndex);
        }

        // スティック左右で感度を調整する（全員共通の値。誰のスティックでも動かせる）
        void AdjustSensitivity()
        {
            float x = SwitchInput.I.Stick(joyconIndex).x;
            if (Mathf.Abs(x) < stickDeadZone) return;

            float amount = (Mathf.Abs(x) - stickDeadZone) / (1f - stickDeadZone);
            float before = sharedSensitivity;
            sharedSensitivity = Mathf.Clamp(
                sharedSensitivity + Mathf.Sign(x) * amount * sensitivityStep * Time.deltaTime,
                sensitivityMin, sensitivityMax);

            // 変化量ぶんカーソルを押し上げて手応えにする。上下限に張り付くと変化量は0になり、拡大は止まる
            float delta = sharedSensitivity - before;
            if (delta != 0f)
                reactionOffset = Mathf.Clamp(reactionOffset + delta * scaleReaction, -scaleLimit, scaleLimit);
        }

        void RecoverReaction()
        {
            if (reactionFrame == Time.frameCount) return;
            reactionFrame = Time.frameCount;
            reactionOffset = Mathf.SmoothDamp(reactionOffset, 0f, ref reactionVelocity, scaleRecovery);
        }

        bool UpdateMouse()
        {
            if (!GameInput.TryGetMousePosition(out Vector2 pos)) return false;
            cursor = pos;
            return GameInput.MouseFirePressed();
        }

        // 砲身を狙う位置へ水平方向（Y軸回転のみ）に向ける（見た目だけ）。
        // 初期の傾き（X/Z）は保持し、Y軸まわりに回した分だけを足す。
        void UpdateBarrel(Vector3 point)
        {
            if (barrel == null) return;

            Vector3 to = point - barrel.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;

            // 初期姿勢を、船の動きに追従させたワールド姿勢
            Quaternion baseRot = transform.rotation * barrelRotInStation;
            Vector3 baseForward = baseRot * Vector3.forward;
            baseForward.y = 0f;
            if (baseForward.sqrMagnitude < 0.0001f) return;

            float yaw = Vector3.SignedAngle(baseForward, to, Vector3.up);
            barrel.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRot;
        }

        // 砲手ごとの色付きカーソル（クールタイム中は小さく）
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !shooter.IsAlive) return;

            // アイテムを持っている間は、そのアイテムのカーソルの大きさ・画像に切り替える
            ItemAsset item = items != null ? items.CursorItem : null;
            float baseSize = item != null && item.CursorSize > 0f ? item.CursorSize : cursorSize;
            Sprite sprite = item != null ? item.CursorSprite : null;
            float size = baseSize * (shooter.IsReady ? 1f : 0.6f) * (1f + reactionOffset);
            float x = cursor.x;
            float y = Screen.height - cursor.y; // GUIは上原点

            Color old = GUI.color;
            GUI.color = cursorColor;
            if (sprite != null)
            {
                // スプライトシート上の位置を指定して描く
                Rect tr = sprite.textureRect;
                var uv = new Rect(tr.x / sprite.texture.width, tr.y / sprite.texture.height,
                    tr.width / sprite.texture.width, tr.height / sprite.texture.height);
                GUI.DrawTextureWithTexCoords(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), sprite.texture, uv);
            }
            else if (cursorTexture != null)
            {
                GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), cursorTexture);
            }
            else
            {
                GUI.DrawTexture(new Rect(x - size * 0.5f, y - 2f, size, 4f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x - 2f, y - size * 0.5f, 4f, size), Texture2D.whiteTexture);
            }
            // 砲手の表示名。砲手1〜3 = P2〜P4（P1は舵取り役）
            GUI.Label(new Rect(x + size * 0.5f + 4f, y - 12f, 200f, 24f), $"P{stationNumber + 1}");
            GUI.color = old;
        }
    }
}
