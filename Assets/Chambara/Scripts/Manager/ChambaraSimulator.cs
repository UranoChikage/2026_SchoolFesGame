namespace Chambara
{
    public class ChambaraSimulator
    {
        public PlayerData playerData0;
        public PlayerData playerData1;

        public void Tick()
        {
            StepState(playerData0);
            StepState(playerData1);
        }


        public void StepState(PlayerData p)
        {
            if (p.actionType is ActionType.None) return;

            p.frame++;


        }

        public void Attack(int playerIndex, int attackIndex)
        {
            if (playerIndex == 0)
            {
                // playerData0‚ÌUŒ‚ˆ—
            }
            else if (playerIndex == 1)
            {
                // playerData1‚ÌUŒ‚ˆ—
            }
        }
    }
}
