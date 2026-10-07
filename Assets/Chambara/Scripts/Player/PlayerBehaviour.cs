using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// プレイヤーの表示と入力元の窓口。ロジックは持たない
    /// </summary>
    public class PlayerBehaviour : MonoBehaviour
    {
        IChambaraInputContext input;
        Renderer rend;

        public void Init(int playerIndex, Transform opponentPos)
        {
            transform.LookAt(opponentPos);
            rend = GetComponentInChildren<Renderer>();
            input = GetComponent<IChambaraInputContext>();
            input?.Init(playerIndex);
        }

        public PlayerInput ReadInput() => input != null ? input.Read() : PlayerInput.None;

        public void Render(ChambaraSimulator sim, Fighter f)
        {
            // 仮表示：怯み=黄 攻撃=赤 ガード=青 通常=白
            if (rend == null) return;
            rend.material.color =
                sim.Frame < f.stunUntil ? Color.yellow :
                f.attackDir >= 0 ? Color.red :
                sim.IsGuarding(f) ? Color.blue : Color.white;
        }
    }
}
