using UnityEngine;

namespace Chambara
{
    public interface IChambaraInputContext
    {
        ChambaraInputType InputType { get; }
        int AttackIndex{ get; }
    }

    public enum ChambaraInputType
    {
        None,
        Idle,
        Attack,
        Guard,
    }
}
