using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// ChambaraSimulatorをUnityで動かす。入力を集めて60fps固定でTickし、結果を表示側に渡す
    /// </summary>
    public class ChambaraManager : MonoBehaviour
    {
        const float TickInterval = 1f / 60f;

        [SerializeField] PlayerBehaviour playerPrefab;
        [SerializeField] Transform edgePlayerPos0;
        [SerializeField] Transform edgePlayerPos1;
        [SerializeField] ChambaraRule rule = new ChambaraRule();

        ChambaraSimulator sim;
        PlayerBehaviour[] players;
        float elapsed;

        private void Start()
        {
            StartGame();
        }

        private void StartGame()
        {
            players = new[]
            {
                Instantiate(playerPrefab, edgePlayerPos0.position, Quaternion.identity),
                Instantiate(playerPrefab, edgePlayerPos1.position, Quaternion.identity),
            };
            players[0].Init(0, edgePlayerPos1);
            players[1].Init(1, edgePlayerPos0);

            sim = new ChambaraSimulator(rule);
            sim.OnHit      += w => Debug.Log($"{w + 1}P の勝ち");
            sim.OnGuarded  += p => Debug.Log($"{p + 1}P の攻撃がガードされた");
            sim.OnClash    += () => Debug.Log("相打ち！両者怯み");
            sim.OnShowdown += (w, diff) => Debug.Log($"ほぼ同時！{diff}フレーム差で {w + 1}P の勝ち");
            elapsed = 0f;
        }

        private void Update()
        {
            // Fixed Timestep(0.02)は他のシーンと共有なので、ここで独自に60fpsへ刻む
            elapsed += Time.deltaTime;
            while (elapsed >= TickInterval && !sim.Finished)
            {
                elapsed -= TickInterval;
                sim.Tick(players[0].ReadInput(), players[1].ReadInput());
                if (sim.Finished && sim.Winner < 0) Debug.Log("時間切れ 引き分け");
            }

            for (int i = 0; i < players.Length; i++)
            {
                players[i].Render(sim, sim.f[i]);
            }
        }
    }
}
