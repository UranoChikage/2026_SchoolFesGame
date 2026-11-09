using UnityEngine;

namespace Common
{
    /// <summary>
    /// Joy-Conのジャイロでカーソルを動かすための共通部品（プレイヤー空間ジャイロ / Jibb Smart / GyroWiki）。
    /// Title の Select と Pirates の砲手が同じ動かし方になるようにここへまとめてある。
    ///
    /// ■ 考え方
    ///   角速度をマウスの移動量として積算する。姿勢→オイラー角は使わないので、持ち方がずれても位置は飛ばない。
    ///   重力は「ヨーとロールをどちら向きに合成するか」の判定にだけ使う。
    ///   縦持ち（ひねる操作＝ロール）でも横持ちでも同じコードで動く。
    ///
    /// ■ 使い方
    ///   プレイヤー（カーソル）ごとに1つ持つ。毎フレーム Rate を呼び、戻り値 × 感度 × Time.deltaTime を位置に足す。
    ///   位置のリセット（中央に戻す）は呼び出し側で位置を直接戻す。持ち方を変えたときは Reset で重力を取り直す。
    ///
    /// ■ 軸の対応（Unity座標系。バックエンド側で変換済み）
    ///     gyro.x = ピッチ（前後に倒す） / gyro.y = ヨー（上下軸まわり） / gyro.z = ロール（縦持ちではこれが横移動）
    /// </summary>
    public class GyroPointer
    {
        Vector3 gravity = Vector3.down;
        bool hasGravity;

        /// <summary>重力の推定を捨てる。繋ぎ直した／持ち方が変わったときに呼ぶ</summary>
        public void Reset() => hasGravity = false;

        /// <summary>
        /// 今フレームのカーソルの動き（度/秒相当）。x=右が+、y=上が+。
        /// 位置 += Rate(...) * 感度(px/度) * Time.deltaTime
        /// </summary>
        /// <param name="yawRelaxFactor">ローカル操作を許容する角度の広さ。1.41で±45度、2で±60度</param>
        /// <param name="gravityBlend">重力推定の追従速度。小さいほど安定するが遅れる</param>
        public Vector2 Rate(int index, float yawRelaxFactor = 2f, float gravityBlend = 0.02f)
        {
            Vector3 gyro = SwitchInput.I.AngularVelocity(index);
            UpdateGravity(index, gyro, gravityBlend);

            Vector3 g = gravity;

            // ヨーとロールだけの内積。ピッチが横移動に混ざるのを防ぐ
            float worldYaw = gyro.y * g.y + gyro.z * g.z;

            // 大きさはローカルのヨー+ロールの合成長、向きはワールドヨーの符号。
            // relax factor を超えて軸から外れた入力は、反転する前に 0 へ絞られる
            float magnitude = new Vector2(gyro.y, gyro.z).magnitude;
            float yaw = Mathf.Sign(worldYaw)
                      * Mathf.Min(Mathf.Abs(worldYaw) * yawRelaxFactor, magnitude);

            return new Vector2(-yaw, gyro.x);
        }

        // 加速度と角速度から重力方向を推定する（相補フィルタ）。
        // 加速度計は手振れや直線加速度も拾うので、角速度で回しながらゆっくり寄せる
        void UpdateGravity(int index, Vector3 gyro, float blend)
        {
            Vector3 accel = SwitchInput.I.Acceleration(index);
            if (accel.sqrMagnitude < 1e-6f) return;   // 値が来ていない

            if (!hasGravity)
            {
                gravity = (-accel).normalized;
                hasGravity = true;
                return;
            }

            // 角速度ぶんだけ重力ベクトルを逆回転させる
            float angle = gyro.magnitude * Time.deltaTime;
            if (angle > 0.0001f)
                gravity = Quaternion.AngleAxis(angle, -gyro.normalized) * gravity;

            // 加速度の向きへ少しずつ寄せる
            gravity = Vector3.Slerp(gravity, (-accel).normalized, blend).normalized;
        }
    }
}
