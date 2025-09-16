using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EcosystemSimulation
{
    public abstract class Animal
    {
        public int Id;
        public double X { get; protected set; }
        public double Y { get; protected set; }
        public double VelocityX { get; protected set; }
        public double VelocityY { get; protected set; }
        public bool IsAlive { get; set; } = true;

        // Thread-local random
        private static readonly ThreadLocal<Random> ThreadLocalRandom =
            new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        protected Random Random => ThreadLocalRandom.Value;

        protected Animal(double x, double y)
        {
            X = x;
            Y = y;
        }

        public abstract void Move(int worldWidth, int worldHeight, List<Animal> others);

        protected void ApplyVelocity(double maxSpeed)
        {
            double speed = Math.Sqrt(VelocityX * VelocityX + VelocityY * VelocityY);
            if (speed > maxSpeed)
            {
                VelocityX = (VelocityX / speed) * maxSpeed;
                VelocityY = (VelocityY / speed) * maxSpeed;
            }

            X += VelocityX;
            Y += VelocityY;
        }

        protected void ContainWithinBounds(int width, int height)
        {
            if (X < 0 || X > width)
            {
                VelocityX *= -0.8;
                X = Math.Clamp(X, 0, width);
            }
            if (Y < 0 || Y > height)
            {
                VelocityY *= -0.8;
                Y = Math.Clamp(Y, 0, height);
            }
        }
    }

    public class Hare : Animal
    {
        public const double MaxSpeed = 2.0;
        public const double FleeDistance = 100;

        public Hare(double x, double y) : base(x, y) { }

        public override void Move(int worldWidth, int worldHeight, List<Animal> others)
        {
            var wolves = others.OfType<Wolf>().Where(w => w.IsAlive).ToList();
            double fx = 0, fy = 0;

            foreach (var wolf in wolves)
            {
                double dx = X - wolf.X;
                double dy = Y - wolf.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < FleeDistance && dist > 0)
                {
                    fx += (dx / dist) * (1.0 / dist); // stronger when closer
                    fy += (dy / dist) * (1.0 / dist);
                }
            }

            if (fx == 0 && fy == 0)
            {
                // wander slightly
                fx = (Random.NextDouble() - 0.5) * 0.1;
                fy = (Random.NextDouble() - 0.5) * 0.1;
            }

            VelocityX = VelocityX * 0.9 + fx;
            VelocityY = VelocityY * 0.9 + fy;

            ApplyVelocity(MaxSpeed);
            ContainWithinBounds(worldWidth, worldHeight);
        }
    }

    public class Wolf : Animal
    {
        public const double MaxSpeed = 1.5;
        public const double HuntRange = 15;
        public const double ChaseDistance = 150;

        public Wolf(double x, double y) : base(x, y) { }

        public override void Move(int worldWidth, int worldHeight, List<Animal> others)
        {
            var hares = others.OfType<Hare>().Where(h => h.IsAlive).ToList();
            Hare target = null;
            double closest = double.MaxValue;
            double fx = 0, fy = 0;

            foreach (var hare in hares)
            {
                double dx = hare.X - X;
                double dy = hare.Y - Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < ChaseDistance && dist < closest)
                {
                    closest = dist;
                    target = hare;
                }
            }

            if (target != null)
            {
                double dx = target.X - X;
                double dy = target.Y - Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist > 0)
                {
                    double strength = Math.Min(0.05, 1.0 / dist);
                    fx = (dx / dist) * strength;
                    fy = (dy / dist) * strength;
                }

                if (dist <= HuntRange)
                {
                    // 🔒 Synchronize kill attempt
                    lock (target)
                    {
                        if (target.IsAlive)
                        {
                            target.IsAlive = false;
                        }
                    }
                }
            }
            else
            {
                fx = (Random.NextDouble() - 0.5) * 0.05;
                fy = (Random.NextDouble() - 0.5) * 0.05;
            }

            VelocityX = VelocityX * 0.9 + fx;
            VelocityY = VelocityY * 0.9 + fy;

            ApplyVelocity(MaxSpeed);
            ContainWithinBounds(worldWidth, worldHeight);
        }
    }
}