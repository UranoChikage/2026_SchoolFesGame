using System.Collections.Generic;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// Sword が振っている間だけ子の TrailRenderer を出す
    /// フレーム間は Sword の振りの軌道を計算して点を足し、速く振っても弧になるようにする
    /// Trails（TrailRenderer をまとめた親）に付ける
    /// </summary>
    public class SwordTrail : MonoBehaviour
    {
        [SerializeField] Sword sword;
        [SerializeField] TrailRenderer[] trails;
        [SerializeField, Min(1)] int samplesPerSwing = 60;   // 1回の振りで打つ点の数（多いほどなめらか）

        Vector3[] offsets;   // 各 Trail の pivot から見た位置
        readonly List<Vector3> points = new List<Vector3>();

        private void Awake()
        {
            if (sword == null) sword = GetComponentInParent<Sword>();
            if (trails == null || trails.Length == 0) trails = GetComponentsInChildren<TrailRenderer>();

            // 振っている間だけ出す（emitting が false の間に足した点は描画されない）
            SetEmitting(false);
            offsets = new Vector3[trails.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                offsets[i] = sword.Pivot.InverseTransformPoint(trails[i].transform.position);
            }
        }

        private void OnEnable()
        {
            if (sword == null) return;
            sword.OnSwingStart += OnSwingStart;
            sword.OnSwingUpdate += OnSwingUpdate;
            sword.OnSwingEnd += OnSwingEnd;
        }

        private void OnDisable()
        {
            if (sword == null) return;
            sword.OnSwingStart -= OnSwingStart;
            sword.OnSwingUpdate -= OnSwingUpdate;
            sword.OnSwingEnd -= OnSwingEnd;
        }

        private void OnSwingStart()
        {
            // 前回の残りが今回の軌跡とつながらないように消す
            foreach (var trail in trails) trail.Clear();
            SetEmitting(true);
        }

        // 最後の点は OnSwingUpdate で打ち終わっているので、すぐ止めてよい
        private void OnSwingEnd() => SetEmitting(false);

        private void OnSwingUpdate(float from, float to)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt((to - from) * samplesPerSwing));

            for (int i = 0; i < trails.Length; i++)
            {
                points.Clear();
                // 最初の点だけは振り始めの位置も打つ
                for (int j = trails[i].positionCount == 0 ? 0 : 1; j <= steps; j++)
                {
                    var m = sword.SwingPivotMatrix(Mathf.Lerp(from, to, (float)j / steps));
                    points.Add(m.MultiplyPoint3x4(offsets[i]));
                }
                trails[i].AddPositions(points.ToArray());
            }
        }

        void SetEmitting(bool value)
        {
            foreach (var trail in trails) trail.emitting = value;
        }
    }
}
