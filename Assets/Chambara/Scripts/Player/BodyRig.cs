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
        [SerializeField, Tooltip("アイドル中・勝利時に跳ねる高さ")] float hopHeight = 0.12f;
        [SerializeField] float hopSpeed     = 12f;

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

        [Header("試合の決着（勝ち・負けのポーズの数値）")]
        [SerializeField, Tooltip("未設定なら初期値で動く")] ResultPoseConfig poseConfig;

        PlayerBehaviour player;
        Sword sword;
        Transform floatPivot, chest, neck;
        public Transform Body => floatPivot;   // 体の中心。吹っ飛び中もここが一緒に動く
        Vector3 floatRest;   // Player のローカル座標
        Vector3 chestEuler, headEuler;
        Spring hair;
        readonly System.Collections.Generic.List<Spring> tails = new();

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

        // 決着演出
        enum ResultPose { None, Victory, Defeat }
        ResultPose result;
        float resultTime;            // 決着演出が始まってからの時間
        bool collapsed;              // 負け：崩れ落ち始めたか
        float sink, sinkVel;         // 負け：崩れ落ちて沈んだ深さ
        Transform gripR, gripL;      // 手（剣の握りの子）
        Vector3 handRPos, handLPos, swordPos, swordFromPos;
        Quaternion handRRot, handLRot, swordRot, swordFromRot;

        // パーツの大きさ（決着のポーズで手を置く位置に使う。ワールド座標）
        Vector3 headOffset;          // 首から頭の中心（Player の向き基準）
        float headRadius = 0.1f, headHeight = 0.2f, bodyRadius = 0.15f, bodyHeight = 0.3f;
        float groundOffset;          // Player の位置から足元（一番下のパーツの下端）までの高さ

        public Transform Head => neck;   // 頭（涙のエフェクトなどを付ける）
        public Vector3 HeadCenter => neck.position + neck.rotation * headOffset;

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
                gripR = AttachHand(Find(handRName), handROffset);
                gripL = AttachHand(Find(handLName), handLOffset);
            }

            floatRest = transform.InverseTransformPoint(floatPivot.position);

            // 決着のポーズ用に、パーツの大きさを覚えておく（モデルを差し替えても合うように）
            headOffset = Quaternion.Inverse(transform.rotation) * (headCenter - neckPos);
            if (head != null)
            {
                Bounds hb = PartBounds(head);
                headRadius = Mathf.Max(hb.extents.x, hb.extents.z, 0.05f);
                headHeight = Mathf.Max(hb.size.y, 0.05f);
            }
            if (body != null)
            {
                Bounds bb = PartBounds(body);
                bodyRadius = Mathf.Max(bb.extents.x, bb.extents.z, 0.05f);
                bodyHeight = Mathf.Max(bb.size.y, 0.05f);
            }
            Transform bottomPart = lower != null ? lower : body;
            groundOffset = bottomPart != null ? PartBounds(bottomPart).min.y - transform.position.y : 0f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            PlayerState state = player.Data != null ? player.Data.state : PlayerState.Idle;

            bool inResult = result != ResultPose.None;
            if (inResult) resultTime += dt;
            bool idle = state == PlayerState.Idle && !player.IsStepping && !player.Lost && !inResult;
            limp  = Mathf.Lerp(limp, player.Lost && !inResult ? 1f : 0f, Damp(limpSpeed));   // 決着演出では脱力の代わりに崩れ落ちる
            lunge = Mathf.Lerp(lunge, !inResult && (state == PlayerState.Attack || state == PlayerState.Thrust) ? attackLunge : 0f, Damp(bodySpeed));
            hop   = Mathf.Lerp(hop, player.Won || idle ? 1f : 0f, Damp(bodySpeed));
            float sob = UpdateDefeatBody(dt);   // 負け：崩れ落ちて、しゃくり上げる（0〜1）

            // 体全体：呼吸・跳ね・攻撃の踏み込み・脱力で沈む
            float y = Mathf.Sin(Time.time * breathSpeed) * breathHeight * (1f - limp)
                    + Mathf.Abs(Mathf.Sin(Time.time * hopSpeed)) * hopHeight * hop
                    + player.StepJump
                    - limpSink * limp
                    - sink + sob * poseConfig.sobHeight;
            Vector3 floatPos = transform.TransformPoint(floatRest + new Vector3(0f, y, lunge));
            floatPivot.SetPositionAndRotation(floatPos, transform.rotation);
            UpdateKnockback(dt, floatPos);

            // 胴：剣の向き（Player基準）に合わせてひねる・傾ける
            Vector3 b = sword != null && sword.Pivot != null ? floatPivot.InverseTransformDirection(sword.Pivot.up) : Vector3.up;
            Vector3 chestTarget = result switch
            {
                ResultPose.Victory => new Vector3(-poseConfig.victoryLeanBack, 0f, 0f),
                ResultPose.Defeat  => collapsed ? new Vector3(poseConfig.defeatBend + sob * poseConfig.sobAngle, 0f, 0f) : Vector3.zero,
                _ => new Vector3((restBladeY - b.y) * bendAngle + limpBend * limp, b.x * twistAngle, -b.x * tiltAngle),
            };
            chestEuler = Vector3.Lerp(chestEuler, chestTarget, Damp(bodySpeed));
            chest.rotation = floatPivot.rotation * Quaternion.Euler(chestEuler);

            // 頭：胴の回転を打ち消して相手を見る。怯みで上を向き、負けるとうなだれる
            Vector3 headTarget = -chestEuler * headStabilize;
            if (state == PlayerState.Stun && !inResult) headTarget.x -= stunHeadTilt;
            headTarget.x += limpHeadDrop * limp;
            if (result == ResultPose.Victory) headTarget = new Vector3(-poseConfig.victoryHeadUp, 0f, 0f);
            if (result == ResultPose.Defeat) headTarget = collapsed ? new Vector3(poseConfig.defeatHeadDrop, 0f, 0f) : Vector3.zero;
            headEuler = Vector3.Lerp(headEuler, headTarget, Damp(bodySpeed));
            neck.rotation = chest.rotation * Quaternion.Euler(headEuler);

            // 決着のポーズ：剣と手を動かす（Sword が置いた剣を上書きする）
            if (result == ResultPose.Victory) UpdateVictoryHands();
            if (result == ResultPose.Defeat && collapsed) UpdateDefeatHands();

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

        /// <summary>
        /// 試合の決着演出を始める。won: 勝ったなら片腕を突き上げて喜ぶ、負けたなら崩れ落ちて泣く
        /// 呼ぶ前に Player を演出の位置・向きに置いておく
        /// </summary>
        public void PlayResult(bool won)
        {
            if (poseConfig == null) poseConfig = ScriptableObject.CreateInstance<ResultPoseConfig>();   // 未設定なら初期値
            result = won ? ResultPose.Victory : ResultPose.Defeat;
            resultTime = 0f;
            collapsed = false;
            knockedBack = false;   // 吹っ飛んでいたら戻す
            limp = 0f;
            sink = sinkVel = 0f;
            chestEuler = headEuler = Vector3.zero;   // 立った状態から始める（負けた方はここから崩れ落ちる）

            // 今の剣と手の位置から動き始める
            if (sword != null && sword.Pivot != null)
            {
                swordPos = sword.Pivot.position;
                swordRot = sword.Pivot.rotation;
            }
            if (gripR != null) { handRPos = gripR.position; handRRot = gripR.rotation; }
            if (gripL != null) { handLPos = gripL.position; handLRot = gripL.rotation; }
        }

        // 負け：defeatDelay 待ってから、バネで沈んで（少し弾んで）崩れ落ちる。しゃくり上げの強さ（0〜1）を返す
        float UpdateDefeatBody(float dt)
        {
            if (result != ResultPose.Defeat) return 0f;
            if (!collapsed)
            {
                if (resultTime < poseConfig.defeatDelay) return 0f;
                collapsed = true;
                // 剣を落とし始める位置と、顔を覆い始める手の位置
                if (sword != null && sword.Pivot != null)
                {
                    swordFromPos = sword.Pivot.position;
                    swordFromRot = sword.Pivot.rotation;
                }
                if (gripR != null) { handRPos = gripR.position; handRRot = gripR.rotation; }
                if (gripL != null) { handLPos = gripL.position; handLRot = gripL.rotation; }
            }

            sinkVel += ((poseConfig.defeatSink - sink) * poseConfig.defeatSinkStiffness - sinkVel * poseConfig.defeatSinkDamping) * dt;
            sink += sinkVel * dt;

            // 崩れ落ちてから少しして、ひっく、ひっくと肩を震わせる
            float t = resultTime - poseConfig.defeatDelay;
            float fadeIn = Mathf.Clamp01((t - poseConfig.sobStartDelay) / 0.5f);
            return Mathf.Pow(Mathf.Abs(Mathf.Sin(t * poseConfig.sobSpeed)), 3f) * fadeIn;
        }

        // 勝ち：剣を握った右手を頭の上へ突き上げ（ときどき突き上げ直す）、左手は腰に当てる
        void UpdateVictoryHands()
        {
            float k = Damp(poseConfig.handSpeed);
            Vector3 up = floatPivot.up, right = floatPivot.right;

            if (sword != null && sword.Pivot != null)
            {
                float pump = Mathf.Abs(Mathf.Sin(resultTime * poseConfig.victoryPumpSpeed)) * poseConfig.victoryPumpHeight;
                Vector3 target = neck.position + right * (bodyRadius + poseConfig.victorySpread) + up * (headHeight + poseConfig.victoryRaise + pump);
                Quaternion targetRot = floatPivot.rotation * Quaternion.Euler(0f, 0f, -poseConfig.victoryBladeTilt);   // 刃を上に、少し外へ傾ける
                swordPos = Vector3.Lerp(swordPos, target, k);
                swordRot = Quaternion.Slerp(swordRot, targetRot, k);
                sword.Pivot.SetPositionAndRotation(swordPos, swordRot);
            }

            Vector3 hip = chest.position - right * (bodyRadius + 0.03f) + up * (bodyHeight * 0.2f);
            MoveHand(gripL, ref handLPos, ref handLRot, hip, chest.rotation, k);
        }

        // 負け：剣を足元に落とし、両手で顔を覆う
        void UpdateDefeatHands()
        {
            if (sword != null && sword.Pivot != null)
            {
                float p = poseConfig.swordDropTime > 0f ? Mathf.Clamp01((resultTime - poseConfig.defeatDelay) / poseConfig.swordDropTime) : 1f;
                p *= p;   // 加速しながら落ちる
                Vector3 to = transform.position + transform.rotation * new Vector3(poseConfig.droppedSwordPos.x, groundOffset + poseConfig.droppedSwordPos.y, poseConfig.droppedSwordPos.z);
                Quaternion toRot = transform.rotation * Quaternion.Euler(0f, poseConfig.droppedSwordYaw, 0f) * Quaternion.Euler(0f, 0f, -90f);   // 刃を寝かせる
                sword.Pivot.SetPositionAndRotation(Vector3.Lerp(swordFromPos, to, p), Quaternion.Slerp(swordFromRot, toRot, p));
            }

            float k = Damp(poseConfig.handSpeed);
            Vector3 face = HeadCenter + neck.forward * (headRadius + poseConfig.defeatHandGap) - neck.up * (headRadius * 0.1f);
            Vector3 side = neck.right * (headRadius * 0.45f);
            MoveHand(gripR, ref handRPos, ref handRRot, face + side, neck.rotation, k);
            MoveHand(gripL, ref handLPos, ref handLRot, face - side, neck.rotation, k);
        }

        // 手を剣から離して target へ動かす（握りの子のまま、ワールドの位置・向きを上書きする）
        static void MoveHand(Transform grip, ref Vector3 pos, ref Quaternion rot, Vector3 target, Quaternion targetRot, float k)
        {
            if (grip == null) return;
            pos = Vector3.Lerp(pos, target, k);
            rot = Quaternion.Slerp(rot, targetRot, k);
            grip.SetPositionAndRotation(pos, rot);
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
        Transform AttachHand(Transform hand, float offset)
        {
            if (hand == null) return null;
            Transform grip = Anchor(hand.name + "_Grip", sword.Pivot, PartBounds(hand).center, hand);
            grip.localPosition = Vector3.up * (offset / sword.Pivot.lossyScale.y);
            return grip;
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
