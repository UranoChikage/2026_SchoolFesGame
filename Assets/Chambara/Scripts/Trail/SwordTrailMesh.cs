using System.Collections.Generic;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// TrailRenderer の代わりに、刃の root〜tip が通った面を帯状のメッシュにして描く
    /// 点ごとに打った時刻を持たせて、時間で少しずつ消す（TrailRenderer は1フレームで足した点が同時に消えるので尾がガタつく）
    /// UV.x = 経過時間 / lifetime（0=剣側 → 1=尾）、UV.y = 0:root → 1:tip。点の数や長さで UV が伸び縮みしない
    /// 頂点カラーの a = 刃先の速さ / fullSpeed（0〜1）。遅いところを薄くするのに使う
    /// MeshFilter と MeshRenderer（Material は SwordTrail.mat）と同じ GameObject に付ける
    /// shape = Tube のときは root〜tip を直径にした円の断面で、軌跡に沿ったパイプにする（UV.y は円周 0→1）
    /// </summary>
    [DefaultExecutionOrder(100)]   // Sword の LateUpdate で点を打った後にメッシュを作る
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SwordTrailMesh : MonoBehaviour
    {
        [SerializeField] Sword sword;
        [SerializeField] Transform root;   // 帯の根元側の位置（pivot の子に置く）。null なら pivot の位置
        [SerializeField] Transform tip;    // 帯の刃先側の位置（pivot の子に置く）。null なら pivot の上 1m
        [SerializeField, Min(0.01f)] float lifetime = 0.15f;   // 点が消えるまでの時間
        [SerializeField, Min(1)] int samplesPerSwing = 60;     // 1回の振りで打つ点の数（多いほどなめらか）
        [SerializeField] bool followPlayer = true;             // true: 軌跡も Player と一緒に動く / false: その場に残る
        [SerializeField, Min(0.01f)] float fullSpeed = 20f;   // 刃先がこの速さ（m/s）以上で頂点カラーの a が1になる

        public enum Shape { Ribbon, Tube }
        [Header("Shape")]
        [SerializeField] Shape shape = Shape.Ribbon;             // Ribbon: 平らな帯 / Tube: 円柱（パイプ）
        [SerializeField, Range(3, 32)] int tubeSegments = 12;    // Tube の円周の分割数
        [SerializeField, Range(0f, 1f)] float tubeTailRadius = 0.3f;   // Tube の尾の太さ（剣側を1とした倍率）

        [Header("Tube Bumps (終わり際の表面のでこぼこ)")]
        [SerializeField, Range(0f, 1.5f)] float bumpAmount = 0.6f;     // でこぼこの深さ（半径に対する倍率）
        [SerializeField, Range(0f, 1f)] float bumpStart = 0.3f;        // 寿命のどこから出始めるか（0=出た瞬間 → 1=消える瞬間）
        [SerializeField, Min(0.01f)] float bumpAroundScale = 1.5f;     // 円周方向の細かさ（大きいほどコブが増える）
        [SerializeField, Min(0f)] float bumpAlongFrequency = 40f;      // 軌跡方向の細かさ（1秒ぶんの軌跡あたり）
        [SerializeField, Min(0f)] float bumpSpeed = 3f;                // コブがうごめく速さ（0 で止まる）

        struct Sample
        {
            public Vector3 root, tip;   // followPlayer なら pivot の親のローカル座標、そうでなければワールド座標
            public float time;
            public float speed;         // 1つ前の点からの刃先の速さ
        }

        readonly List<Sample> samples = new List<Sample>();
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();   // Tube のときだけ使う（断面の外向き。陰影用）
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();   // rgb は白、a に速さを入れる
        readonly List<int> triangles = new List<int>();

        // 切り詰め済みの点（新しい順）
        readonly List<Vector3> rings = new List<Vector3>();   // root, tip を交互に入れる
        readonly List<float> ringU = new List<float>();
        readonly List<float> ringTime = new List<float>();   // 点を打った時刻（でこぼこを点に貼り付けて、流れても模様が滑らないようにする）
        readonly List<Color> ringColor = new List<Color>();

        Mesh mesh;
        MeshRenderer meshRenderer;
        Vector3 rootOffset, tipOffset;   // pivot から見た位置

        // 前フレームの時刻と pivot の親の位置・回転（フレーム間の点を補間するため）
        float prevTime;
        Vector3 prevParentPos;
        Quaternion prevParentRot;

        private void Awake()
        {
            if (sword == null) sword = GetComponentInParent<Sword>();
            if (sword == null)
            {
                Destroy(gameObject);
                Debug.Log("SwordTrailMesh needs Sword in parent");
                return;
            }

            mesh = new Mesh { name = "SwordTrail" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.enabled = false;

            var pivot = sword.Pivot;
            rootOffset = root != null ? pivot.InverseTransformPoint(root.position) : Vector3.zero;
            tipOffset = tip != null ? pivot.InverseTransformPoint(tip.position) : Vector3.up;
        }

        private void OnEnable()
        {
            if (sword == null) return;
            sword.OnSwingStart += OnSwingStart;
            sword.OnSwingUpdate += OnSwingUpdate;
        }

        private void OnDisable()
        {
            if (sword == null) return;
            sword.OnSwingStart -= OnSwingStart;
            sword.OnSwingUpdate -= OnSwingUpdate;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        private void OnSwingStart()
        {
            // 前回の残りが今回の軌跡とつながらないように消す
            samples.Clear();
            prevTime = Time.time - Time.deltaTime;
            GetParentPose(out prevParentPos, out prevParentRot);
        }

        private void OnSwingUpdate(float from, float to)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt((to - from) * samplesPerSwing));
            float now = Time.time;
            GetParentPose(out var parentPos, out var parentRot);
            var parentScale = sword.Pivot.parent != null ? sword.Pivot.parent.lossyScale : Vector3.one;

            // 最初の点だけは振り始めの位置も打つ
            for (int j = samples.Count == 0 ? 0 : 1; j <= steps; j++)
            {
                float k = (float)j / steps;
                var parent = followPlayer
                    ? Matrix4x4.identity
                    : Matrix4x4.TRS(
                        Vector3.Lerp(prevParentPos, parentPos, k),
                        Quaternion.Slerp(prevParentRot, parentRot, k),
                        parentScale);
                var m = sword.SwingPivotMatrix(Mathf.Lerp(from, to, k), parent);
                var s = new Sample
                {
                    root = m.MultiplyPoint3x4(rootOffset),
                    tip = m.MultiplyPoint3x4(tipOffset),
                    time = Mathf.Lerp(prevTime, now, k),
                };
                // 振り始めの点は止まっているので 0
                if (samples.Count > 0)
                {
                    var last = samples[samples.Count - 1];
                    s.speed = Vector3.Distance(last.tip, s.tip) / Mathf.Max(s.time - last.time, 1e-5f);
                }
                samples.Add(s);
            }

            prevTime = now;
            prevParentPos = parentPos;
            prevParentRot = parentRot;
        }

        private void LateUpdate()
        {
            float now = Time.time;

            // 古い点を消す。尾をなめらかに縮めるため、lifetime をまたぐ1点だけは残して下で切り詰める
            while (samples.Count >= 2 && now - samples[1].time >= lifetime) samples.RemoveAt(0);
            if (samples.Count < 2 || now - samples[samples.Count - 1].time >= lifetime)
            {
                samples.Clear();
                meshRenderer.enabled = false;
                return;
            }

            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();

            var space = followPlayer ? sword.Pivot.parent : null;
            var toLocal = transform.worldToLocalMatrix * (space != null ? space.localToWorldMatrix : Matrix4x4.identity);

            // 新しい点（剣側）から順に並べる
            rings.Clear();
            ringU.Clear();
            ringTime.Clear();
            ringColor.Clear();
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                var s = samples[i];
                float age = now - s.time;
                if (age > lifetime)
                {
                    // 一番古い点は lifetime ちょうどの位置まで寄せる
                    var next = samples[i + 1];
                    float nextAge = now - next.time;
                    float f = (lifetime - nextAge) / Mathf.Max(age - nextAge, 1e-5f);
                    s.root = Vector3.Lerp(next.root, s.root, f);
                    s.tip = Vector3.Lerp(next.tip, s.tip, f);
                    age = lifetime;
                }

                rings.Add(s.root);
                rings.Add(s.tip);
                ringU.Add(age / lifetime);
                ringTime.Add(now - age);
                ringColor.Add(new Color(1f, 1f, 1f, Mathf.Clamp01(s.speed / fullSpeed)));
            }

            if (shape == Shape.Tube) BuildTube(toLocal);
            else BuildRibbon(toLocal);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            if (shape == Shape.Tube) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshRenderer.enabled = true;
        }

        void BuildRibbon(Matrix4x4 toLocal)
        {
            for (int i = 0; i < ringU.Count; i++)
            {
                vertices.Add(toLocal.MultiplyPoint3x4(rings[i * 2]));
                vertices.Add(toLocal.MultiplyPoint3x4(rings[i * 2 + 1]));
                uvs.Add(new Vector2(ringU[i], 0f));
                uvs.Add(new Vector2(ringU[i], 1f));
                colors.Add(ringColor[i]);
                colors.Add(ringColor[i]);
            }

            for (int i = 0; i + 3 < vertices.Count; i += 2)
            {
                triangles.Add(i);     triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i + 3);
            }
        }

        void BuildTube(Matrix4x4 toLocal)
        {
            int n = ringU.Count;
            int ringVerts = tubeSegments + 1;   // 継ぎ目の UV を 1 にするため最後の1点は重ねる
            Vector3 prevSide = Vector3.zero;

            float now = Time.time;
            for (int i = 0; i < n; i++)
            {
                var root = rings[i * 2];
                var tip = rings[i * 2 + 1];
                var center = (root + tip) * 0.5f;

                // 進む向き（前後の点の中心から求める）
                var a = i > 0 ? (rings[(i - 1) * 2] + rings[(i - 1) * 2 + 1]) * 0.5f : center;
                var b = i < n - 1 ? (rings[(i + 1) * 2] + rings[(i + 1) * 2 + 1]) * 0.5f : center;
                var forward = a - b;

                // 断面の1軸目は root→tip（進む向きに直交させる）、2軸目はそれと進む向きの外積
                var up = tip - root;
                float radius = up.magnitude * 0.5f * Mathf.Lerp(1f, tubeTailRadius, ringU[i]);
                if (forward.sqrMagnitude > 1e-10f) up = Vector3.ProjectOnPlane(up, forward);
                if (up.sqrMagnitude < 1e-10f) up = Vector3.up;
                up.Normalize();
                var side = forward.sqrMagnitude > 1e-10f ? Vector3.Cross(forward, up).normalized : prevSide;
                if (side.sqrMagnitude < 0.5f) side = Vector3.Cross(up, Vector3.forward).sqrMagnitude > 1e-6f
                    ? Vector3.Cross(up, Vector3.forward).normalized
                    : Vector3.right;
                prevSide = side;

                for (int k = 0; k < ringVerts; k++)
                {
                    float v = (float)k / tubeSegments;
                    float ang = v * Mathf.PI * 2f;
                    float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                    float r = radius * Bump(cos, sin, ringTime[i], ringU[i], now);
                    var p = center + (up * cos + side * sin) * r;
                    vertices.Add(toLocal.MultiplyPoint3x4(p));
                    normals.Add(toLocal.MultiplyVector(up * cos + side * sin).normalized);
                    uvs.Add(new Vector2(ringU[i], v));
                    colors.Add(ringColor[i]);
                }
            }

            for (int i = 0; i < n - 1; i++)
            {
                int r0 = i * ringVerts, r1 = (i + 1) * ringVerts;
                for (int k = 0; k < tubeSegments; k++)
                {
                    triangles.Add(r0 + k);     triangles.Add(r1 + k);     triangles.Add(r0 + k + 1);
                    triangles.Add(r0 + k + 1); triangles.Add(r1 + k);     triangles.Add(r1 + k + 1);
                }
            }
        }

        // 半径にかける倍率。尾に近づくほど（bumpStart 以降）でこぼこが強くなる
        // 円周は cos/sin をノイズの座標に使って継ぎ目なくつなぐ
        float Bump(float cos, float sin, float sampleTime, float u, float now)
        {
            float w = Mathf.InverseLerp(bumpStart, 1f, u);
            if (w <= 0f || bumpAmount <= 0f) return 1f;
            w *= w;   // 終わり際ほど急に荒れる

            float along = sampleTime * bumpAlongFrequency;
            float anim = now * bumpSpeed;
            float x = cos * bumpAroundScale, y = sin * bumpAroundScale;
            // 大きなコブ + 細かいコブ（0〜1 → -1〜1）
            float n1 = Mathf.PerlinNoise(x + along + 13.7f, y + anim);
            float n2 = Mathf.PerlinNoise(x * 2.3f - anim * 1.4f + 71.1f, y * 2.3f + along * 2.1f);
            float noise = (n1 * 0.7f + n2 * 0.3f) * 2f - 1f;
            return Mathf.Max(0.05f, 1f + noise * bumpAmount * w);
        }

        void GetParentPose(out Vector3 pos, out Quaternion rot)
        {
            var parent = sword.Pivot.parent;
            pos = parent != null ? parent.position : Vector3.zero;
            rot = parent != null ? parent.rotation : Quaternion.identity;
        }
    }
}
