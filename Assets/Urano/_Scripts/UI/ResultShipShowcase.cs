using System.Collections.Generic;
using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// リザルトシーンで、バトルから残っている敵船（PoolManager のプール）のシルエットだけを消す。
    /// 船の動き・位置はそのまま。リザルトのシーンに1つ置く。
    /// タイトルへ戻るときのプール解除は ResultSequence.GoTitle が行う。
    /// </summary>
    public class ResultShipShowcase : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("シルエットを外すとき、見た目のレイヤーをこれに戻す（0=Default）")]
        int displayLayer = 0;

        void Start()
        {
            // シルエットは EnemyVisual レイヤーのメッシュに掛かる。レイヤーを戻すだけで外れる
            int silhouette = LayerMask.NameToLayer("EnemyVisual");
            if (silhouette < 0) return;

            List<EnemyShip> ships = PoolManager.FindAll<EnemyShip>();
            foreach (EnemyShip ship in ships)
            {
                foreach (MeshRenderer r in ship.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.gameObject.layer == silhouette) r.gameObject.layer = displayLayer;
                }
            }
        }
    }
}
