using Microsoft.AspNetCore.Mvc;
using BattleshipsGame.Services;
using BattleshipsGame.Models;

namespace BattleshipsGame.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameController : ControllerBase
    {
        private readonly GameService _gameService;
        private readonly GameMatchmakingService _matchmakingService;
        private readonly ILogger<GameController> _logger;

        public GameController(
            GameService gameService, 
            GameMatchmakingService matchmakingService,
            ILogger<GameController> logger)
        {
            _gameService = gameService;
            _matchmakingService = matchmakingService;
            _logger = logger;
        }

        // AJAX endpoint for game statistics
        [HttpGet("stats")]
        public async Task<ActionResult<GameStatsDto>> GetGameStats()
        {
            try
            {
                // Simulate async data retrieval (could be from database)
                await Task.Delay(100); // Simulate network delay
                
                var stats = new GameStatsDto
                {
                    ActiveGames = GetActiveGamesCount(),
                    PlayersOnline = GetOnlinePlayersCount(),
                    GamesToday = GetTodayGamesCount()
                };

                _logger.LogInformation("Game statistics requested: {Stats}", stats);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving game statistics");
                return StatusCode(500, "Internal server error");
            }
        }

        // AJAX endpoint for game history
        [HttpGet("history/{playerId}")]
        public async Task<ActionResult<List<GameHistoryDto>>> GetGameHistory(string playerId)
        {
            try
            {
                await Task.Delay(50);
                
                // In real application, this would query database
                var history = GenerateGameHistory(playerId);
                
                _logger.LogInformation("Game history requested for player {PlayerId}", playerId);
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving game history for player {PlayerId}", playerId);
                return StatusCode(500, "Internal server error");
            }
        }

        // AJAX endpoint for leaderboard
        [HttpGet("leaderboard")]
        public async Task<ActionResult<List<PlayerRankingDto>>> GetLeaderboard()
        {
            try
            {
                await Task.Delay(200);
                
                var leaderboard = GenerateLeaderboard();
                
                _logger.LogInformation("Leaderboard requested");
                return Ok(leaderboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leaderboard");
                return StatusCode(500, "Internal server error");
            }
        }

        // AJAX endpoint for real-time player count
        [HttpGet("player-count")]
        public async Task<ActionResult<int>> GetPlayerCount()
        {
            try
            {
                await Task.Delay(10);
                var count = GetOnlinePlayersCount();
                
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving player count");
                return StatusCode(500, "Internal server error");
            }
        }

        // Helper methods (in real app, these would use database/cache)
        private int GetActiveGamesCount()
        {
            // In real implementation, count from _gameService or database
            return Random.Shared.Next(10, 50);
        }

        private int GetOnlinePlayersCount()
        {
            // In real implementation, count from connection tracking
            return Random.Shared.Next(20, 200);
        }

        private int GetTodayGamesCount()
        {
            // In real implementation, query database for today's games
            return Random.Shared.Next(100, 500);
        }

        private List<GameHistoryDto> GenerateGameHistory(string playerId)
        {
            var history = new List<GameHistoryDto>();
            var random = Random.Shared;
            
            for (int i = 0; i < random.Next(5, 15); i++)
            {
                history.Add(new GameHistoryDto
                {
                    GameId = Guid.NewGuid().ToString(),
                    OpponentName = $"Player{random.Next(1000, 9999)}",
                    Result = random.Next(2) == 0 ? "Win" : "Loss",
                    Duration = TimeSpan.FromMinutes(random.Next(3, 15)),
                    PlayedAt = DateTime.UtcNow.AddDays(-random.Next(0, 30)),
                    ShotsFired = random.Next(15, 45),
                    ShotsHit = random.Next(8, 25)
                });
            }
            
            return history.OrderByDescending(h => h.PlayedAt).ToList();
        }

        private List<PlayerRankingDto> GenerateLeaderboard()
        {
            var leaderboard = new List<PlayerRankingDto>();
            var names = new[] { "Admiral_Smith", "Captain_Jones", "Sailor_Brown", "Commander_Wilson", "Lieutenant_Davis" };
            var random = Random.Shared;
            
            for (int i = 0; i < names.Length; i++)
            {
                leaderboard.Add(new PlayerRankingDto
                {
                    Rank = i + 1,
                    PlayerName = names[i],
                    GamesWon = random.Next(50, 200),
                    GamesPlayed = random.Next(100, 300),
                    WinRate = Math.Round(random.NextDouble() * 0.4 + 0.4, 2) // 40-80% win rate
                });
            }
            
            return leaderboard.OrderBy(p => p.Rank).ToList();
        }
    }

    // DTOs for API responses
    public class GameStatsDto
    {
        public int ActiveGames { get; set; }
        public int PlayersOnline { get; set; }
        public int GamesToday { get; set; }
    }

    public class GameHistoryDto
    {
        public string GameId { get; set; } = "";
        public string OpponentName { get; set; } = "";
        public string Result { get; set; } = "";
        public TimeSpan Duration { get; set; }
        public DateTime PlayedAt { get; set; }
        public int ShotsFired { get; set; }
        public int ShotsHit { get; set; }
    }

    public class PlayerRankingDto
    {
        public int Rank { get; set; }
        public string PlayerName { get; set; } = "";
        public int GamesWon { get; set; }
        public int GamesPlayed { get; set; }
        public double WinRate { get; set; }
    }
}