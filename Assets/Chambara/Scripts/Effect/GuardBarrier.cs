using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// ガード中、構えている方向にバリア（GuardBarrier マテリアル）を出す
    /// Player プレハブ（PlayerBehaviour と同じオブジェクト）に付ける
    /// barrier が未設定なら barrierMaterial を貼った Quad を自動で作る
    /// 方向は Sword と同じく「自分の後ろから見て」上=0から時計回り
    /// </summary>
    public class GuardBarrier : MonoBehaviour
    {
        [SerializeField] PlayerBehaviour player;            // 未設定なら同じオブジェクトから探す
        [SerializeField] Renderer barrier;                  // バリアの見た目（Quad など）。未設定なら自動で作る
        [SerializeField] Material barrierMaterial;          // 自動で作るときに貼るマテリアル（GuardBarrier.mat）
        [SerializeField, Range(1, 8)] int directions = 8;   // ChambaraRule.directions と合わせる

        [Header("置く位置（Player のローカル座標）")]
        [SerializeField] Vector3 center = new Vector3(0f, 1.0f, 0.6f);   // バリアの基準位置（体の前）
        [SerializeField] float offset = 0.35f;                            // 構える方向へずらす距離
        [SerializeField] Vector2 size = new Vector2(1.0f, 0.6f);          // x:方向に垂直な幅 y:方向に沿った高さ
        [SerializeField] float tilt = 20f;                                // 外側の端を前（相手側）に倒す角度。マイナスで手前に

        [Header("出し方")]
        [SerializeField] float fadeInSpeed  = 12f;   // 出るときの速さ
        [SerializeField] float fadeOutSpeed = 8f;    // 消えるときの速さ
        [SerializeField] float popScale = 0.6f;      // 出始めの大きさ（1で等倍から出る）
        [SerializeField] float turnSpeed = 20f;      // ガード方向を変えたときに回り込む速さ

        [Header("ガード成立時のフラッシュ")]
        [SerializeField] float hitDecay = 4f;        // フラッシュが消える速さ（ゲーム内時間。スロー中はゆっくり）
        [SerializeField] float hitPunch = 0.25f;     // フラッシュの瞬間に大きくなる割合

        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int HitId   = Shader.PropertyToID("_Hit");

        MaterialPropertyBlock mpb;
        ChambaraManager manager;
        float alpha;          // 0:消えている 1:出ている
        float hit;            // ガード成立フラッシュ 1→0
        float angle;          // 今のバリアの向き（度）
        bool hasAngle;

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerBehaviour>();
            if (barrier == null) CreateBarrier();
            mpb = new MaterialPropertyBlock();
            Apply();
        }

        private void Start()
        {
            manager = ChambaraManager.Instance;
            if (manager != null) manager.OnGuarded += OnGuarded;
        }

        private void OnDestroy()
        {
            if (manager != null) manager.OnGuarded -= OnGuarded;
        }

        void CreateBarrier()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GuardBarrier";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            barrier = go.GetComponent<Renderer>();
            barrier.sharedMaterial = barrierMaterial;
            barrier.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            barrier.receiveShadows = false;
        }

        // ガードされた瞬間（attacker は攻撃した側）。守った側が自分ならフラッシュ
        void OnGuarded(int attacker)
        {
            var players = manager.Players;
            if (players == null || players[1 - attacker] != player) return;
            hit = 1f;
            alpha = 1f;
        }

        private void LateUpdate()
        {
            if (player == null || barrier == null || player.Data == null) return;

            bool guarding = player.Data.state == PlayerState.Guard && player.Data.dir >= 0
                            && !player.Lost && !player.InResult;

            if (guarding)
            {
                float target = 360f / directions * player.Data.dir;
                angle = hasAngle ? Mathf.LerpAngle(angle, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime)) : target;
                hasAngle = true;
            }
            else if (alpha <= 0.001f)
            {
                hasAngle = false;   // 完全に消えたら、次は回り込まずにその方向へ出す
            }

            float speed = guarding ? fadeInSpeed : fadeOutSpeed;
            alpha = Mathf.MoveTowards(alpha, guarding ? 1f : 0f, speed * Time.deltaTime);
            hit = Mathf.MoveTowards(hit, 0f, hitDecay * Time.deltaTime);

            Apply();
        }

        void Apply()
        {
            bool visible = alpha > 0.001f;
            barrier.enabled = visible;
            if (!visible) return;

            Transform t = barrier.transform;
            GetLocalPose(angle, out var pos, out var rot);
            t.localPosition = pos;
            t.localRotation = rot;

            float ease = 1f - (1f - alpha) * (1f - alpha);   // 出始めに速く広がる
            float scale = Mathf.Lerp(popScale, 1f, ease) * (1f + hitPunch * hit);
            t.localScale = new Vector3(size.x * scale, size.y * scale, 1f);

            barrier.GetPropertyBlock(mpb);
            mpb.SetFloat(AlphaId, alpha);
            mpb.SetFloat(HitId, hit);
            barrier.SetPropertyBlock(mpb);
        }

        // 向き angle（度）のときのバリアの位置・回転（Player のローカル座標）
        // 方向 dir へ回す（Sword.Roll と同じ向き）→ 外側の端を前へ倒す
        void GetLocalPose(float angle, out Vector3 pos, out Quaternion rot)
        {
            Quaternion roll = Quaternion.Euler(0f, 0f, -angle);
            pos = center + roll * Vector3.up * offset;
            rot = roll * Quaternion.Euler(tilt, 0f, 0f);   // +tilt で外側の端が +Z（相手側）へ
        }

        // 選択中、ガードできる4方向（上右下左）にバリアが出る場所を表示する
        private void OnDrawGizmosSelected()
        {
            Matrix4x4 baseMatrix = transform.localToWorldMatrix;
            int step = Mathf.Max(1, directions / 4);
            Color[] colors = { Color.cyan, Color.green, Color.yellow, Color.magenta };   // 上 右 下 左

            // 基準位置
            Gizmos.matrix = baseMatrix;
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(center, 0.03f);

            for (int i = 0; i < 4; i++)
            {
                int dir = i * step;
                float a = 360f / directions * dir;
                GetLocalPose(a, out var pos, out var rot);

                // 基準位置 → バリア中心
                Gizmos.matrix = baseMatrix;
                Gizmos.color = colors[i];
                Gizmos.DrawLine(center, pos);

                // バリアの板（Quad と同じ 1x1 を size で拡大）
                Gizmos.matrix = baseMatrix * Matrix4x4.TRS(pos, rot, new Vector3(size.x, size.y, 1f));
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 1f, 0f));
                Gizmos.color = new Color(colors[i].r, colors[i].g, colors[i].b, 0.15f);
                Gizmos.DrawCube(Vector3.zero, new Vector3(1f, 1f, 0f));
                // 外側（構える方向）を指す矢印
                Gizmos.color = colors[i];
                Gizmos.DrawLine(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 0.4f, 0f));
                Gizmos.DrawLine(new Vector3(-0.15f, 0.2f, 0f), new Vector3(0f, 0.4f, 0f));
                Gizmos.DrawLine(new Vector3(0.15f, 0.2f, 0f), new Vector3(0f, 0.4f, 0f));
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
