namespace BattleshipsGame.Services
{
    public class BarrierSynchronizationService
    {
        private readonly Dictionary<string, Barrier> _gameBarriers = new();
        private readonly object _barrierLock = new object();

        public async Task WaitForAllPlayersReady(string gameId, int expectedPlayers = 2)
        {
            Barrier barrier;
            
            lock (_barrierLock)
            {
                if (!_gameBarriers.TryGetValue(gameId, out barrier))
                {
                    barrier = new Barrier(expectedPlayers);
                    _gameBarriers[gameId] = barrier;
                }
            }

            // Use barrier to synchronize players
            await Task.Run(() =>
            {
                try
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(30)); // 30 second timeout
                }
                catch (BarrierPostPhaseException)
                {
                    // Handle barrier completion
                }
                catch (TimeoutException)
                {
                    // Handle timeout
                    throw new InvalidOperationException("Timeout waiting for all players to be ready");
                }
            });

            // Clean up barrier after use
            lock (_barrierLock)
            {
                if (_gameBarriers.ContainsKey(gameId))
                {
                    barrier.Dispose();
                    _gameBarriers.Remove(gameId);
                }
            }
        }

        public void RemoveGameBarrier(string gameId)
        {
            lock (_barrierLock)
            {
                if (_gameBarriers.TryGetValue(gameId, out var barrier))
                {
                    barrier.Dispose();
                    _gameBarriers.Remove(gameId);
                }
            }
        }
    }
}