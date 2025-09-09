using System.Collections.Concurrent;
using BattleshipsGame.Models;

namespace BattleshipsGame.Services
{
    public class ThreadPoolGameProcessor
    {
        private readonly ConcurrentQueue<GameProcessingTask> _processingQueue = new();
        private readonly SemaphoreSlim _processingSemaphore = new(Environment.ProcessorCount, Environment.ProcessorCount);
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        
        public ThreadPoolGameProcessor()
        {
            // Start background processing
            _ = Task.Run(ProcessQueueAsync);
        }

        public async Task QueueGameProcessing(string gameId, GameProcessingType type, object? data = null)
        {
            var task = new GameProcessingTask
            {
                GameId = gameId,
                Type = type,
                Data = data,
                QueuedAt = DateTime.UtcNow
            };

            _processingQueue.Enqueue(task);
            
            // Process immediately using thread pool
            await Task.Run(async () => await ProcessSingleTask(task));
        }

        private async Task ProcessQueueAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_processingQueue.TryDequeue(out var task))
                {
                    // Use thread pool for processing
                    _ = Task.Run(async () => await ProcessSingleTask(task), _cancellationTokenSource.Token);
                }
                
                await Task.Delay(100, _cancellationTokenSource.Token);
            }
        }

        private async Task ProcessSingleTask(GameProcessingTask task)
        {
            await _processingSemaphore.WaitAsync(_cancellationTokenSource.Token);
            
            try
            {
                // Simulate different types of game processing
                switch (task.Type)
                {
                    case GameProcessingType.AIMove:
                        await ProcessAIMove(task);
                        break;
                    case GameProcessingType.GameAnalytics:
                        await ProcessGameAnalytics(task);
                        break;
                    case GameProcessingType.LeaderboardUpdate:
                        await ProcessLeaderboardUpdate(task);
                        break;
                }
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }

        private async Task ProcessAIMove(GameProcessingTask task)
        {
            // Simulate AI thinking time
            await Task.Delay(Random.Shared.Next(500, 2000));
            
            // AI logic would go here
            Console.WriteLine($"Processed AI move for game {task.GameId}");
        }

        private async Task ProcessGameAnalytics(GameProcessingTask task)
        {
            // Simulate analytics processing
            await Task.Delay(Random.Shared.Next(100, 500));
            
            // Analytics logic would go here
            Console.WriteLine($"Processed analytics for game {task.GameId}");
        }

        private async Task ProcessLeaderboardUpdate(GameProcessingTask task)
        {
            // Simulate leaderboard calculation
            await Task.Delay(Random.Shared.Next(200, 800));
            
            // Leaderboard update logic would go here
            Console.WriteLine($"Updated leaderboard for game {task.GameId}");
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _processingSemaphore.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }

    public class GameProcessingTask
    {
        public string GameId { get; set; } = "";
        public GameProcessingType Type { get; set; }
        public object? Data { get; set; }
        public DateTime QueuedAt { get; set; }
    }

    public enum GameProcessingType
    {
        AIMove,
        GameAnalytics,
        LeaderboardUpdate
    }
}
