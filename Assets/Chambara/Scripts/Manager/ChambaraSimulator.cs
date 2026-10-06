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

            switch (p.actionType)
            {
                case ActionType.Move:
                    Move(p);
                    break;
                case ActionType.Attack:
                    // Attack‚Ìˆ—
                    break;
                case ActionType.Guard:
                    // Guard‚Ìˆ—
                    break;
                case ActionType.Stun:
                    // Stun‚Ìˆ—
                    break;
            }
        }

        private void Move(PlayerData p)
        {
            // Move‚Ìˆ—


        }

        public void SetState(PlayerData p, ActionType actionType, ActionPhase phase)
        {
            p.actionType = actionType;
            p.phase = phase;
            p.frame = 0;
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
