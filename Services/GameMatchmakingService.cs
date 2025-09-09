using System.Collections.Concurrent;
using BattleshipsGame.Models;

namespace BattleshipsGame.Services
{
    public class GameMatchmakingService
    {
        private readonly ConcurrentQueue<string> _waitingPlayers = new();
        private readonly SemaphoreSlim _matchmakingSemaphore = new(1, 1); // Semaphore for matchmaking
        private readonly GameService _gameService;
        
        public GameMatchmakingService(GameService gameService)
        {
            _gameService = gameService;
        }

        public async Task<string?> FindOrCreateGame(string connectionId, string playerName)
        {
            await _matchmakingSemaphore.WaitAsync(); // Semaphore usage
            try
            {
                // Try to match with waiting player
                if (_waitingPlayers.TryDequeue(out var waitingPlayerId))
                {
                    // Create game with two players
                    var game = await _gameService.CreateGameWithPlayers(waitingPlayerId, connectionId, playerName);
                    return game.GameId;
                }
                else
                {
                    // Add to waiting queue
                    _waitingPlayers.Enqueue(connectionId);
                    return null; // Still waiting for opponent
                }
            }
            finally
            {
                _matchmakingSemaphore.Release(); // Always release semaphore
            }
        }

        public async Task RemoveFromQueue(string connectionId)
        {
            await _matchmakingSemaphore.WaitAsync();
            try
            {
                // Remove player from waiting queue if disconnected
                var tempQueue = new ConcurrentQueue<string>();
                while (_waitingPlayers.TryDequeue(out var playerId))
                {
                    if (playerId != connectionId)
                        tempQueue.Enqueue(playerId);
                }
                
                // Replace queue
                while (tempQueue.TryDequeue(out var playerId))
                {
                    _waitingPlayers.Enqueue(playerId);
                }
            }
            finally
            {
                _matchmakingSemaphore.Release();
            }
        }
    }
}