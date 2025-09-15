using System;

namespace EcosystemSimulation.Models
{
    public abstract class Animal
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        protected int _energy;
        public int Energy { get => _energy; set => _energy = value; }
        protected int _age;
        public int Age { get => _age; set => _age = value; }
        public bool IsAlive { get; set; } = true;

        // Thread-local random for thread safety
        private static readonly ThreadLocal<Random> ThreadLocalRandom =
            new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        protected Random Random => ThreadLocalRandom.Value;

        public abstract void Move(int worldWidth, int worldHeight);
        public abstract void Update();
        public abstract bool CanReproduce();
    }

    public class Hare : Animal
    {
        public const int MaxEnergy = 100;
        public const int ReproductionThreshold = 60;
        public const int MaxAge = 50;

        public Hare(double x, double y)
        {
            X = x;
            Y = y;
            Energy = 50;
            Age = 0;
        }

        public override void Move(int worldWidth, int worldHeight)
        {
            // Use thread-safe random
            X += (Random.NextDouble() - 0.5) * 10;
            Y += (Random.NextDouble() - 0.5) * 10;

            X = Math.Max(0, Math.Min(worldWidth, X));
            Y = Math.Max(0, Math.Min(worldHeight, Y));

            Interlocked.Decrement(ref _energy); // Thread-safe energy decrease
        }

        public override void Update()
        {
            Interlocked.Increment(ref _age); // Thread-safe age increment

            if (Random.NextDouble() < 0.3)
            {
                // Thread-safe energy increase with bounds checking
                int currentEnergy, newEnergy;
                do
                {
                    currentEnergy = Energy;
                    newEnergy = Math.Min(MaxEnergy, currentEnergy + 15);
                } while (Interlocked.CompareExchange(ref _energy, newEnergy, currentEnergy) != currentEnergy);
            }

            if (Age > MaxAge || Energy <= 0)
            {
                IsAlive = false;
            }
        }

        public override bool CanReproduce()
        {
            return Energy > ReproductionThreshold && Age > 5;
        }
    }

    public class Wolf : Animal
    {
        public const int MaxEnergy = 150;
        public const int ReproductionThreshold = 100;
        public const int MaxAge = 80;
        public const double HuntRange = 20;

        public Wolf(double x, double y)
        {
            X = x;
            Y = y;
            Energy = 75;
            Age = 0;
        }

        public override void Move(int worldWidth, int worldHeight)
        {
            // Use thread-safe random
            X += (Random.NextDouble() - 0.5) * 15;
            Y += (Random.NextDouble() - 0.5) * 15;

            X = Math.Max(0, Math.Min(worldWidth, X));
            Y = Math.Max(0, Math.Min(worldHeight, Y));

            // Thread-safe energy decrease
            Interlocked.Add(ref _energy, -2);
        }

        public override void Update()
        {
            Interlocked.Increment(ref _age); // Thread-safe age increment
            Interlocked.Decrement(ref _energy); // Thread-safe energy decrease

            if (Age > MaxAge || Energy <= 0)
            {
                IsAlive = false;
            }
        }

        public override bool CanReproduce()
        {
            return Energy > ReproductionThreshold && Age > 10;
        }

        public bool CanHunt(Hare hare)
        {
            var distance = Math.Sqrt(Math.Pow(X - hare.X, 2) + Math.Pow(Y - hare.Y, 2));
            return distance <= HuntRange;
        }

        public void Hunt(Hare hare)
        {
            if (CanHunt(hare) && Random.NextDouble() < 0.7) // 70% success rate
            {
                // Thread-safe energy increase with bounds checking
                int currentEnergy, newEnergy;
                do
                {
                    currentEnergy = Energy;
                    newEnergy = Math.Min(MaxEnergy, currentEnergy + 40);
                } while (Interlocked.CompareExchange(ref _energy, newEnergy, currentEnergy) != currentEnergy);

                hare.IsAlive = false;
            }
        }
    }
}