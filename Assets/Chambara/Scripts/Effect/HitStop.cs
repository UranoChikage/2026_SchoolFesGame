using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Chambara
{
    /// <summary>
    /// timeScale にして duration 止めてから元の速さに戻す
    /// シーンのどこかに1つ置き、ChambaraManager に設定する（当たったときに ChambaraManager から Play が呼ばれる）
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        private static HitStop instance;
        public static HitStop Instance => instance;

        [SerializeField] float timeScale = 0f;      // 一番遅くなったときの速さ（0で完全に止める）
        [SerializeField] float duration = 0.15f;   // 当たってから一番遅くなるまで（実時間）。0ですぐ止める

        Coroutine running;
        float resumeScale;   // 当たる前の速さ。スロー中に当たったときはスローの速さに戻す
        Action pendingEnd;   // 元の速さに戻ったときに呼ぶもの
        public bool IsPlaying => running != null;
        public event Action OnPlay;   // 当たって遅くし始めた瞬間
        public event Action OnEnd;    // 元の速さに戻った瞬間

        private void Awake()
        {
            instance = this;
        }

        /// <summary>
        /// ヒットストップを始める。onEnd は元の速さに戻ったときに呼ばれる
        /// </summary>
        public void Play(Action onEnd = null)
        {
            // ヒットストップ中にもう一度呼ばれたら、最初に当たる前の速さに戻す。前の onEnd も最後に呼ぶ
            if (running != null) StopCoroutine(running);
            else resumeScale = Time.timeScale;
            pendingEnd += onEnd;
            running = StartCoroutine(Stop());
            OnPlay?.Invoke();
        }

        // スロー演出の途中で当たったときは、duration + stopDuration を ChambaraManager の slowDuration より短くしておく
        IEnumerator Stop()
        {
            var manager = ChambaraManager.Instance;

            manager.SetTimeScale(timeScale);
            CameraManager.Instance.ExpandSmooth(duration);
            yield return new WaitForSecondsRealtime(duration);



            // 元の速さに戻す
            manager.SetTimeScale(resumeScale);
            running = null;

            var end = pendingEnd;
            pendingEnd = null;
            end?.Invoke();
            OnEnd?.Invoke();
        }
    }
}
