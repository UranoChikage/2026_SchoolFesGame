using UnityEngine;

namespace Pirates
{
    /// <summary>エイムアシストが吸い付く対象（敵船、海に浮かぶアイテムなど）</summary>
    public interface IAimTarget
    {
        /// <summary>狙う位置（ワールド）</summary>
        Vector3 AimPosition { get; }

        /// <summary>偏差に使う速度（ワールド）。動かない対象は zero</summary>
        Vector3 AimVelocity { get; }

        /// <summary>いまアシストの対象にしてよいか（沈んだ敵など、狙っても意味のないものは false）</summary>
        bool CanAssist { get; }
    }
}
