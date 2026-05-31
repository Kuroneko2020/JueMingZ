namespace JueMingZ.GameState
{
    public sealed class GameStateReadResult
    {
        public GameStateReadStatus Status { get; set; }
        public GameStateSnapshot Snapshot { get; set; }
        public string Message { get; set; }

        public static GameStateReadResult FromSnapshot(GameStateSnapshot snapshot)
        {
            return new GameStateReadResult
            {
                Status = GameStateReadStatus.Succeeded,
                Snapshot = snapshot,
                Message = string.Empty
            };
        }

        public static GameStateReadResult Unavailable(string message)
        {
            return new GameStateReadResult
            {
                Status = GameStateReadStatus.Unavailable,
                Snapshot = GameStateSnapshot.Unknown(message),
                Message = message ?? string.Empty
            };
        }
    }
}
