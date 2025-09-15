using System.Collections.Generic;

namespace EcosystemSimulation.Models
{
    public class SimulationState
    {
        public List<Hare> Hares { get; set; } = new List<Hare>();
        public List<Wolf> Wolves { get; set; } = new List<Wolf>();
        public int Cycle { get; set; }
        public int HareCount => Hares.Count;
        public int WolfCount => Wolves.Count;
    }
}

