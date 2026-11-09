using System.Collections.Generic;
using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 手前の物（自船・敵・波など）に隠れたとき「だけ」、付けた物体の外周に輪郭線を出す。
    /// Item・お宝・プレイヤー船のPrefabのルートに付ける。元のマテリアルは触らず、配下のメッシュごとに
    /// 2枚の殻（Mark=ステンシルを書く / Ring=隠れた部分の輪郭を描く）を子として足す。
    /// MeshRenderer / SkinnedMeshRenderer 対応（パーティクルは対象外）。見えている間は何も描かない。
    /// </summary>
    public class OutlineHull : MonoBehaviour
    {
        [SerializeField] Color color = Color.white;
        [SerializeField]
        [Min(0f)]
        [Tooltip("線の太さ(ピクセル)。画面上でほぼ一定")]
        float width = 4f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("手前の物がこの距離(m)以上かぶさったときだけ輪郭を出す。大きいほど出にくい。見えているのに出るときは上げる（海面に浮くお宝は1前後）")]
        float hideMargin = 0.5f;

        Material markMat;
        Material ringMat;
        readonly List<GameObject> shells = new List<GameObject>();

        void Awake()
        {
            Shader mark = Shader.Find("Urano/OutlineHidden");
            Shader ring = Shader.Find("Urano/OutlineHiddenRing");
            if (mark == null || ring == null)
            {
                Debug.LogWarning("[OutlineHull] Urano/OutlineHidden と Urano/OutlineHiddenRing のシェーダが見つかりません", this);
                return;
            }
            markMat = new Material(mark);
            ringMat = new Material(ring);
            ringMat.SetColor("_Color", color);
            ringMat.SetFloat("_Width", width);
            ringMat.SetFloat("_HideMargin", hideMargin);

            // 先に集めてから足す（足した殻を再度対象にしない）
            MeshRenderer[] meshes = GetComponentsInChildren<MeshRenderer>();
            SkinnedMeshRenderer[] skins = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (MeshRenderer r in meshes)
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                AddShell(r.transform, mf.sharedMesh, null, markMat);
                AddShell(r.transform, mf.sharedMesh, null, ringMat);
            }
            foreach (SkinnedMeshRenderer r in skins)
            {
                if (r.sharedMesh == null) continue;
                AddShell(r.transform, r.sharedMesh, r, markMat);
                AddShell(r.transform, r.sharedMesh, r, ringMat);
            }
        }

        void OnDestroy()
        {
            if (markMat != null) Destroy(markMat);
            if (ringMat != null) Destroy(ringMat);
        }

        void AddShell(Transform parent, Mesh mesh, SkinnedMeshRenderer skin, Material mat)
        {
            var go = new GameObject("OutlineShell") { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            shells.Add(go);

            var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;

            Renderer r;
            if (skin != null)
            {
                var s = go.AddComponent<SkinnedMeshRenderer>();
                s.sharedMesh = mesh;
                s.bones = skin.bones;
                s.rootBone = skin.rootBone;
                s.localBounds = skin.localBounds;
                r = s;
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                r = go.AddComponent<MeshRenderer>();
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
