using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 海のメッシュを追従対象の真下へ動かし、メッシュの端が見えないようにする。
    /// 波やノーマルマップはワールド座標で計算されるので、メッシュが動いても模様は海に固定されたまま。
    /// 親子にはせず位置だけ追従する（船の傾きや沈没に引きずられない）。
    /// </summary>
    public class SeaFollower : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("追従先。未設定ならプレイヤー船（PlayerTarget）を自動で追う")]
        Transform target;
        [SerializeField]
        [Tooltip("この間隔(m)の格子に位置をそろえる。メッシュの頂点間隔と同じにすると、頂点がずれて波がちらつかない。0ならそろえない")]
        float gridSize = 0f;

        /// <summary>追従先を差し替える（演出中に別の場所の海を使うときなど）。nullでプレイヤーへ戻る</summary>
        public Transform Target
        {
            get => target;
            set => target = value;
        }

        float fixedY;

        void Awake()
        {
            fixedY = transform.position.y;
        }

        void LateUpdate()
        {
            Transform t = target != null ? target : (PlayerTarget.Current != null ? PlayerTarget.Current.transform : null);
            if (t == null) return;

            Vector3 p = t.position;
            if (gridSize > 0f)
            {
                p.x = Mathf.Round(p.x / gridSize) * gridSize;
                p.z = Mathf.Round(p.z / gridSize) * gridSize;
            }
            transform.position = new Vector3(p.x, fixedY, p.z);
        }
    }
}
