using System;
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
        private readonly Random _random;
        private static object _stateLock = new object();
        private int _nextHareId = 1;
        private int _nextWolfId = 1;
        
        private const int WorldWidth = 800;
        private const int WorldHeight = 600;

        public SimulationService(IHubContext<SimulationHub> hubContext)
        {
            _hubContext = hubContext;
            _state = new SimulationState();
            _random = new Random();
            InitializePopulation();
        }

        private void InitializePopulation()
        {
            lock (_stateLock)
            {
                for (int i = 0; i < 20; i++)
                {
                    _state.Hares.Add(new Hare(_random.NextDouble() * WorldWidth, _random.NextDouble() * WorldHeight) 
                    { 
                        Id = Interlocked.Increment(ref _nextHareId)
                    });
                }

                for (int i = 0; i < 5; i++)
                {
                    _state.Wolves.Add(new Wolf(_random.NextDouble() * WorldWidth, _random.NextDouble() * WorldHeight) 
                    { 
                        Id = Interlocked.Increment(ref _nextWolfId)
                    });
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _state.Cycle++;

                // Move all animals
                foreach (var hare in _state.Hares.Where(h => h.IsAlive))
                {
                    hare.Move(WorldWidth, WorldHeight);
                    hare.Update();
                }

                foreach (var wolf in _state.Wolves.Where(w => w.IsAlive))
                {
                    wolf.Move(WorldWidth, WorldHeight);
                    wolf.Update();
                }

                // Hunting
                foreach (var wolf in _state.Wolves.Where(w => w.IsAlive))
                {
                    var nearbyHares = _state.Hares.Where(h => h.IsAlive && wolf.CanHunt(h)).ToList();
                    if (nearbyHares.Any())
                    {
                        var targetHare = nearbyHares[_random.Next(nearbyHares.Count)];
                        wolf.Hunt(targetHare);
                    }
                }

                // Reproduction
                var reproductiveHares = _state.Hares.Where(h => h.IsAlive && h.CanReproduce()).ToList();
                foreach (var hare in reproductiveHares)
                {
                    if (_random.NextDouble() < 0.1)
                    {
                        var baby = new Hare(
                            hare.X + (_random.NextDouble() - 0.5) * 20,
                            hare.Y + (_random.NextDouble() - 0.5) * 20)
                        {
                            Id = _nextHareId++
                        };
                        _state.Hares.Add(baby);
                        hare.Energy -= 20;
                    }
                }

                var reproductiveWolves = _state.Wolves.Where(w => w.IsAlive && w.CanReproduce()).ToList();
                foreach (var wolf in reproductiveWolves)
                {
                    if (_random.NextDouble() < 0.05)
                    {
                        var baby = new Wolf(
                            wolf.X + (_random.NextDouble() - 0.5) * 30,
                            wolf.Y + (_random.NextDouble() - 0.5) * 30)
                        {
                            Id = _nextWolfId++
                        };
                        _state.Wolves.Add(baby);
                        wolf.Energy -= 30;
                    }
                }

                // Remove dead animals
                _state.Hares.RemoveAll(h => !h.IsAlive);
                _state.Wolves.RemoveAll(w => !w.IsAlive);

                // Prevent extinction
                if (!_state.Hares.Any())
                {
                    for (int i = 0; i < 5; i++)
                    {
                        _state.Hares.Add(new Hare(_random.NextDouble() * WorldWidth, _random.NextDouble() * WorldHeight) 
                        { 
                            Id = _nextHareId++ 
                        });
                    }
                }

                if (!_state.Wolves.Any())
                {
                    for (int i = 0; i < 2; i++)
                    {
                        _state.Wolves.Add(new Wolf(_random.NextDouble() * WorldWidth, _random.NextDouble() * WorldHeight) 
                        { 
                            Id = _nextWolfId++ 
                        });
                    }
                }

                // Send update to all connected clients
                await _hubContext.Clients.All.SendAsync("SimulationUpdate", _state, stoppingToken);
                
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}