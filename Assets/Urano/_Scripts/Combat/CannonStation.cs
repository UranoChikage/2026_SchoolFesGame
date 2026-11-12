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

        [Header("エイムアシスト（敵船にゆるく吸い付く・軽く偏差）")]
        [SerializeField]
        [Tooltip("ONで、カーソルが敵船の近くにあると少しだけ吸い付き、敵の動きを軽く先読みする。手で動かせば外れるので、狙う操作は残る")]
        bool aimAssist = true;
        [SerializeField]
        [Tooltip("敵船の画面上の位置からこの距離(px)以内にカーソルがあるときだけ働く。大きいほど遠くから効く")]
        float assistRadius = 220f;
        [SerializeField]
        [Tooltip("ONで、海に浮かぶアイテムにも吸い付く（まだアイテムを持っていないときだけ）")]
        bool assistItems = true;
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("吸い付きの強さ。0=なし、1=敵船に完全に重なる。小さいほどゆるい")]
        float assistStrength = 0.45f;
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("偏差の量。0=偏差なし、1=敵の速度×弾の滞空時間ぶん先を狙う。小さいほど軽い")]
        float leadAmount = 0.4f;
        [SerializeField]
        [Tooltip("吸い付きの滑らかさ(秒)。大きいほどゆっくり寄る")]
        float assistSmoothTime = 0.18f;
        [SerializeField]
        [Tooltip("アシストが効き始めた瞬間、カーソルを跳ねさせながら1回転させる（見た目だけの演出）")]
        bool lockOnSpin = true;
        [SerializeField]
        [Tooltip("1回転にかける時間(秒)")]
        float lockOnSpinTime = 0.45f;
        [SerializeField]
        [Tooltip("跳ねるときの拡大量。0.5で最大1.5倍")]
        float lockOnPop = 0.5f;
        [SerializeField]
        [Tooltip("ロック中、敵船のまわりにブラケット（四隅の枠）と、偏差で狙っている位置の点を出す。寄り具合に応じて濃くなる")]
        bool showLockMarker = true;
        [SerializeField]
        [Tooltip("ロックのブラケットの大きさ(px)")]
        float lockMarkerSize = 110f;

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
        Vector2 cursor;          // 表示・照準に使う最終位置（inputCursor + assistOffset）
        Vector2 inputCursor;     // 入力だけで動かした位置。アシストの影響を受けない
        Vector2 assistOffset;    // アシストによるずれ
        Vector2 assistVelocity;
        IAimTarget assistTarget; // いま吸い付いている対象（敵船・アイテム。ころころ変わらないよう粘らせる）
        const float MaxLeadTime = 2f;
        float spinStartTime = -999f; // 回転演出を始めた時刻（Time.unscaledTime）
        Vector2 lockShipScreen;      // ロック中の敵船の画面座標（ブラケットを描く位置）
        Vector2 lockLeadScreen;      // 偏差を足した狙い位置の画面座標
        float lockWeight;            // 寄り具合（0〜1）
        readonly GyroPointer pointer = new GyroPointer();
        string status = "";
        Quaternion barrelRotInStation = Quaternion.identity; // 砲身の初期姿勢（この砲を基準）
        ItemHolder items;

        /// <summary>この砲手が使うJoyConの番号。振動の送り先に使う</summary>
        public int JoyconIndex => joyconIndex;

        /// <summary>この砲手のコントローラーを振動させる。duration が負なら StopVibrate まで続く</summary>
        public void Vibrate(bool strong, float duration)
        {
            if (strong) GameVibration.High(joyconIndex, duration);
            else GameVibration.Low(joyconIndex, duration);
        }

        public void StopVibrate() => GameVibration.Stop(joyconIndex);

        /// <summary>振動データを1回再生（他の振動と同時に鳴る）。再生できなければ短い振動で代用</summary>
        public void PlayVibration(string path, bool fallbackStrong, float fallbackDuration)
            => GameVibration.PlayFile(joyconIndex, path, fallbackStrong, fallbackDuration);

        /// <summary>振動データをループ再生して再生IDを返す。止めるときは StopLoopVibration に渡す</summary>
        public int LoopVibration(string path, float duration = -1f) => GameVibration.LoopFile(joyconIndex, path, duration);
        public void StopLoopVibration(int playId) => GameVibration.StopLoop(joyconIndex, playId);

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
            inputCursor = cursor;
        }

        void Update()
        {
            SelectByNumberKey();
            RecoverReaction();

            cursor = inputCursor; // 入力はアシストを含まない位置に対して行う（アシストが積み重ならないように）

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
            inputCursor = cursor;

            ApplyAimAssist(status != "waiting");

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

        // エイムアシスト：入力のカーソル(inputCursor)に「敵船へ寄るずれ」を足して、最終のカーソル(cursor)にする。
        // 手でカーソルを動かせば敵船から離れてずれが小さくなるので、狙う操作はそのまま残る
        void ApplyAimAssist(bool active)
        {
            Vector2 desired = Vector2.zero;
            if (aimAssist && active && shooter.IsAlive)
            {
                if (TryFindAssistTarget(out Vector2 targetScreen, out float weight))
                    desired = (targetScreen - inputCursor) * (assistStrength * weight);
            }
            else
            {
                assistTarget = null; // アシストが切れたらロックも解く（目印が残らないように）
            }

            assistOffset = Vector2.SmoothDamp(assistOffset, desired, ref assistVelocity, assistSmoothTime);
            cursor = inputCursor + assistOffset;
            cursor.x = Mathf.Clamp(cursor.x, 0f, Screen.width);
            cursor.y = Mathf.Clamp(cursor.y, 0f, Screen.height);
        }

        // 入力のカーソルに画面上で一番近い敵船を探す。targetScreen=偏差を足した狙い位置の画面座標、weight=近いほど1（半径の外は0）
        bool TryFindAssistTarget(out Vector2 targetScreen, out float weight)
        {
            targetScreen = default;
            weight = 0f;

            Camera cam = Camera.main;
            if (cam == null || assistRadius <= 0f) return false;

            IAimTarget best = null;
            float bestScore = float.MaxValue;
            float bestDistance = 0f;
            Vector2 bestScreen = default;

            foreach (EnemyShip enemy in EnemyShip.Active)
            {
                if (enemy == null) continue;
                ConsiderTarget(cam, enemy, ref best, ref bestScore, ref bestDistance, ref bestScreen);
            }

            // 海に浮かぶアイテムにも吸い付く。ただし取れない状態（すでに所持中・ビーム使用中）なら狙っても無駄なので対象にしない
            if (assistItems && items != null && items.Held == null && !items.IsBeamActive)
            {
                foreach (ItemPickup pickup in ItemPickup.Active)
                {
                    if (pickup == null) continue;
                    ConsiderTarget(cam, pickup, ref best, ref bestScore, ref bestDistance, ref bestScreen);
                }
            }

            // ロックする敵が決まった瞬間（敵がいない→いる、または別の敵へ移った）に、カーソルの回転演出を始める。
            // 回転の途中で別の敵に割り込まれたら、そこから頭からやり直す
            if (best != null && best != assistTarget && lockOnSpin) spinStartTime = Time.unscaledTime;
            assistTarget = best;
            if (best == null) return false;

            // 偏差：敵の速度 × 弾の滞空時間 のうち、leadAmount ぶんだけ先を狙う
            Vector3 aimPoint = best.AimPosition;
            if (leadAmount > 0f && shooter.TryGetFlightTime(aimPoint, out float flightTime))
            {
                Vector3 velocity = best.AimVelocity;
                velocity.y = 0f;
                aimPoint += velocity * (Mathf.Min(flightTime, MaxLeadTime) * leadAmount);
            }

            Vector3 led = cam.WorldToScreenPoint(aimPoint);
            if (led.z <= 0f)
            {
                assistTarget = null;
                return false;
            }
            targetScreen = new Vector2(led.x, led.y);
            lockShipScreen = bestScreen;
            lockLeadScreen = targetScreen;

            float t = Mathf.Clamp01(bestDistance / assistRadius);
            weight = 1f - t * t * (3f - 2f * t); // 近いほど1、半径の端で0（なめらか）
            lockWeight = weight;
            return true;
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
        // 候補1つを調べて、いまの最有力より近ければ入れ替える
        void ConsiderTarget(Camera cam, IAimTarget target, ref IAimTarget best, ref float bestScore,
            ref float bestDistance, ref Vector2 bestScreen)
        {
            if (!target.CanAssist) return;

            Vector3 sp = cam.WorldToScreenPoint(target.AimPosition);
            if (sp.z <= 0f) return; // カメラの後ろ

            float distance = Vector2.Distance(new Vector2(sp.x, sp.y), inputCursor);
            bool locked = target == assistTarget;
            if (distance > assistRadius * (locked ? 1.25f : 1f)) return; // 今吸い付いている対象は、少し遠くまで離さない

            float score = locked ? distance * 0.7f : distance; // 粘らせて、近いもの同士で行ったり来たりしないようにする
            if (score < bestScore)
            {
                bestScore = score;
                best = target;
                bestDistance = distance;
                bestScreen = new Vector2(sp.x, sp.y);
            }
        }

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

        // ロック中の目印：敵船のまわりの四隅のブラケットと、偏差で狙っている位置の点。寄り具合が強いほど濃くなる
        void DrawLockMarker()
        {
            if (!showLockMarker || assistTarget == null) return;

            Color c = cursorColor;
            c.a = Mathf.Lerp(0.4f, 1f, lockWeight);
            Color old = GUI.color;
            GUI.color = c;

            Texture2D white = Texture2D.whiteTexture;
            float cx = lockShipScreen.x;
            float cy = Screen.height - lockShipScreen.y;
            float half = lockMarkerSize * 0.5f * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 8f)); // ゆるく脈打つ
            float len = lockMarkerSize * 0.28f;
            const float thick = 3f;

            // 四隅（左上・右上・左下・右下）に「 」型を描く
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    float px = cx + sx * half;
                    float py = cy + sy * half;
                    GUI.DrawTexture(new Rect(sx > 0 ? px - len : px, sy > 0 ? py - thick : py, len, thick), white);
                    GUI.DrawTexture(new Rect(sx > 0 ? px - thick : px, sy > 0 ? py - len : py, thick, len), white);
                }
            }

            // 偏差で狙っている位置
            float lx = lockLeadScreen.x;
            float ly = Screen.height - lockLeadScreen.y;
            GUI.DrawTexture(new Rect(lx - 4f, ly - 4f, 8f, 8f), white);

            GUI.color = old;
        }

        // 回転演出の進み具合（0〜1）。演出中でなければ -1
        float SpinProgress()
        {
            if (!lockOnSpin || lockOnSpinTime <= 0f) return -1f;
            float t = (Time.unscaledTime - spinStartTime) / lockOnSpinTime;
            return t >= 0f && t < 1f ? t : -1f;
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
            float labelOffset = size * 0.5f;    // 名前の位置は演出で動かさない

            // アシストが効いた瞬間：跳ねながら（拡大して戻る）1回転する。見た目だけ
            Matrix4x4 oldMatrix = GUI.matrix;
            float spin = SpinProgress();
            if (spin >= 0f)
            {
                float ease = 1f - Mathf.Pow(1f - spin, 3f);          // 最初は速く、最後にゆっくり止まる
                size *= 1f + lockOnPop * Mathf.Sin(spin * Mathf.PI); // 真ん中で一番大きく
                GUIUtility.RotateAroundPivot(360f * ease, new Vector2(x, y));
            }

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
            GUI.matrix = oldMatrix; // 名前は回さない
            DrawLockMarker();
            // 砲手の表示名。砲手1〜3 = P2〜P4（P1は舵取り役）
            GUI.Label(new Rect(x + labelOffset + 4f, y - 12f, 200f, 24f), $"P{stationNumber + 1}");
            GUI.color = old;
        }
    }
}
