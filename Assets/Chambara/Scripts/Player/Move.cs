using System;

namespace Chambara
{
    public class Move 
    {
        PlayerConfig playerConfig;
        PlayerData playerData;

        public event Action<float> OnMove;

        public Move(PlayerConfig config,PlayerData playerData)
        {
            playerConfig = config;
            this.playerData = playerData;
        }

        public void Tick(float f)
        {
            if (playerData.actionType == ActionType.Move)
            {
                playerData.distance += (playerConfig.moveSpeed/f);
                OnMove?.Invoke(playerData.distance);
            }
        }
    }
}
