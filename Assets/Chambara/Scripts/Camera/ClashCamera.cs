using System;
using System.Collections;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// 吹っ飛ばされたプレイヤーを、いくつかの決まった位置から目で追うカメラ
    /// CameraManager から SetTarget で追いかける Transform を受け取る
    /// カットごとに吹っ飛びを最初からやり直して、別の位置から繰り返し見せる
    /// 全部見せ終わったら、最後のカットから勝った方を映す位置へ滑らかに移る
    /// </summary>
    [DefaultExecutionOrder(200)]   // BodyRig が体を動かした後にカメラを合わせる
    [RequireComponent(typeof(Camera))]
    public class ClashCamera : MonoBehaviour
    {
        [Serializable]
        public class Shot
        {
            [Tooltip("どこから見るか（度）。0:飛ぶ方向の真横 / 90:飛んでいく先 / -90:飛んでくる側")]
            public float angle = 0f;
            public float distance = 4f;    // 吹っ飛び始めの位置からの距離
            public float height   = 1f;    // 吹っ飛び始めの位置より上にずらす高さ
            [Tooltip("見る点。吹っ飛び始めの位置から（x:横 y:上 z:飛ぶ方向）。0なら吹っ飛び始めの位置を見る")]
            public Vector3 lookOffset = Vector3.zero;
            public float duration = 1.2f;  // このカットを映す時間（ゲーム内時間・秒）
        }

        [SerializeField] private Shot[] shots =
        {
            new Shot { angle = -20f, distance = 4f, height = 1f,   duration = 1.2f },
            new Shot { angle = 70f,  distance = 3f, height = 0.3f, duration = 1.2f },
            new Shot { angle = 160f, distance = 5f, height = 3f,   duration = 1.2f },
        };
        [SerializeField] private int loops = 1;              // カットを全部見せるのを何周するか
        [SerializeField] private bool follow = false;       // 向きで体を追いかけるか（false ならカットごとの lookOffset の方を向いたまま）
        [SerializeField] private float followSpeed = 8f;    // 向きを追いかける速さ（大きいほどぴったり付いていく）

        [Header("Winner")]
        [SerializeField] private Vector3 winnerOffset = new Vector3(0.8f, 1.4f, 2.2f);   // 勝った方から見たカメラの位置（x:右 y:上 z:前）
        [SerializeField] private Vector3 winnerLook   = new Vector3(0f, 1.1f, 0f);       // 勝った方から見た注視点
        [SerializeField] private float winnerMoveTime = 1.5f;                            // 最後のカットから移るのにかける時間（ゲーム内時間・秒）
        [SerializeField] private AnimationCurve winnerMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Gizmo")]
        [SerializeField, Tooltip("再生していないときに、この位置・向きのプレイヤーが吹っ飛ぶとしてカメラ位置を表示する（プレイヤーの出現位置など）")]
        private Transform previewTarget;
        [SerializeField] private float previewBodyHeight = 0.8f;   // previewTarget から体の中心までの高さ
        [SerializeField, Tooltip("再生していないときに、この位置・向きを勝った方として勝者カメラの位置を表示する")]
        private Transform previewWinner;

        Transform target, winner;
        int currentShot = -1;
        bool showingWinner;   // 勝った方へ移り始めたら、吹っ飛びは追いかけない
        BodyRig bodyRig;
        Vector3 origin, fly, side;   // 吹っ飛び始めの体の位置と、飛ぶ方向・その横（吹っ飛び始めに固定）
        Coroutine replayRoutine;

        //後で消す！（AIは消さないで～）

        [SerializeField] private ScoreUi scoreUi;
        public void UpdateScore()
        {
            if(scoreUi != null && bodyRig != null)
            {
                //1Pが1ポイント取って1-0の場合の例
                scoreUi.UpdateScore(1,0,1);
            }
        }

        //ここまで！


        // target: 吹っ飛ぶ体。吹っ飛び始めの瞬間に呼ぶ（体が回り始める前に、位置と飛ぶ方向を覚えておく）
        // 吹っ飛び始めの向きの後ろへ飛ぶので、その向きで見る方向を決めて固定する。映し始めるのは Play を呼んでから
        // winner: 勝った方。カットを見せ終わったらここを映す（null なら最後のカットのまま止める）
        public void SetTarget(Transform target, Transform winner = null)
        {
            if (replayRoutine != null) StopCoroutine(replayRoutine);
            replayRoutine = null;
            currentShot = -1;
            showingWinner = false;
            this.target = target;
            this.winner = winner;
            if (target == null) return;

            bodyRig = target.GetComponentInParent<BodyRig>();
            origin = target.position;
            fly = Vector3.ProjectOnPlane(-target.forward, Vector3.up);
            fly = fly.sqrMagnitude > 0.0001f ? fly.normalized : Vector3.back;
            side = Vector3.Cross(fly, Vector3.up);
        }

        // カットを順に映す。GameObject を有効にしてから呼ぶ
        // replayFirst: 最初のカットも吹っ飛びをやり直すか（false なら今続いている吹っ飛びをそのまま映す）
        public void Play(bool replayFirst)
        {
            if (target == null) return;
            if (replayRoutine != null) StopCoroutine(replayRoutine);
            replayRoutine = StartCoroutine(Replay(replayFirst));
        }

        IEnumerator Replay(bool replayFirst)
        {
            if (shots == null || shots.Length == 0)
            {
                PlaceCamera(new Shot());
                yield break;
            }

            bool first = !replayFirst;
            for (int loop = 0; loop < Mathf.Max(loops, 1); loop++)
            {
                for (int i = 0; i < shots.Length; i++)
                {
                    // 最初のカットは（replayFirst でなければ）今始まった吹っ飛びをそのまま映す。それ以外はやり直す
                    if (!first && bodyRig != null) bodyRig.PlayKnockback();
                    first = false;

                    currentShot = i;
                    PlaceCamera(shots[i]);
                    yield return new WaitForSeconds(shots[i].duration);   // 吹っ飛びと同じゲーム内時間で数える
                }
            }

            if (winner != null) yield return MoveToWinner();
            replayRoutine = null;   // 最後の位置のまま止める

            //ここも後で消す
            yield return new WaitForSeconds(0.5f);
            UpdateScore();
            //ここまで
        }

        // 最後のカットの位置・向きから、勝った方を映す位置・向きへ滑らかに移る
        IEnumerator MoveToWinner()
        {
            showingWinner = true;
            currentShot = -1;
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            WinnerPose(winner, out Vector3 toPos, out Quaternion toRot);   // 勝った方は動かないので、移り先は最初に一度だけ求める
            bool hasCurve = winnerMoveCurve != null && winnerMoveCurve.length > 0;   // カーブ未設定時は線形

            for (float t = 0f; t < winnerMoveTime; t += Time.deltaTime)
            {
                float k = hasCurve ? winnerMoveCurve.Evaluate(t / winnerMoveTime) : t / winnerMoveTime;
                transform.SetPositionAndRotation(Vector3.LerpUnclamped(fromPos, toPos, k), Quaternion.SlerpUnclamped(fromRot, toRot, k));
                yield return null;
            }
            transform.SetPositionAndRotation(toPos, toRot);
        }

        void WinnerPose(Transform w, out Vector3 pos, out Quaternion rot)
        {
            Quaternion yaw = WinnerYaw(w);
            pos = w.position + yaw * winnerOffset;
            Vector3 dir = WinnerLookPoint(w) - pos;
            rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : yaw;
        }

        Vector3 WinnerLookPoint(Transform w) => w.position + WinnerYaw(w) * winnerLook;

        // 勝った方の向き（水平）を基準に置く
        static Quaternion WinnerYaw(Transform w)
        {
            Vector3 forward = Vector3.ProjectOnPlane(w.forward, Vector3.up);
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        }

        private void PlaceCamera(Shot shot)
        {
            transform.position = ShotPosition(shot, origin, side);
            // 追いかけるときは、カットが変わった瞬間すぐ体を向く。追いかけないときは決めた点を向いたまま
            transform.LookAt(follow ? target.position : LookPoint(shot, origin, fly, side));
        }

        static Vector3 ShotPosition(Shot shot, Vector3 origin, Vector3 side)
        {
            Vector3 dir = Quaternion.AngleAxis(shot.angle, Vector3.up) * side;
            return origin + dir * shot.distance + Vector3.up * shot.height;
        }

        static Vector3 LookPoint(Shot shot, Vector3 origin, Vector3 fly, Vector3 side)
        {
            return origin + side * shot.lookOffset.x + Vector3.up * shot.lookOffset.y + fly * shot.lookOffset.z;
        }

        // 位置はカットごとに置いたまま固定し、向きだけ体の方へ回して追いかける
        private void LateUpdate()
        {
            if (target == null || !follow || showingWinner) return;

            Vector3 dir = target.position - transform.position;
            if (dir.sqrMagnitude < 0.0001f) return;

            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);   // ヒットストップ・スローに合わせてゲーム内時間で追う
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), k);
        }

        // 各カットのカメラ位置を表示する。追いかけている間は実際の位置、それ以外は previewTarget から見積もる
        private void OnDrawGizmosSelected()
        {
            DrawWinnerGizmo();
            if (shots == null) return;

            Vector3 o, f;
            if (target != null)
            {
                o = origin;
                f = fly;
            }
            else if (previewTarget != null)
            {
                o = previewTarget.position + Vector3.up * previewBodyHeight;
                f = Vector3.ProjectOnPlane(-previewTarget.forward, Vector3.up);
                f = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.back;
            }
            else return;
            Vector3 s = Vector3.Cross(f, Vector3.up);

            // 吹っ飛び始めの位置と飛ぶ方向
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(o, 0.2f);
            Gizmos.DrawLine(o, o + f * 2f);

            Camera cam = GetComponent<Camera>();
            for (int i = 0; i < shots.Length; i++)
            {
                Vector3 p = ShotPosition(shots[i], o, s);
                Vector3 look = LookPoint(shots[i], o, f, s);
                Gizmos.color = target != null && i == currentShot ? Color.yellow : Color.cyan;   // 今映しているカットは黄色
                Gizmos.DrawWireSphere(p, 0.15f);
                Gizmos.DrawLine(p, look);
                Gizmos.DrawWireSphere(look, 0.05f);

                // 映る範囲（follow しないときの向き）
                if (cam != null && (look - p).sqrMagnitude > 0.0001f)
                {
                    Matrix4x4 m = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(p, Quaternion.LookRotation(look - p), Vector3.one);
                    Gizmos.DrawFrustum(Vector3.zero, cam.fieldOfView, 1.5f, 0.1f, cam.aspect);
                    Gizmos.matrix = m;
                }
#if UNITY_EDITOR
                UnityEditor.Handles.Label(p + Vector3.up * 0.3f, $"Shot {i}");
#endif
            }
        }

        // 勝者カメラの位置と向き。追いかけている間は実際の勝った方、それ以外は previewWinner から見積もる
        private void DrawWinnerGizmo()
        {
            Transform w = winner != null ? winner : previewWinner;
            if (w == null) return;

            WinnerPose(w, out Vector3 pos, out Quaternion rot);

            // 勝った方の足元と向き
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(w.position, 0.2f);
            Gizmos.DrawLine(w.position, w.position + Vector3.ProjectOnPlane(w.forward, Vector3.up).normalized * 1f);

            Gizmos.color = showingWinner ? Color.yellow : Color.green;   // 勝った方を映しているときは黄色
            Gizmos.DrawWireSphere(pos, 0.15f);
            Vector3 look = WinnerLookPoint(w);
            Gizmos.DrawLine(pos, look);
            Gizmos.DrawWireSphere(look, 0.05f);
            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                Matrix4x4 m = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
                Gizmos.DrawFrustum(Vector3.zero, cam.fieldOfView, 1.5f, 0.1f, cam.aspect);
                Gizmos.matrix = m;
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(pos + Vector3.up * 0.3f, "Winner");
#endif
        }
    }
}
