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

        [Header("突き（前へ出る距離・届く距離は ChambaraRule で設定）")]
        [SerializeField] float thrustStartup     = 0.15f;  // 突いてから判定まで（この間に前へ出る）
        [SerializeField] float thrustRecovery    = 0.4f;   // 判定後の硬直
        [SerializeField] float thrustGuardedStun = 1.2f;   // 突きをガードされたときの操作不能時間
        [SerializeField] float thrustHitStun     = 1.0f;   // 突きを受けたときの操作不能時間（のけぞる）
        [SerializeField] float thrustPushTime    = 0.2f;   // 突きが当たった後、受けた側が下がる・当てた側が詰め寄るのにかかる時間

        [Header("移動（1歩の距離は ChambaraRule で設定）")]
        [SerializeField] float stepTime   = 0.25f;  // 1歩にかかる時間（秒）。この間は他の行動を受け付けない
        [SerializeField] float jumpHeight = 0.3f;   // 1歩で跳ぶ高さ

        [Header("効果が出るタイミング（0:振り始め 1:振り終わり）")]
        [SerializeField, Range(0f, 1f)] float attackImpact = 1f;   // 攻撃が当たる・ガードされる
        [SerializeField, Range(0f, 1f)] float clashImpact  = 1f;   // 相殺で剣がぶつかる

        [Header("のけぞり（怯み中）")]
        [SerializeField] float leanAngle = 25f;
        [SerializeField] float leanBack  = 0.3f;
        [SerializeField] float leanSpeed = 15f;

        [Header("モデル切り替え")]
        [SerializeField] ModelList playerModels;
        [SerializeField] ModelList swordModels;   // 剣は Sword の pivot の子に生成する（刃は+Y方向に伸ばしておく）

        public Fighter Data { get; private set; }
        public float AttackTime { get; private set; }   // 振った時刻
        public float AttackStartup => attackStartup;
        public float ThrustStartup => thrustStartup;
        public bool IsAttacking => Data.state == PlayerState.Attack || Data.state == PlayerState.Thrust;   // 斬り・突きの最中
        public bool Resolved;                            // この攻撃の判定が済んだか
        public bool Lost { get; private set; }           // 負けた（試合終了後も黒く表示する）
        public bool Won { get; private set; }            // 勝った
        public float AttackImpactTime => Data.state == PlayerState.Thrust ? ThrustImpactTime : SwingTime(attackImpact);   // 攻撃の効果が出る時刻
        public float ClashImpactTime  => Data.state == PlayerState.Thrust ? ThrustImpactTime : SwingTime(clashImpact);    // 相殺の効果が出る時刻
        float ThrustImpactTime => AttackTime + thrustStartup;   // 突きは前へ出終わった瞬間に効果が出る
        public bool IsHitTiming => IsAttacking && !Resolved && Time.time >= AttackImpactTime;
        public bool IsStepping { get; private set; }     // 移動中（1歩・突きで前へ出る・突きで押される・詰め寄る）
        public float StepJump { get; private set; }      // 1歩で跳んでいる高さ（BodyRigが体だけを持ち上げる。カメラが揺れないようtransformは動かさない）

        BodyRig bodyRig;
        public Transform Body => bodyRig != null && bodyRig.Body != null ? bodyRig.Body : transform;   // 体の中心（吹っ飛びの追尾用）

        public event Action<PlayerState, int> OnStateChanged;

        IChambaraInputContext input;
        ISwordTracking tracking;
        Sword sword;
        Renderer rend;
        Vector3 edgePos, opponentEdgePos;
        Quaternion baseRotation;
        float stateEnd;   // 攻撃・怯みが終わる時刻
        int pose = -1;
        float poseHold;
        float lean;
        ChambaraSimulator sim;
        int playerIndex;
        float stepStart, stepFrom, stepDuration, stepHeight;
        bool prevFront, prevBack;   // 押し直し判定用（前回読んだときに押されていたか）

        static readonly int[] guardDirections = new int[4] { 0, 2, 4, 6 };   // 上右下左の4方向

        /// <summary>このプレイヤーのキャラクター名（ModelList の名前）</summary>
        public string ModelName { get; private set; }

        /// <summary>このプレイヤーの武器名（SwordModels の名前）</summary>
        public string SwordName { get; private set; }

        public void Init(int playerIndex, string modelName, string swordName, ChambaraSimulator sim, Transform edge, Transform opponentEdge)
        {
            ModelName = modelName;
            SwordName = swordName;
            this.sim = sim;
            this.playerIndex = playerIndex;
            Data = sim.players[playerIndex];
            edgePos = edge.position;
            opponentEdgePos = opponentEdge.position;
            transform.LookAt(opponentEdge);
            baseRotation = transform.rotation;
            GenerateModel(modelName);
            rend = GetComponentInChildren<Renderer>();
            sword = GetComponentInChildren<Sword>();
            GenerateSword(swordName);
            // Joy-Con があればそちらを使う（Joy-Conが使えないときは JoyconInputContext が DebugInputContext に任せる）
            input = TryGetComponent(out JoyconInputContext joycon) ? (IChambaraInputContext)joycon : GetComponent<IChambaraInputContext>();
            input?.Init(playerIndex);
            tracking = input as ISwordTracking;
            bodyRig = GetComponent<BodyRig>();
            if (bodyRig != null) bodyRig.Init(this);
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

        private void GenerateSword(string swordName)
        {
            if (swordModels == null || sword == null || sword.Pivot == null) return;
            GameObject swordPrefab = swordModels.GetModel(swordName);
            if (swordPrefab == null) return;
            Instantiate(swordPrefab, sword.Pivot, false);

            // 仮の刃は隠す
            Renderer defaultBlade = sword.GetComponent<Renderer>();
            if (defaultBlade != null) defaultBlade.enabled = false;
        }

        // 入力を読んで状態を更新する
        public void UpdateState(float deltaTime)
        {
            if (IsAttacking || Data.state == PlayerState.Stun)
            {
                if (Time.time < stateEnd) return;
                SetState(PlayerState.Idle, -1);
            }
            if (IsStepping) return;   // 移動中は他の行動を受け付けない

            PlayerInput in_ = input != null ? input.Read() : PlayerInput.None;
            bool frontPressed = in_.isFront && !prevFront;   // 押しっぱなしでは続けて進まない
            bool backPressed  = in_.isBack  && !prevBack;
            prevFront = in_.isFront;
            prevBack  = in_.isBack;

            if (in_.swing >= 0)
            {
                SetState(PlayerState.Attack, in_.swing);
                AttackTime = Time.time;
                stateEnd = AttackTime + attackStartup + attackRecovery;
                Resolved = false;
                return;
            }

            if (in_.thrust)
            {
                SetState(PlayerState.Thrust, -1);
                AttackTime = Time.time;
                stateEnd = AttackTime + thrustStartup + thrustRecovery;
                Resolved = false;
                pose = -1;
                sim.ThrustAdvance(playerIndex);
                MoveToTarget(thrustStartup, 0f);   // 判定の瞬間にちょうど前へ出終わる
                return;
            }

            // 前後同時押しはどちらにも進まない
            if (frontPressed != backPressed && sim.TryStep(playerIndex, frontPressed))
            {
                MoveToTarget(stepTime, jumpHeight);
                pose = -1;
                poseHold = 0f;   // 歩くと構えの溜めはやり直し
                SetState(PlayerState.Idle, -1);
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

        public void Stun(bool clash) => Stun(clash ? clashStun : parriedStun);

        // 一定時間操作不能にする
        public void Stun(float duration)
        {
            SetState(PlayerState.Stun, -1);
            stateEnd = Time.time + duration;
        }

        // 突きをガードされた
        public void ThrustGuarded() => Stun(thrustGuardedStun);

        // 突きを受けた：のけぞって操作不能になり、行き先（sim が決めた targetPosition）まで押し下げられる
        public void ThrustHitTaken()
        {
            Stun(thrustHitStun);
            MoveToTarget(thrustPushTime, 0f);
        }

        // 突きを当てた：相手の目の前（sim が決めた targetPosition）まで詰め寄る
        public void ThrustClose() => MoveToTarget(thrustPushTime, 0f);

        // 今の位置から Data.targetPosition まで duration 秒かけて動く（height: 途中で跳ねる高さ）
        void MoveToTarget(float duration, float height)
        {
            IsStepping = true;
            stepStart = Time.time;
            stepFrom = Data.position;
            stepDuration = duration;
            stepHeight = height;
        }

        public void Lose() => Lost = true;

        public bool InResult { get; private set; }   // 試合の決着演出中（決まった位置に立って勝ち・負けのポーズ）
        Vector3 resultPos;

        // 試合の決着演出：point の位置・向きに立たせて、勝ち・負けのポーズをとらせる（吹っ飛んで消えていても戻す）
        public void PlayResult(Transform point)
        {
            InResult = true;
            gameObject.SetActive(true);
            IsStepping = false;
            StepJump = 0f;
            lean = 0f;
            resultPos = point.position;
            baseRotation = point.rotation;
            transform.SetPositionAndRotation(resultPos, baseRotation);
            if (bodyRig != null) bodyRig.PlayResult(Won);
        }

        /// <summary>入力元が剣の向きを出しているとき、その回転（プレイヤー基準）を返す</summary>
        public bool TryGetTrackedSword(out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            return tracking != null && tracking.TryGetSwordRotation(out rotation);
        }

        // 斬られて吹っ飛ぶ。いつ吹っ飛ばすかは ChambaraManager が決める（ヒットストップが終わってから）
        public void Knockback()
        {
            if (bodyRig != null) bodyRig.PlayKnockback();
        }
        public void Win() => Won = true;

        // 振り下ろしの割合 progress（振りかぶりの後を0、振り切りを1）に剣が来る時刻
        float SwingTime(float progress)
        {
            float windup = sword != null ? sword.WindupRatio : 0f;
            return AttackTime + attackStartup * Mathf.Lerp(windup, 1f, progress);
        }

        void SetState(PlayerState state, int dir)
        {
            if (state == PlayerState.Attack || state == PlayerState.Thrust || state == PlayerState.Stun) poseHold = 0f;   // 振る・突く・怯むと構えの溜めはやり直し
            Data.state = state;
            Data.dir = dir;
            OnStateChanged?.Invoke(state, dir);
        }

        // 移動を進め、跳ねている高さを返す
        float UpdateStep()
        {
            if (!IsStepping) return 0f;
            float t = stepDuration > 0f ? Mathf.Clamp01((Time.time - stepStart) / stepDuration) : 1f;
            Data.position = Mathf.Lerp(stepFrom, Data.targetPosition, t);
            if (t >= 1f) IsStepping = false;
            return stepHeight * 4f * t * (1f - t);   // 放物線で跳ぶ
        }

        public void Render()
        {
            if (InResult)
            {
                // 決着演出中は決まった位置に立ったまま（のけぞり・移動はしない）
                StepJump = 0f;
                transform.SetPositionAndRotation(resultPos, baseRotation);
                return;
            }

            StepJump = UpdateStep();

            bool stunned = Data.state == PlayerState.Stun;
            lean = Mathf.Lerp(lean, stunned ? 1f : 0f, 1f - Mathf.Exp(-leanSpeed * Time.deltaTime));
            transform.rotation = baseRotation * Quaternion.Euler(-leanAngle * lean, 0f, 0f);

            // position 0で自分の端、1で相手の端。のけぞり中は後ろに下がる
            transform.position = Vector3.Lerp(edgePos, opponentEdgePos, Data.position)
                               - baseRotation * Vector3.forward * (leanBack * lean);

            // 仮表示：負け=黒 怯み=黄 攻撃=赤 突き=マゼンタ ガード=青 通常=白
            if (rend == null) return;
            rend.material.color = Lost ? Color.black : Data.state switch
            {
                PlayerState.Stun   => Color.yellow,
                PlayerState.Attack => Color.red,
                PlayerState.Thrust => Color.magenta,
                PlayerState.Guard  => Color.blue,
                _                  => Color.white,
            };
        }
    }
}
