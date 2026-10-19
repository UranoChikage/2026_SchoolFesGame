using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 帆のメッシュに合わせて、SailSway シェーダの「揺れの中心」「固定になる距離」「揺れる向き」を設定する。
    /// 帆ごとにメッシュの大きさが違っても、マテリアルを共有したまま使える。
    /// 選択中はシーンビューに、動かす中心(球)・固定になる距離(ワイヤー球)・揺れる向き(矢印)を表示する。
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class SailSwayFitter : MonoBehaviour
    {
        static readonly int CenterId = Shader.PropertyToID("_Center");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int SwayDirId = Shader.PropertyToID("_SwayDir");
        static readonly int BillowId = Shader.PropertyToID("_Billow");
        static readonly int SwayAmpId = Shader.PropertyToID("_SwayAmp");
        static readonly int SwaySpeedId = Shader.PropertyToID("_SwaySpeed");

        [SerializeField]
        [Tooltip("ONなら、メッシュからの自動計算をやめ、帆マテリアルの「中心・固定になる距離・揺れる向き」の値をそのまま使う。"
               + "共有マテリアルで値を決めてあるときに、シルエット描画へ同じ値を渡すために使う")]
        bool useMaterialValues = false;
        [SerializeField]
        [Tooltip("揺れの中心を、メッシュ中心からずらす量(オブジェクト空間)")]
        Vector3 centerOffset;
        [SerializeField]
        [Tooltip("固定になる距離(オブジェクト空間)を直接指定する。0なら下の倍率でメッシュの大きさから自動計算")]
        float radiusOverride = 0f;
        [SerializeField]
        [Tooltip("固定になる距離の倍率。1でメッシュの対角線の半分。小さいほど中心付近だけが動く")]
        float radiusScale = 1f;
        [SerializeField]
        [Tooltip("揺れる向き(オブジェクト空間)。帆の面に垂直な向きにする。赤い線で確認できる")]
        Vector3 swayDirection = Vector3.forward;

        void Awake()
        {
            Apply();
        }

        void OnValidate()
        {
            if (Application.isPlaying) Apply();
        }

        bool TryGetShape(out Vector3 center, out float radius)
        {
            center = Vector3.zero;
            radius = 0f;

            Material mat = GetComponent<MeshRenderer>().sharedMaterial;
            if (useMaterialValues && mat != null && mat.HasProperty(CenterId))
            {
                center = mat.GetVector(CenterId);
                radius = Mathf.Max(mat.GetFloat(RadiusId), 0.0001f);
                return true;
            }

            var filter = GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;

            var bounds = filter.sharedMesh.bounds;
            center = bounds.center + centerOffset;
            radius = radiusOverride > 0f ? radiusOverride : bounds.extents.magnitude * radiusScale;
            radius = Mathf.Max(radius, 0.0001f);
            return true;
        }

        Vector3 GetSwayDirection()
        {
            Material mat = GetComponent<MeshRenderer>().sharedMaterial;
            if (useMaterialValues && mat != null && mat.HasProperty(SwayDirId))
            {
                return ((Vector3)mat.GetVector(SwayDirId)).normalized;
            }
            return swayDirection.normalized;
        }

        void Apply()
        {
            if (!TryGetShape(out var center, out var radius)) return;

            var meshRenderer = GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);
            block.SetVector(CenterId, center);
            block.SetFloat(RadiusId, radius);
            block.SetVector(SwayDirId, GetSwayDirection());

            // EnemySilhouette(シルエット描画)にも同じ揺れを入れるため、帆マテリアルの値も渡す
            Material mat = meshRenderer.sharedMaterial;
            if (mat != null && mat.HasProperty(BillowId))
            {
                block.SetFloat(BillowId, mat.GetFloat(BillowId));
                block.SetFloat(SwayAmpId, mat.GetFloat(SwayAmpId));
                block.SetFloat(SwaySpeedId, mat.GetFloat(SwaySpeedId));
            }
            meshRenderer.SetPropertyBlock(block);
        }

        void OnDrawGizmosSelected()
        {
            if (!TryGetShape(out var center, out var radius)) return;

            var prev = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(center, radius * 0.04f);
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(center, radius);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(center, center + GetSwayDirection() * radius * 0.5f);

            Gizmos.matrix = prev;
        }
    }
}
