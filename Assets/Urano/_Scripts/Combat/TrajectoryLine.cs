using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 砲弾の放物線の予測線。CannonShooterが実際に飛ばす初速と同じ重力で点を刻むので、予測線どおりに飛ぶ。
    /// CannonStationが毎フレーム Show / Hide を呼ぶ。
    /// </summary>
    public class TrajectoryLine : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("線のマテリアル（任意）。未設定ならSprites/Defaultを自動生成。VFX/Materials/Trajectory.mat など")]
        Material material;
        [SerializeField]
        Color color = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField]
        [Tooltip("線の太さ(m)")]
        float width = 0.2f;
        [SerializeField]
        [Tooltip("点を刻む時間間隔(秒)。小さいほど滑らか")]
        float timeStep = 0.05f;
        [SerializeField]
        [Tooltip("点の最大数（これを超える長さは打ち切る）")]
        int maxPoints = 120;
        [SerializeField]
        [Tooltip("クールタイム中の線の濃さ(0〜1)")]
        [Range(0f, 1f)] float cooldownAlpha = 0.3f;

        // 全ラインで共有するマテリアル（Sprites/Defaultは頂点カラーで着色でき、どのレンダーパイプラインでも使える）
        static Material sharedMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => sharedMaterial = null;

        LineRenderer line;
        Vector3[] points;

        void Awake()
        {
            // 他のRendererと干渉しないよう子オブジェクトに作る
            var go = new GameObject("TrajectoryLine");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = material != null ? material : SharedMaterial;
            line.enabled = false;
            points = new Vector3[maxPoints];
        }

        void OnDisable()
        {
            if (line != null) line.enabled = false;
        }

        public void SetColor(Color c) => color = new Color(c.r, c.g, c.b, color.a);

        public void Hide()
        {
            if (line.enabled) line.enabled = false;
        }

        /// <summary>origin から initialVelocity で撃った弾道を、endHeight の高さに落ちるまで描く</summary>
        public void Show(Vector3 origin, Vector3 initialVelocity, float endHeight, bool ready)
        {
            int count = 0;
            Vector3 p = origin;
            Vector3 v = initialVelocity;
            points[count++] = p;

            while (count < maxPoints)
            {
                v += Physics.gravity * timeStep;
                Vector3 next = p + v * timeStep;

                // 下降中に着弾高さを割ったら、その交点で打ち切る
                if (v.y < 0f && next.y <= endHeight)
                {
                    float t = Mathf.Clamp01((p.y - endHeight) / Mathf.Max(0.0001f, p.y - next.y));
                    points[count++] = Vector3.Lerp(p, next, t);
                    break;
                }

                p = next;
                points[count++] = p;
            }

            Color c = color;
            if (!ready) c.a *= cooldownAlpha;
            line.startColor = c;
            line.endColor = c;
            line.startWidth = width;
            line.endWidth = width;
            line.positionCount = count;
            line.SetPositions(points);
            line.enabled = true;
        }

        static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null) sharedMaterial = new Material(Shader.Find("Sprites/Default")) { renderQueue = 3100 }; // 水面(2900)より後に描く
                return sharedMaterial;
            }
        }
    }
}
