namespace Chambara
{
    /// <summary>
    /// キャラ選択画面で決めた内容。シーンをまたいで ChambaraManager に渡す
    /// </summary>
    public static class ChambaraSelection
    {
        public const int PlayerCount = 2;

        public static bool HasValue { get; private set; }
        public static readonly string[] PlayerModelNames = new string[PlayerCount];
        public static readonly string[] SwordModelNames = new string[PlayerCount];

        public static void Set(int player, string playerModel, string swordModel)
        {
            PlayerModelNames[player] = playerModel;
            SwordModelNames[player] = swordModel;
            HasValue = true;
        }

        public static void Clear()
        {
            HasValue = false;
            for (int i = 0; i < PlayerCount; i++)
            {
                PlayerModelNames[i] = null;
                SwordModelNames[i] = null;
            }
        }
    }
}
