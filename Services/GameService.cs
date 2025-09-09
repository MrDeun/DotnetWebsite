using System.Collections.Concurrent;
using BattleshipsGame.Models;

namespace BattleshipsGame.Services
{
    public class GameService
    {
        private readonly ConcurrentDictionary<string, Game> _games = new();
        private readonly ConcurrentDictionary<string, string> _playerToGame = new();
        private readonly Mutex _globalGameMutex = new(false); // Mutex for global operations
        
        // Thread pool for AI operations (if implementing AI opponent)
        private readonly SemaphoreSlim _aiProcessingSemaphore = new(3, 3); // Limit concurrent AI operations

        public async Task<Game> CreateGameWithPlayers(string player1Id, string player2Id, string player2Name)
        {
            _globalGameMutex.WaitOne(); // Mutex usage
            try
            {
                var gameId = Guid.NewGuid().ToString();
                var game = new Game
                {
                    GameId = gameId,
                    Player1 = new Player { ConnectionId = player1Id, Name = "Waiting Player" },
                    Player2 = new Player { ConnectionId = player2Id, Name = player2Name },
                    State = GameState.PlacingShips,
                    CreatedAt = DateTime.UtcNow
                };

                game.Player1.Board.InitializeBoard();
                game.Player2.Board.InitializeBoard();

                _games[gameId] = game;
                _playerToGame[player1Id] = gameId;
                _playerToGame[player2Id] = gameId;

                return game;
            }
            finally
            {
                _globalGameMutex.ReleaseMutex(); // Always release mutex
            }
        }

        public async Task<ShotResult> ProcessShot(string connectionId, string gameId, int x, int y)
        {
            if (!_games.TryGetValue(gameId, out var game))
                throw new InvalidOperationException("Game not found");

            // Lock game for thread safety
            lock (game.GameLock) // Lock usage
            {
                var shooter = GetPlayer(game, connectionId);
                var opponent = GetOpponent(game, connectionId);
                
                if (game.CurrentPlayerTurn != connectionId)
                    throw new InvalidOperationException("Not your turn");

                shooter.ShotsFired++;
                
                var cellState = opponent.Board.Grid[x, y];
                var result = new ShotResult 
                { 
                    X = x, 
                    Y = y, 
                    ShooterConnectionId = connectionId 
                };

                if (cellState == CellState.Ship)
                {
                    opponent.Board.Grid[x, y] = CellState.Hit;
                    result.IsHit = true;
                    shooter.ShotsHit++;

                    // Check if ship is sunk
                    var sunkShip = CheckAndMarkSunkShips(opponent.Board, x, y);
                    if (sunkShip != null)
                    {
                        result.IsShipSunk = true;
                        result.SunkShipName = sunkShip.Name;
                        shooter.ShipsSunk++;

                        // Check game over
                        if (opponent.Board.Ships.All(s => s.IsSunk))
                        {
                            game.State = GameState.GameOver;
                            game.GameEndedAt = DateTime.UtcNow;
                            result.IsGameOver = true;
                        }
                    }
                }
                else if (cellState == CellState.Water)
                {
                    opponent.Board.Grid[x, y] = CellState.Miss;
                    result.IsHit = false;
                    
                    // Switch turns
                    game.CurrentPlayerTurn = game.CurrentPlayerTurn == game.Player1?.ConnectionId 
                        ? game.Player2?.ConnectionId ?? "" 
                        : game.Player1?.ConnectionId ?? "";
                }

                return result;
            }
        }

        // Thread pool usage for AI processing (example)
        public async Task ProcessAIMove(string gameId)
        {
            await _aiProcessingSemaphore.WaitAsync();
            try
            {
                await Task.Run(() => // Using thread pool
                {
                    // AI logic here - runs on thread pool thread
                    Thread.Sleep(1000); // Simulate AI thinking time
                    // Generate AI move...
                });
            }
            finally
            {
                _aiProcessingSemaphore.Release();
            }
        }

        private Ship? CheckAndMarkSunkShips(Board board, int hitX, int hitY)
        {
            foreach (var ship in board.Ships.Where(s => !s.IsSunk))
            {
                if (ship.Positions.Contains((hitX, hitY)))
                {
                    // Check if all ship positions are hit
                    bool allHit = ship.Positions.All(pos => 
                        board.Grid[pos.X, pos.Y] == CellState.Hit);
                    
                    if (allHit)
                    {
                        ship.IsSunk = true;
                        // Mark as sunk on board
                        foreach (var pos in ship.Positions)
                        {
                            board.Grid[pos.X, pos.Y] = CellState.Sunk;
                        }
                        return ship;
                    }
                    break;
                }
            }
            return null;
        }

        private Player GetPlayer(Game game, string connectionId)
        {
            return game.Player1?.ConnectionId == connectionId ? game.Player1 : game.Player2
                ?? throw new InvalidOperationException("Player not found");
        }

        private Player GetOpponent(Game game, string connectionId)
        {
            return game.Player1?.ConnectionId == connectionId ? game.Player2 : game.Player1
                ?? throw new InvalidOperationException("Opponent not found");
        }

        public Game? GetGame(string gameId)
        {
            _games.TryGetValue(gameId, out var game);
            return game;
        }

        public string? GetGameIdForPlayer(string connectionId)
        {
            _playerToGame.TryGetValue(connectionId, out var gameId);
            return gameId;
        }

        public void RemovePlayer(string connectionId)
        {
            if (_playerToGame.TryRemove(connectionId, out var gameId))
            {
                if (_games.TryGetValue(gameId, out var game))
                {
                    lock (game.GameLock)
                    {
                        // Mark game as abandoned or remove if no players left
                        if (game.Player1?.ConnectionId == connectionId)
                            game.Player1 = null;
                        if (game.Player2?.ConnectionId == connectionId)
                            game.Player2 = null;

                        if (game.Player1 == null && game.Player2 == null)
                        {
                            _games.TryRemove(gameId, out _);
                        }
                    }
                }
            }
        }
    }
}