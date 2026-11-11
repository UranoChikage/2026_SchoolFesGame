using Common;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("攻撃（刃先の向きが swingTime 秒以内に swingAngle 度以上動いたら、動いた向きに振る）")]
        [Tooltip("この時間（秒）以内に動いた分だけを見る")] public float swingTime = 0.3f;
        [Tooltip("刃先の向きがこの角度（度）以上動いたら振ったとみなす")] public float swingAngle = 90f;
        [Tooltip("振った後、この時間（秒）は次の振りを受け付けない（1回の大振りで2回出ないようにする）")] public float swingCooldown = 0.3f;
        [Tooltip("振った入力を覚えておく時間（秒）。硬直中に振っても後から出ないようにする")] public float swingBuffer = 0.1f;

        [Header("突き")]
        [Tooltip("角速度（ひねりを除く）がこれ以下のときだけ突きになる（≒角速度0）")] public float thrustMaxAngularSpeed = 0.3f;
        [Tooltip("重力を除いた加速度（G）がこれを超えたら突いたとみなす")] public float thrustThreshold = 2f;
        [FormerlySerializedAs("swingRearm"), Range(0.1f, 1f), Tooltip("加速度が thrustThreshold のこの割合まで下がったら次の突きを受け付ける")] public float thrustRearm = 0.5f;
        [Tooltip("重力の向きを推定し直す速さ。加速度の大きさが1G付近（ほぼ静止）のときだけ寄せる")] public float gravityFollow = 5f;
        [Range(0f, 1f), Tooltip("加速度の向きと前方の cos がこれ以上なら前へ突いたとみなす")] public float thrustForwardDot = 0.7f;
        [Tooltip("引いたときに突きが出る（前へ突いても出ない）場合はチェックする（加速度の符号を反転）")] public bool invertThrustAccel = false;

        [Header("ガード")]
        public SwitchButton guardButton = SwitchButton.Confirm;   // Joy-Con では ZR/ZL
        [Tooltip("刃と正面のなす角が 90度±この角度 ならガードの構え")] public float perpendicularTolerance = 30f;
        [Tooltip("刃先の角度（真上=0）がこれ以内なら上向き、180-これ以上なら下向き。間は水平")] public float verticalRange = 45f;
        [Tooltip("上向きのとき、真上からこの角度以上傾けないと左右が決まらない")] public float sideDeadZone = 5f;

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
