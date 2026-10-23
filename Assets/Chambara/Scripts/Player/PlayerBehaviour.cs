using System;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// 1人分の入力→状態の流れ（タイミング管理）と表示
    /// </summary>
    public class PlayerBehaviour : MonoBehaviour
    {
        [Header("タイミング（秒）")]
        [SerializeField] float guardHoldTime  = 1f;     // 同じ方向にこの時間構えるとガード成立
        [SerializeField] float attackStartup  = 0.2f;   // 振ってから判定まで
        [SerializeField] float attackRecovery = 0.33f;  // 判定後の硬直
        [SerializeField] float parriedStun    = 0.8f;   // ガードされたときの怯み
        [SerializeField] float clashStun      = 0.5f;   // 相殺したときの怯み

        [Header("効果が出るタイミング（0:振り始め 1:振り終わり）")]
        [SerializeField, Range(0f, 1f)] float attackImpact = 1f;   // 攻撃が当たる・ガードされる
        [SerializeField, Range(0f, 1f)] float clashImpact  = 1f;   // 相殺で剣がぶつかる

        [Header("のけぞり（怯み中）")]
        [SerializeField] float leanAngle = 25f;
        [SerializeField] float leanBack  = 0.3f;
        [SerializeField] float leanSpeed = 15f;

        [Header("モデル切り替え")]
        [SerializeField] ModelList playerModels;

        public Fighter Data { get; private set; }
        public float AttackTime { get; private set; }   // 振った時刻
        public float AttackStartup => attackStartup;
        public bool Resolved;                            // この攻撃の判定が済んだか
        public bool Lost { get; private set; }           // 負けた（試合終了後も黒く表示する）
        public bool Won { get; private set; }            // 勝った
        public float AttackImpactTime => SwingTime(attackImpact);   // 攻撃の効果が出る時刻
        public float ClashImpactTime  => SwingTime(clashImpact);    // 相殺の効果が出る時刻
        public bool IsHitTiming => Data.state == PlayerState.Attack && !Resolved && Time.time >= AttackImpactTime;

        BodyRig bodyRig;

        public event Action<PlayerState, int> OnStateChanged;

        IChambaraInputContext input;
        Sword sword;
        Renderer rend;
        Vector3 edgePos, opponentEdgePos;
        Quaternion baseRotation;
        float stateEnd;   // 攻撃・怯みが終わる時刻
        int pose = -1;
        float poseHold;
        float lean;

        static readonly int[] guardDirections = new int[4] { 0, 2, 4, 6 };   // 上右下左の4方向

        public void Init(int playerIndex,string modelName, Fighter data, Transform edge, Transform opponentEdge)
        {
            Data = data;
            edgePos = edge.position;
            opponentEdgePos = opponentEdge.position;
            transform.LookAt(opponentEdge);
            baseRotation = transform.rotation;
            GenerateModel(modelName);
            rend = GetComponentInChildren<Renderer>();
            sword = GetComponentInChildren<Sword>();
            input = GetComponent<IChambaraInputContext>();
            input?.Init(playerIndex);
            BodyRig rig = GetComponent<BodyRig>();
            if (rig != null) rig.Init(this);
        }

        private void GenerateModel(string modelName)
        {
            if (playerModels == null) return;
            GameObject modelPrefab = playerModels.GetModel(modelName);
            if (modelPrefab == null) return;
            GameObject modelInstance = Instantiate(modelPrefab, transform);
            modelInstance.transform.localPosition = new Vector3(0f,-0.41f,0f);
            modelInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        // 入力を読んで状態を更新する
        public void UpdateState(float deltaTime)
        {
            if (Data.state == PlayerState.Attack || Data.state == PlayerState.Stun)
            {
                if (Time.time < stateEnd) return;
                SetState(PlayerState.Idle, -1);
            }

            PlayerInput in_ = input != null ? input.Read() : PlayerInput.None;
            if (in_.swing >= 0)
            {
                SetState(PlayerState.Attack, in_.swing);
                AttackTime = Time.time;
                stateEnd = AttackTime + attackStartup + attackRecovery;
                Resolved = false;
                return;
            }

            poseHold = in_.pose >= 0 && in_.pose == pose ? poseHold + deltaTime : 0f;
            pose = in_.pose;
            if (poseHold >= guardHoldTime)
            {
                for (int i = 0; i < guardDirections.Length; i++)
                {
                    if (pose == guardDirections[i])
                    {
                        SetState(PlayerState.Guard, pose);
                        return;
                    }
                }
            }
            SetState(PlayerState.Idle, -1);
        }

        public void Stun(bool clash)
        {
            SetState(PlayerState.Stun, -1);
            stateEnd = Time.time + (clash ? clashStun : parriedStun);
        }

        public void Lose() => Lost = true;
        public void Win() => Won = true;

        // 振り下ろしの割合 progress（振りかぶりの後を0、振り切りを1）に剣が来る時刻
        float SwingTime(float progress)
        {
            float windup = sword != null ? sword.WindupRatio : 0f;
            return AttackTime + attackStartup * Mathf.Lerp(windup, 1f, progress);
        }

        void SetState(PlayerState state, int dir)
        {
            if (state == PlayerState.Attack || state == PlayerState.Stun) poseHold = 0f;   // 振る・怯むと構えの溜めはやり直し
            Data.state = state;
            Data.dir = dir;
            OnStateChanged?.Invoke(state, dir);
        }

        public void Render(float distance)
        {
            bool stunned = Data.state == PlayerState.Stun;
            lean = Mathf.Lerp(lean, stunned ? 1f : 0f, 1f - Mathf.Exp(-leanSpeed * Time.deltaTime));
            transform.rotation = baseRotation * Quaternion.Euler(-leanAngle * lean, 0f, 0f);

            // 距離1で自分の端、距離0で中央。のけぞり中は後ろに下がる
            transform.position = Vector3.Lerp(edgePos, opponentEdgePos, (1f - distance) / 2f)
                               - baseRotation * Vector3.forward * (leanBack * lean);

            // 仮表示：負け=黒 怯み=黄 攻撃=赤 ガード=青 通常=白
            if (rend == null) return;
            rend.material.color = Lost ? Color.black : Data.state switch
            {
                PlayerState.Stun   => Color.yellow,
                PlayerState.Attack => Color.red,
                PlayerState.Guard  => Color.blue,
                _                  => Color.white,
            };
        }
    }
}
