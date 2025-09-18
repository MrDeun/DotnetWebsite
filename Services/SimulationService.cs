using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using EcosystemSimulation.Models;
using EcosystemSimulation.Hubs;

namespace EcosystemSimulation.Services
{
    public class SimulationService : BackgroundService
    {
        private readonly IHubContext<SimulationHub> _hubContext;
        private readonly SimulationState _state;
        private readonly ThreadLocal<Random> _threadLocalRandom;
        private int _nextHareId = 1;
        private int _nextWolfId = 1;
        private int _currentCycle = 0; // Separate field for thread-safe cycle tracking

        // Thread synchronization primitives
        private readonly Mutex _stateMutex = new Mutex(); // Mutex for critical state operations

        private const int WorldWidth = 800;
        private const int WorldHeight = 600;

        // Configurable thread pool settings
        private readonly int _workerThreadCount;
        private readonly int _minWorkerThreads;
        private readonly int _maxWorkerThreads;
        private readonly int _minIOThreads;
        private readonly int _maxIOThreads;

        public SimulationService(IHubContext<SimulationHub> hubContext)
        {
            _hubContext = hubContext;
            _state = new SimulationState();
            _threadLocalRandom = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

            // Configure thread pool settings
            _workerThreadCount = Environment.ProcessorCount; // Default to CPU core count
            _minWorkerThreads = Math.Max(1, Environment.ProcessorCount / 2);
            _maxWorkerThreads = Environment.ProcessorCount * 2;
            _minIOThreads = 4;
            _maxIOThreads = Environment.ProcessorCount * 4;

            ConfigureThreadPool();


            InitializePopulation();
        }

        private void ConfigureThreadPool()
        {
            // Set minimum thread pool sizes
            ThreadPool.SetMinThreads(_minWorkerThreads, _minIOThreads);

            // Set maximum thread pool sizes  
            ThreadPool.SetMaxThreads(_maxWorkerThreads, _maxIOThreads);

            // Get current settings for logging
            ThreadPool.GetMinThreads(out int currentMinWorker, out int currentMinIO);
            ThreadPool.GetMaxThreads(out int currentMaxWorker, out int currentMaxIO);
            ThreadPool.GetAvailableThreads(out int availableWorker, out int availableIO);

            Console.WriteLine($"Thread Pool Configuration:");
            Console.WriteLine($"  Worker Threads: Min={currentMinWorker}, Max={currentMaxWorker}, Available={availableWorker}");
            Console.WriteLine($"  I/O Threads: Min={currentMinIO}, Max={currentMaxIO}, Available={availableIO}");
            Console.WriteLine($"  Simulation Worker Threads: {_workerThreadCount}");
        }

        private void InitializePopulation()
        {
            const int wolfCount = 10;
            const int hareCount = 100;
            _stateMutex.WaitOne();
            try
            {
                for (int i = 0; i < hareCount; i++)
                {
                    var random = _threadLocalRandom.Value;
                    _state.Hares.Add(new Hare(random.NextDouble() * WorldWidth, random.NextDouble() * WorldHeight)
                    {
                        Id = Interlocked.Increment(ref _nextHareId)
                    });
                }

                for (int i = 0; i < wolfCount; i++)
                {
                    var random = _threadLocalRandom.Value;
                    _state.Wolves.Add(new Wolf(random.NextDouble() * WorldWidth, random.NextDouble() * WorldHeight)
                    {
                        Id = Interlocked.Increment(ref _nextWolfId)
                    });
                }
            }
            finally
            {
                _stateMutex.ReleaseMutex();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Update cycle using thread-safe increment
                var currentCycle = Interlocked.Increment(ref _currentCycle);

                // Update the state's cycle property
                _stateMutex.WaitOne();
                try
                {
                    _state.Cycle = currentCycle;
                }
                finally
                {
                    _stateMutex.ReleaseMutex();
                }

                await ProcessAnimalMovementPhase(stoppingToken);


                await CleanupAndManagePopulation(stoppingToken);

                await _hubContext.Clients.All.SendAsync("SimulationUpdate", CreateStateCopy(), stoppingToken);

                await Task.Delay(50, stoppingToken); // Faster updates for better chase visualization
            }
        }

        private async Task ProcessAnimalMovementPhase(CancellationToken stoppingToken)
        {
            // Get snapshots for thread-safe processing
            List<Animal> allAnimals;
            _stateMutex.WaitOne();
            try
            {
                allAnimals = _state.Hares.Cast<Animal>().Concat(_state.Wolves.Cast<Animal>()).Where(a => a.IsAlive).ToList();
            }
            finally
            {
                _stateMutex.ReleaseMutex();
            }

            // Determine optimal number of threads based on animal count
            int optimalThreadCount = Math.Min(_workerThreadCount, Math.Max(1, allAnimals.Count / 2));

            // Use thread pool to process animal movement in parallel
            var tasks = new List<Task>();

            // Create tasks for processing hares
            for (int i = 0; i < optimalThreadCount; i++)
            {
                int threadIndex = i;
                tasks.Add(Task.Run(() => ProcessHaresMovement(threadIndex, optimalThreadCount, allAnimals, stoppingToken)));
            }

            // Create tasks for processing wolves
            for (int i = 0; i < optimalThreadCount; i++)
            {
                int threadIndex = i;
                tasks.Add(Task.Run(() => ProcessWolvesMovement(threadIndex, optimalThreadCount, allAnimals, stoppingToken)));
            }

            // Wait for all movement tasks to complete - this replaces the barrier
            await Task.WhenAll(tasks);

            Console.WriteLine($"Cycle {_currentCycle} - All {optimalThreadCount} threads completed movement phase");
        }

        private static void ProcessHaresMovement(int threadIndex, int totalThreads, List<Animal> allAnimals, CancellationToken stoppingToken)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} processing hares movement chunk {threadIndex + 1}/{totalThreads}");

            var hares = allAnimals.OfType<Hare>().Where(h => h.IsAlive).ToList();
            if (hares.Count == 0) return;

            // Process assigned chunk of hares
            int chunkSize = Math.Max(1, hares.Count / totalThreads);
            int startIndex = threadIndex * chunkSize;
            if (startIndex >= hares.Count) return;

            int endIndex = (threadIndex == totalThreads - 1) ? hares.Count : startIndex + chunkSize;

            for (int i = startIndex; i < endIndex && !stoppingToken.IsCancellationRequested; i++)
            {
                var hare = hares[i];
                hare.Move(WorldWidth, WorldHeight, allAnimals);
            }
        }

        private static void ProcessWolvesMovement(int threadIndex, int totalThreads, List<Animal> allAnimals, CancellationToken stoppingToken)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} processing wolves movement chunk {threadIndex + 1}/{totalThreads}");

            var wolves = allAnimals.OfType<Wolf>().Where(w => w.IsAlive).ToList();

            if (wolves.Count == 0) return;

            // Process assigned chunk of wolves
            int chunkSize = Math.Max(1, wolves.Count / totalThreads);
            int startIndex = threadIndex * chunkSize;

            if (startIndex >= wolves.Count) return;

            int endIndex = (threadIndex == totalThreads - 1) ? wolves.Count : startIndex + chunkSize;

            for (int i = startIndex; i < endIndex && !stoppingToken.IsCancellationRequested; i++)
            {
                var wolf = wolves[i];
                wolf.Move(WorldWidth, WorldHeight, allAnimals);
            }
        }
        private async Task CleanupAndManagePopulation(CancellationToken stoppingToken)
        {
            await Task.Run(() =>
            {
                var random = _threadLocalRandom.Value;

                _stateMutex.WaitOne();
                try
                {
                    _state.Hares.RemoveAll(h => !h.IsAlive);
                    _state.Wolves.RemoveAll(w => !w.IsAlive);

                    if (_state.Hares.Count < 10)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            _state.Hares.Add(new Hare(random.NextDouble() * WorldWidth, random.NextDouble() * WorldHeight)
                            {
                                Id = Interlocked.Increment(ref _nextHareId)
                            });
                        }
                    }

                    if (_state.Wolves.Count < 3)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            _state.Wolves.Add(new Wolf(random.NextDouble() * WorldWidth, random.NextDouble() * WorldHeight)
                            {
                                Id = Interlocked.Increment(ref _nextWolfId)
                            });
                        }
                    }
                }
                finally
                {
                    _stateMutex.ReleaseMutex();
                }
            }, stoppingToken);
        }

        private SimulationState CreateStateCopy()
        {
            _stateMutex.WaitOne();
            try
            {
                return new SimulationState
                {
                    Hares = _state.Hares.ToList(),
                    Wolves = _state.Wolves.ToList(),
                    Cycle = _state.Cycle
                };
            }
            finally
            {
                _stateMutex.ReleaseMutex();
            }
        }

        public override void Dispose()
        {
            _stateMutex?.Dispose();
            _threadLocalRandom?.Dispose();
            base.Dispose();
        }
    }
}
