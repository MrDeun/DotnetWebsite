using Microsoft.AspNetCore.SignalR;
using BattleshipsGame.Services;
using BattleshipsGame.Models;

namespace BattleshipsGame.Hubs
{
    public class GameHub : Hub
    {
        private readonly GameService _gameService;
        private readonly GameMatchmakingService _matchmakingService;

        public GameHub(GameService gameService, GameMatchmakingService matchmakingService)
        {
            _gameService = gameService;
            _matchmakingService = matchmakingService;
        }

        // WebSocket methods
        public async Task JoinMatchmaking(string playerName)
        {
            var gameId = await _matchmakingService.FindOrCreateGame(Context.ConnectionId, playerName);
            
            if (gameId != null)
            {
                // Game found/created
                await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
                var game = _gameService.GetGame(gameId);
                await Clients.Group(gameId).SendAsync("GameFound", game);
            }
            else
            {
                // Still waiting for opponent
                await Clients.Caller.SendAsync("WaitingForOpponent");
            }
        }

        public async Task PlaceShips(List<Ship> ships)
        {
            var gameId = _gameService.GetGameIdForPlayer(Context.ConnectionId);
            if (gameId == null) return;

            var game = _gameService.GetGame(gameId);
            if (game == null) return;

            // Place ships logic here...
            await Clients.Group(gameId).SendAsync("ShipsPlaced", Context.ConnectionId);
        }

        public async Task FireShot(int x, int y)
        {
            var gameId = _gameService.GetGameIdForPlayer(Context.ConnectionId);
            if (gameId == null) return;

            try
            {
                var result = await _gameService.ProcessShot(Context.ConnectionId, gameId, x, y);
                await Clients.Group(gameId).SendAsync("ShotResult", result);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _matchmakingService.RemoveFromQueue(Context.ConnectionId);
            _gameService.RemovePlayer(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}