using Common;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// JoyconInputContext の調整値。1P・2P で共通のアセットを使う
    /// 操作の説明は JoyconInputContext を参照
    /// </summary>
    [CreateAssetMenu(fileName = "JoyconInput", menuName = "Chambara/JoyconInputConfig")]
    public class JoyconInputConfig : ScriptableObject
    {
        public enum Axis { Right, Left, Up, Down, Forward, Back }

        [Header("持ち方（Joy-Conのどの軸がどちらを向くか。Joy-Con自身の座標）")]
        [Tooltip("刃が伸びる方向")] public Axis bladeAxis = Axis.Up;     // ZR/ZL の端（Joy-Conの長い方の軸）
        [Tooltip("剣を真上に立てたとき相手の方を向く面")] public Axis frontAxis = Axis.Back;   // 左右が逆なら bladeAxis=Down / frontAxis=Forward にする

        [Header("正面の合わせ直し")]
        public SwitchButton recenterButton = SwitchButton.Cancel;
        [Tooltip("合わせ直すときに構える剣の角度（真上=0、正面=90）")] public float calibrationAngle = 90f;   // Cancel を押すと剣先が正面を向く

        [Header("攻撃（刃先が速く動き出してから swingAngle 度以上回り、一番速いときに swingMinPeakSpeed を超えていたら、回った向きに振る）")]
        [Tooltip("刃先がこの速さ（度/秒）を超えたら振り始めとみなす")] public float swingStartSpeed = 300f;
        [Tooltip("刃先がこの速さ（度/秒）を下回ったら振り終わりとみなす")] public float swingEndSpeed = 150f;
        [Tooltip("振りの途中で一番速いときにこの速さ（度/秒）を超えていないと振りにしない。振りかぶりや構え直しで出るなら上げ、振っても出ないなら下げる")] public float swingMinPeakSpeed = 450f;
        [Tooltip("振り始めからこの角度（度）以上回ったら振ったとみなす。小さいほど早く出るが、方向がぶれやすくなる")] public float swingAngle = 45f;
        [Tooltip("1回の振りとして見る最長の時間（秒）")] public float swingTime = 0.3f;
        [Tooltip("振った・突いた後、この時間（秒）以内に始まった動きは振りにしない（構えに戻す動きで出ないようにする）")] public float swingCooldown = 0.3f;
        [Tooltip("振り・突きの入力を覚えておく時間（秒）。硬直・移動が明ける直前に振っても出る")] public float swingBuffer = 0.1f;

        [Header("突き（重力を除いた加速度をためた「前へ出た勢い」が thrustImpulse を超えたら突く）")]
        [Tooltip("前へ出た勢い（G・秒。0.1 ≒ 秒速1m）がこれを超えたら突いたとみなす。出にくければ下げ、勝手に出るなら上げる")] public float thrustImpulse = 0.1f;
        [Tooltip("勢いをためる時間（秒）。これより前の加速度は消えていく")] public float thrustWindow = 0.15f;
        [Tooltip("刃先がこの速さ（度/秒）より速く回っている間は突きにしない（振りと区別する）")] public float thrustMaxTipSpeed = 250f;
        [Range(0f, 1f), Tooltip("勢いの向きと前方の cos がこれ以上なら前へ突いたとみなす")] public float thrustForwardDot = 0.5f;
        [Tooltip("振った後、この時間（秒）突きを受け付けない。振り終わりの急停止で突きが出ないようにする")] public float thrustLockout = 0.3f;
        [Tooltip("重力の向きを推定し直す速さ。ほぼ静止しているときだけ寄せる")] public float gravityFollow = 5f;
        [Tooltip("引いたときに突きが出る（前へ突いても出ない）場合はチェックする（加速度の符号を反転）")] public bool invertThrustAccel = false;

        [Header("ガード")]
        public SwitchButton guardButton = SwitchButton.Confirm;   // Joy-Con では ZR/ZL
        [Tooltip("刃と正面のなす角が 90度±この角度 ならガードの構え")] public float perpendicularTolerance = 45f;
        [Tooltip("刃先の角度（真上=0）がこれ以内なら上向き、180-これ以上なら下向き。間は水平")] public float verticalRange = 45f;
        [Tooltip("上向きのとき、真上からこの角度以上傾けないと左右が決まらない")] public float sideDeadZone = 5f;
        [Tooltip("今の構えから抜けるときだけ、境目をこの角度（度）広げる。境目で手がぶれても構えが切り替わらないようにする")] public float guardHysteresis = 10f;
        [Tooltip("構えが外れたり別の向きになったりしても、この時間（秒）続くまでは今の構えを保つ")] public float guardGraceTime = 0.1f;

        [Header("移動")]
        public bool useStick = true;
        [Tooltip("スティックをこれ以上倒したら前進・後退")] public float stickThreshold = 0.5f;
        // ボタンでの移動は使わない（スティックのみ）
        // public bool useButtons = true;
        // public SwitchButton frontButton = SwitchButton.Up;
        // public SwitchButton backButton = SwitchButton.Down;

        public static Vector3 AxisVector(Axis a) => a switch
        {
            Axis.Right   => Vector3.right,
            Axis.Left    => Vector3.left,
            Axis.Up      => Vector3.up,
            Axis.Down    => Vector3.down,
            Axis.Forward => Vector3.forward,
            _            => Vector3.back,
        };

        private void OnValidate()
        {
            if (Mathf.Abs(Vector3.Dot(AxisVector(bladeAxis), AxisVector(frontAxis))) > 0.5f)
                Debug.LogWarning($"{name}: bladeAxis と frontAxis は直交する軸にしてください");
        }
    }
}
