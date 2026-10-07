namespace Chambara
{
    public class Player
    {
        public PlayerData Data { get; private set; }
        public Move Move { get; private set; }

        private PlayerConfig config;

        public Player(PlayerConfig config,IChambaraInputContext inputContext)
        {
            this.config = config;
            Data = new PlayerData();
            Move = new Move(config, Data);
        }

        public void Tick(float f)
        {
            Move.Tick(f);
        }
    }
}
