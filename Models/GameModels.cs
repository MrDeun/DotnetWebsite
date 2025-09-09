using System.Collections.Concurrent;

namespace BattleshipsGame.Models
{
    public enum GameState
    {
        WaitingForPlayers,
        PlacingShips,
        Playing,
        GameOver
    }

    public enum CellState
    {
        Water,
        Ship,
        Hit,
        Miss,
        Sunk
    }

    public class Game
    {
        public string GameId { get; set; } = string.Empty;
        public Player? Player1 { get; set; }
        public Player? Player2 { get; set; }
        public GameState State { get; set; }
        public string CurrentPlayerTurn { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? GameStartedAt { get; set; }
        public DateTime? GameEndedAt { get; set; }
        
        // Thread synchronization - using locks
        private readonly object _gameLock = new object();
        public object GameLock => _gameLock;
    }

    public class Player
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Board Board { get; set; } = new Board();
        public bool IsReady { get; set; }
        public int ShipsSunk { get; set; }
        public int ShotsFired { get; set; }
        public int ShotsHit { get; set; }
    }

    public class Board
    {
        public CellState[,] Grid { get; set; } = new CellState[10, 10];
        public List<Ship> Ships { get; set; } = new List<Ship>();
        
        public void InitializeBoard()
        {
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Grid[i, j] = CellState.Water;
                }
            }
        }
    }

    public class Ship
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Size { get; set; }
        public List<(int X, int Y)> Positions { get; set; } = new List<(int, int)>();
        public bool IsSunk { get; set; }
    }

    public class ShotResult
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsHit { get; set; }
        public bool IsShipSunk { get; set; }
        public bool IsGameOver { get; set; }
        public string? SunkShipName { get; set; }
        public string ShooterConnectionId { get; set; } = string.Empty;
    }
}