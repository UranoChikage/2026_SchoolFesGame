using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// ボーンの無いパーツ分割モデルを、剣の動きと状態に合わせて動かす
    /// 起動時に関節の位置へ空のオブジェクトを作ってパーツの親にし、毎フレームその回転・位置を上書きする
    /// 手は剣の握り（Sword.Pivot）の子にするので、常に剣を握ったまま動く
    /// パーツ名が同じモデルなら、別キャラクターに差し替えてもそのまま動く
    /// </summary>
    [DefaultExecutionOrder(100)]   // Sword が剣を動かした後に体を合わせる
    public class BodyRig : MonoBehaviour
    {
        [Header("パーツ名（表示中の子から名前で探す）")]
        [SerializeField] string headName        = "Head";
        [SerializeField] string hairName        = "Hair";
        [SerializeField] string bodyName        = "Body.1";
        [SerializeField, Tooltip("下半身")] string lowerName = "Body.2";
        [SerializeField, Tooltip("胴と一緒に動く。無いものは飛ばす")] string[] accessoryNames = { "Accessories", "Accessories.2" };
        [SerializeField] string handRName       = "hand.R";
        [SerializeField] string handLName       = "hand.L";

        [Header("手（握りからの距離。+で鍔側、-で柄頭側。ワールド座標）")]
        [SerializeField] float handROffset = 0.05f;
        [SerializeField] float handLOffset = -0.05f;

        [Header("胴（剣の向きに合わせる）")]
        [SerializeField, Tooltip("剣が横にあるとき、そちらへひねる角度")] float twistAngle = 30f;
        [SerializeField, Tooltip("剣が横にあるとき、そちらへ傾ける角度")] float tiltAngle = 10f;
        [SerializeField, Tooltip("剣が下がったときに前かがみになる角度")] float bendAngle = 15f;
        [SerializeField, Tooltip("前かがみ0になる刃の上向き成分（通常の構えの値）")] float restBladeY = 0.7f;
        [SerializeField, Tooltip("攻撃中に体が前に出る距離")] float attackLunge = 0.15f;
        [SerializeField] float bodySpeed   = 12f;

        [Header("頭")]
        [SerializeField, Range(0f, 1f), Tooltip("胴の回転をどれだけ打ち消して相手を見続けるか")] float headStabilize = 0.7f;
        [SerializeField, Tooltip("怯み中に上を向く角度")] float stunHeadTilt = 20f;

        [Header("髪（頭の回転にバネで遅れてついていく）")]
        [SerializeField] float hairStiffness = 150f;
        [SerializeField] float hairDamping   = 10f;
        [SerializeField] float hairMaxAngle  = 25f;

        [Header("ツインテールなど（根元を軸に、髪より大きく揺れる。無ければ飛ばす）")]
        [SerializeField] string[] tailNames  = { "Tail.L", "Tail.R" };
        [SerializeField] float tailStiffness = 80f;
        [SerializeField] float tailDamping   = 6f;
        [SerializeField] float tailMaxAngle  = 40f;

        [Header("浮遊")]
        [SerializeField] float breathHeight = 0.02f;
        [SerializeField] float breathSpeed  = 3f;
        [SerializeField, Tooltip("移動中・勝利時に跳ねる高さ")] float hopHeight = 0.12f;
        [SerializeField] float hopSpeed     = 12f;
        [SerializeField, Tooltip("この速さ（m/s）以上で動いていたら跳ねる")] float hopMinSpeed = 0.05f;

        [Header("負け（力が抜ける）")]
        [SerializeField] float limpBend     = 30f;
        [SerializeField] float limpHeadDrop = 25f;
        [SerializeField] float limpSink     = 0.15f;
        [SerializeField] float limpSpeed    = 4f;

        [Header("やられ（後ろに吹っ飛ぶ。着地せずそのまま落ちていく）")]
        [SerializeField, Tooltip("後ろへ飛ぶ初速（m/s）")] float knockbackSpeed = 4f;
        [SerializeField, Tooltip("上へ飛ぶ初速（m/s）")] float knockbackUp = 5f;
        [SerializeField] float knockbackGravity  = 10f;
        [SerializeField, Tooltip("後ろ回りの回転速度（度/秒）")] float knockbackFlipSpeed = 720f;
        [SerializeField, Tooltip("体の縦軸まわりのきりもみ回転速度（度/秒）")] float knockbackTwistSpeed = 1080f;

        PlayerBehaviour player;
        Sword sword;
        Transform floatPivot, chest, neck;
        Vector3 floatRest;   // Player のローカル座標
        Vector3 chestEuler, headEuler;
        Spring hair;
        readonly System.Collections.Generic.List<Spring> tails = new();
        Vector3 lastPos;

        // 首の回転にバネで遅れてついていく部位
        class Spring
        {
            public Transform pivot;
            public Quaternion rot;
            public Vector3 vel;
            public bool ready;
        }
        float lunge, hop, limp;
        bool knockedBack;
        Vector3 knockbackOffset, knockbackVel;   // Player のローカル座標
        float knockbackFlip, knockbackTwist;

        private void Awake()
        {

        }

        public void Init(PlayerBehaviour p)
        {
            player = p;
            sword = GetComponentInChildren<Sword>();

            // 無いパーツは飛ばす。関節の位置は、あるパーツから順に決める
            Transform head = Find(headName), body = Find(bodyName), lower = Find(lowerName), hairPart = Find(hairName);
            Transform root = lower != null ? lower.parent : body != null ? body.parent : head != null ? head.parent : hairPart != null ? hairPart.parent : transform;

            Vector3 floatPos = lower != null ? PartBounds(lower).center
                             : body != null ? Bottom(PartBounds(body))
                             : head != null ? Bottom(PartBounds(head))
                             : transform.position;
            Vector3 chestPos = body != null ? Bottom(PartBounds(body)) : floatPos;
            Vector3 neckPos = head != null ? Bottom(PartBounds(head))
                             : body != null ? Top(PartBounds(body))
                             : chestPos;

            floatPivot = Anchor("Float", root, floatPos, lower);
            chest = Anchor("Chest", floatPivot, chestPos, body);
            foreach (var accessoryName in accessoryNames)
            {
                Transform accessory = Find(accessoryName);
                if (accessory != null) accessory.SetParent(chest, true);
            }
            neck = Anchor("Neck", chest, neckPos, head);
            Vector3 headCenter = head != null ? PartBounds(head).center : neckPos;
            if (hairPart != null) hair = new Spring { pivot = Anchor("HairPivot", neck, head != null ? headCenter : PartBounds(hairPart).center, hairPart) };

            // 根元＝パーツの中で頭の中心に一番近い点（垂れていても横に出ていても合う）
            foreach (var tailName in tailNames)
            {
                Transform tail = Find(tailName);
                if (tail == null) continue;
                tails.Add(new Spring { pivot = Anchor(tailName + "_Root", neck, PartBounds(tail).ClosestPoint(headCenter), tail) });
            }

            if (sword != null && sword.Pivot != null)
            {
                AttachHand(Find(handRName), handROffset);
                AttachHand(Find(handLName), handLOffset);
            }

            floatRest = transform.InverseTransformPoint(floatPivot.position);
            lastPos = transform.position;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            PlayerState state = player.Data != null ? player.Data.state : PlayerState.Idle;

            float speed = Vector3.ProjectOnPlane(transform.position - lastPos, Vector3.up).magnitude / dt;
            lastPos = transform.position;
            limp  = Mathf.Lerp(limp, player.Lost ? 1f : 0f, Damp(limpSpeed));
            lunge = Mathf.Lerp(lunge, state == PlayerState.Attack ? attackLunge : 0f, Damp(bodySpeed));
            hop   = Mathf.Lerp(hop, player.Won || (speed > hopMinSpeed && !player.Lost) ? 1f : 0f, Damp(bodySpeed));

            // 体全体：呼吸・跳ね・攻撃の踏み込み・脱力で沈む
            float y = Mathf.Sin(Time.time * breathSpeed) * breathHeight * (1f - limp)
                    + Mathf.Abs(Mathf.Sin(Time.time * hopSpeed)) * hopHeight * hop
                    - limpSink * limp;
            Vector3 floatPos = transform.TransformPoint(floatRest + new Vector3(0f, y, lunge));
            floatPivot.SetPositionAndRotation(floatPos, transform.rotation);
            UpdateKnockback(dt, floatPos);

            // 胴：剣の向き（Player基準）に合わせてひねる・傾ける
            Vector3 b = sword != null && sword.Pivot != null ? floatPivot.InverseTransformDirection(sword.Pivot.up) : Vector3.up;
            Vector3 chestTarget = new Vector3((restBladeY - b.y) * bendAngle + limpBend * limp, b.x * twistAngle, -b.x * tiltAngle);
            chestEuler = Vector3.Lerp(chestEuler, chestTarget, Damp(bodySpeed));
            chest.rotation = floatPivot.rotation * Quaternion.Euler(chestEuler);

            // 頭：胴の回転を打ち消して相手を見る。怯みで上を向き、負けるとうなだれる
            Vector3 headTarget = -chestEuler * headStabilize;
            if (state == PlayerState.Stun) headTarget.x -= stunHeadTilt;
            headTarget.x += limpHeadDrop * limp;
            headEuler = Vector3.Lerp(headEuler, headTarget, Damp(bodySpeed));
            neck.rotation = chest.rotation * Quaternion.Euler(headEuler);

            if (hair != null) UpdateSpring(hair, hairStiffness, hairDamping, hairMaxAngle, dt);
            foreach (var tail in tails) UpdateSpring(tail, tailStiffness, tailDamping, tailMaxAngle, dt);
        }

        /// <summary>
        /// 倒されたときの演出：後ろへ放物線を描いて吹っ飛び、後ろ回りときりもみをしながらそのまま落ちていく
        /// </summary>
        public void PlayKnockback()
        {
            knockedBack = true;
            knockbackOffset = Vector3.zero;
            knockbackVel = new Vector3(0f, knockbackUp, -knockbackSpeed);
            knockbackFlip = knockbackTwist = 0f;
        }

        // floatPos: 吹っ飛びが無いときの体の位置。体と剣（手）をまとめて、そこを中心に動かす・回す
        void UpdateKnockback(float dt, Vector3 floatPos)
        {
            if (!knockedBack) return;

            knockbackVel.y -= knockbackGravity * dt;
            knockbackOffset += knockbackVel * dt;
            knockbackFlip  = (knockbackFlip  + knockbackFlipSpeed  * dt) % 360f;
            knockbackTwist = (knockbackTwist + knockbackTwistSpeed * dt) % 360f;

            Quaternion spin = transform.rotation * Quaternion.Euler(-knockbackFlip, 0f, 0f) * Quaternion.Euler(0f, knockbackTwist, 0f)
                            * Quaternion.Inverse(transform.rotation);   // ワールドでの回転量
            Vector3 center = floatPos + transform.TransformVector(knockbackOffset);
            floatPivot.SetPositionAndRotation(center, spin * transform.rotation);

            // 剣は Sword が毎フレーム Player 基準で置き直すので、ここで体と同じだけ動かす
            if (sword != null && sword.Pivot != null)
            {
                Transform pivot = sword.Pivot;
                pivot.SetPositionAndRotation(center + spin * (pivot.position - floatPos), spin * pivot.rotation);
            }
        }

        void UpdateSpring(Spring s, float stiffness, float damping, float maxAngle, float dt)
        {
            Quaternion target = neck.rotation;
            if (!s.ready)
            {
                // Init で Player の向きが決まった後から始める
                s.rot = target;
                s.ready = true;
            }

            (target * Quaternion.Inverse(s.rot)).ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 diff = Mathf.Abs(angle) > 0.01f ? axis * (angle * Mathf.Deg2Rad) : Vector3.zero;
            s.vel += (diff * stiffness - s.vel * damping) * dt;
            if (s.vel.sqrMagnitude > 1e-8f)
                s.rot = Quaternion.AngleAxis(s.vel.magnitude * Mathf.Rad2Deg * dt, s.vel.normalized) * s.rot;
            s.rot = Quaternion.RotateTowards(target, s.rot, maxAngle);
            s.pivot.rotation = s.rot;
        }

        // 握りの子にして、刃の方向に offset だけずらす
        void AttachHand(Transform hand, float offset)
        {
            if (hand == null) return;
            Transform grip = Anchor(hand.name + "_Grip", sword.Pivot, PartBounds(hand).center, hand);
            grip.localPosition = Vector3.up * (offset / sword.Pivot.lossyScale.y);
        }

        // pos に空のオブジェクトを作り、見た目を変えずに parts の親にする
        Transform Anchor(string anchorName, Transform parent, Vector3 pos, params Transform[] parts)
        {
            var anchor = new GameObject(anchorName).transform;
            anchor.SetPositionAndRotation(pos, transform.rotation);
            anchor.SetParent(parent, true);
            foreach (var p in parts)
            {
                if (p != null) p.SetParent(anchor, true);
            }
            return anchor;
        }

        Transform Find(string partName)
        {
            foreach (var t in GetComponentsInChildren<Transform>())   // 非表示にしたモデルは探さない
            {
                if (t.name == partName) return t;
            }
            return null;
        }

        static Bounds PartBounds(Transform t) => t.TryGetComponent(out Renderer r) ? r.bounds : new Bounds(t.position, Vector3.zero);

        static Vector3 Bottom(Bounds b) => new Vector3(b.center.x, b.min.y, b.center.z);
        static Vector3 Top(Bounds b)    => new Vector3(b.center.x, b.max.y, b.center.z);

        static float Damp(float speed) => 1f - Mathf.Exp(-speed * Time.deltaTime);
    }
}
