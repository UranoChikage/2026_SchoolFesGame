using System;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// 状態に合わせて剣の見た目を動かす
    /// pivot（握りの位置。刃は子オブジェクトとして+Y方向に伸ばす）の位置と回転を毎フレーム上書きする
    /// 角度は「相手から見て」ではなく「自分の後ろから見て」の向き（上=0から時計回り）
    /// </summary>
    [ExecuteAlways]
    public class Sword : MonoBehaviour
    {
        [SerializeField] Transform pivot;
        [SerializeField,Range(1,8)] int directions = 8;                                   // ChambaraRule.directions と合わせる
        [SerializeField] Vector3 gripPosition = new Vector3(0.2f, 0f, 0.4f);  // 握りの基準位置（Playerのローカル座標）
        [SerializeField] float bladeLength = 1f;                              // pivot から刃先までの長さ（ワールド座標）。衝突エフェクトの位置に使う

        [Header("通常の構え")]
        [SerializeField] float idleAngle = 45f;   // 真上=0、正面=90
        [SerializeField] float stunAngle = 0f;    // 怯み中（振り上げたまま）

        [Header("攻撃（0:振る方向の反対側 90:正面 180:振る方向）")]
        [SerializeField] float swingStart  = -20f;   // 振りかぶり
        [SerializeField] float swingEnd    = 160f;   // 振り終わり
        [SerializeField] float handMove    = 0.2f;   // 振りに合わせて手が動く距離
        [SerializeField] float straightTilt = 10f;   // 縦・横に振るときだけ斜めにずらす角度（時計回り）
        [SerializeField, Range(0f, 0.9f)] float windupRatio = 0.3f;   // 判定までの時間のうち振りかぶりに使う割合

        [Header("ガード（刃を構える方向に対して垂直に寝かせる）")]
        [SerializeField] Vector2 guardOffset = new Vector2(-0.2f, 0.3f);   // x:横 y:構える方向 にずらす
        [SerializeField] float guardTilt = 20f;                             // 刃先を前に傾ける角度

        [SerializeField] float returnSpeed = 15f;   // 構えの切り替えの速さ

        [Header("エディタでの確認用（再生していないときだけ反映）")]
        [SerializeField] PlayerState previewState = PlayerState.Idle;
        [SerializeField] int previewDir = 0;
        [SerializeField, Range(0f, 1f)] float previewSwing = 0f;

        [SerializeField] PlayerBehaviour player;
        Vector3 pos, attackFromPos;
        Quaternion rot = Quaternion.identity, attackFromRot;
        float swingProgress = 1f;   // 前フレームまでに振った割合（1なら振り切った）

        public Transform Pivot => pivot;
        public bool IsSwinging { get; private set; }
        public event Action OnSwingStart;   // 振りかぶりが終わり、振り下ろし始めた瞬間
        public event Action OnSwingEnd;     // 振り切った、または攻撃が中断された瞬間
        public event Action<float, float> OnSwingUpdate;   // 振っている間毎フレーム（前フレームの割合, 今の割合）

        private void Awake()
        {
            if(player==null) player = GetComponent<PlayerBehaviour>();
        }

        private void Start()
        {
            if (Application.isPlaying) player.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (player != null) player.OnStateChanged -= OnStateChanged;
        }

        private void OnStateChanged(PlayerState state, int dir)
        {
            // 振り始めた瞬間の構えから振りかぶりへつなぐ
            if (state == PlayerState.Attack)
            {
                attackFromPos = pos;
                attackFromRot = rot;
                swingProgress = 0f;
            }
        }

        private void LateUpdate()
        {
            if (pivot == null) return;

            if (!Application.isPlaying)
            {
                if (previewState == PlayerState.Attack) Swing(previewDir, previewSwing, out pos, out rot);
                else Pose(previewState, previewDir, out pos, out rot);
            }
            else if (player.Data == null)
            {
                SetSwinging(false);
                return;
            }
            else if (player.Data.state == PlayerState.Attack)
            {
                // 判定のタイミングでちょうど振り切る。判定後は振り終わりの位置で止める
                float t = (Time.time - player.AttackTime) / player.AttackStartup;
                if (t < windupRatio)
                {
                    Swing(player.Data.dir, 0f, out var p, out var r);
                    float k = Mathf.SmoothStep(0f, 1f, t / windupRatio);
                    pos = Vector3.Lerp(attackFromPos, p, k);
                    rot = Quaternion.Slerp(attackFromRot, r, k);
                }
                else
                {
                    float k = Mathf.Clamp01((t - windupRatio) / (1f - windupRatio));
                    float s = k * k;   // 加速しながら振る
                    Swing(player.Data.dir, s, out pos, out rot);

                    // 振り切ったフレームの分も通知してから終わる
                    if (swingProgress < 1f)
                    {
                        SetSwinging(true);
                        OnSwingUpdate?.Invoke(swingProgress, s);
                        swingProgress = s;
                        if (s >= 1f) SetSwinging(false);
                    }
                }
            }
            else
            {
                SetSwinging(false);
                Pose(player.Data.state, player.Data.dir, out var p, out var r);
                float k = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
                pos = Vector3.Lerp(pos, p, k);
                rot = Quaternion.Slerp(rot, r, k);
            }

            pivot.localPosition = pos;
            pivot.localRotation = rot;
        }

        /// <summary>
        /// 振った割合 s のときの pivot のワールド行列（攻撃中のみ有効）
        /// </summary>
        public Matrix4x4 SwingPivotMatrix(float s)
        {
            Swing(player.Data.dir, s, out var p, out var r);
            var local = Matrix4x4.TRS(p, r, pivot.localScale);
            return pivot.parent != null ? pivot.parent.localToWorldMatrix * local : local;
        }

        /// <summary>
        /// 刃の根元と刃先のワールド座標。攻撃中は振り切った位置を返す（判定の瞬間はまだ表示が追いついていないことがあるため）
        /// </summary>
        public void GetBlade(out Vector3 root, out Vector3 tip)
        {
            if (Application.isPlaying && player.Data != null && player.Data.state == PlayerState.Attack)
            {
                var m = SwingPivotMatrix(1f);
                root = m.MultiplyPoint3x4(Vector3.zero);
                tip = root + m.MultiplyVector(Vector3.up).normalized * bladeLength;
                return;
            }
            root = pivot.position;
            tip = root + pivot.up * bladeLength;
        }

        // bladeLength の調整用。刃先まで線を引く
        private void OnDrawGizmosSelected()
        {
            if (pivot == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pivot.position, pivot.position + pivot.up * bladeLength);
        }

        void SetSwinging(bool value)
        {
            if (IsSwinging == value) return;
            IsSwinging = value;
            if (value) OnSwingStart?.Invoke();
            else OnSwingEnd?.Invoke();
        }

        // dir の方向が上になるように正面の軸で回す
        Quaternion Roll(int dir, float tilt = 0f) => Quaternion.Euler(0f, 0f, -(360f / directions * dir + tilt));

        // dir の反対側から角度 angle だけ正面側へ倒した刃の向き
        Quaternion Blade(int dir, float angle, float tilt = 0f) => Roll(dir, tilt) * Quaternion.Euler(-angle, 0f, 0f) * Quaternion.Euler(0f, 0f, 180f);

        void Swing(int dir, float t, out Vector3 p, out Quaternion r)
        {
            // 縦・横（90度の倍数）のときだけ傾ける
            float tilt = dir * 4 % directions == 0 ? straightTilt : 0f;
            r = Blade(dir, Mathf.Lerp(swingStart, swingEnd, t), tilt);
            p = gripPosition + Roll(dir, tilt) * Vector3.up * Mathf.Lerp(-handMove, handMove, t);
        }

        void Pose(PlayerState state, int dir, out Vector3 p, out Quaternion r)
        {
            int down = directions / 2;
            switch (state)
            {
                case PlayerState.Guard:
                    r = Roll(dir) * Quaternion.Euler(0f, -guardTilt, 0f) * Quaternion.Euler(0f, 0f, -90f);
                    p = gripPosition + Roll(dir) * new Vector3(guardOffset.x, guardOffset.y, 0f);
                    break;
                case PlayerState.Stun:
                    r = Blade(down, stunAngle);
                    p = gripPosition;
                    break;
                default:
                    r = Blade(down, idleAngle);
                    p = gripPosition;
                    break;
            }
        }
    }
}
