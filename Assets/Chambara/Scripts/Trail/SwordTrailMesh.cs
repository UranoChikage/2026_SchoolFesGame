using System.Collections.Generic;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// TrailRenderer の代わりに、刃の root〜tip が通った面を帯状のメッシュにして描く
    /// 点ごとに打った時刻を持たせて、時間で少しずつ消す（TrailRenderer は1フレームで足した点が同時に消えるので尾がガタつく）
    /// UV.x = 経過時間 / lifetime（0=剣側 → 1=尾）、UV.y = 0:root → 1:tip。点の数や長さで UV が伸び縮みしない
    /// MeshFilter と MeshRenderer（Material は SwordTrail.mat）と同じ GameObject に付ける
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

        struct Sample
        {
            public Vector3 root, tip;   // followPlayer なら pivot の親のローカル座標、そうでなければワールド座標
            public float time;
        }

        readonly List<Sample> samples = new List<Sample>();
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();   // シェーダーが頂点カラーを掛けるので白を入れておく
        readonly List<int> triangles = new List<int>();

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
                samples.Add(new Sample
                {
                    root = m.MultiplyPoint3x4(rootOffset),
                    tip = m.MultiplyPoint3x4(tipOffset),
                    time = Mathf.Lerp(prevTime, now, k),
                });
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
            uvs.Clear();
            colors.Clear();
            triangles.Clear();

            var space = followPlayer ? sword.Pivot.parent : null;
            var toLocal = transform.worldToLocalMatrix * (space != null ? space.localToWorldMatrix : Matrix4x4.identity);

            // 新しい点（剣側）から順に並べる
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

                float u = age / lifetime;
                vertices.Add(toLocal.MultiplyPoint3x4(s.root));
                vertices.Add(toLocal.MultiplyPoint3x4(s.tip));
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
                colors.Add(Color.white);
                colors.Add(Color.white);
            }

            for (int i = 0; i + 3 < vertices.Count; i += 2)
            {
                triangles.Add(i);     triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i + 3);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            meshRenderer.enabled = true;
        }

        void GetParentPose(out Vector3 pos, out Quaternion rot)
        {
            var parent = sword.Pivot.parent;
            pos = parent != null ? parent.position : Vector3.zero;
            rot = parent != null ? parent.rotation : Quaternion.identity;
        }
    }
}
