using System;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldEconomy
    {
        public event Action OnChanged;

        private readonly double[] _amounts = new double[TileContribution.ResourceCount];
        private readonly double[] _prodPerSec = new double[TileContribution.ResourceCount];
        private readonly double[] _stats = new double[TileContribution.StatCount];

        public void Init(double startCredits)
        {
            Array.Clear(_amounts, 0, _amounts.Length);
            Array.Clear(_prodPerSec, 0, _prodPerSec.Length);
            Array.Clear(_stats, 0, _stats.Length);

            _amounts[(int)ResourceType.Credits] = startCredits;
            OnChanged?.Invoke();
        }

        public double Get(ResourceType t) => _amounts[(int)t];
        public double GetProduction(ResourceType t) => _prodPerSec[(int)t];
        public double GetStat(StatType t) => _stats[(int)t];

        public bool CanAfford(double creditsCost) => _amounts[(int)ResourceType.Credits] >= creditsCost;

        public bool SpendCredits(double creditsCost)
        {
            if (!CanAfford(creditsCost)) return false;
            _amounts[(int)ResourceType.Credits] -= creditsCost;
            OnChanged?.Invoke();
            return true;
        }

        public void Add(ResourceType t, double amount)
        {
            _amounts[(int)t] += amount;
            OnChanged?.Invoke();
        }

        public void Tick(double dt)
        {
            for (int i = 0; i < _prodPerSec.Length; i++)
                _amounts[i] += _prodPerSec[i] * dt;
        }

        public void AddContribution(in TileContribution c)
        {
            for (int i = 0; i < _prodPerSec.Length; i++)
                _prodPerSec[i] += c.prodPerSec[i];

            for (int i = 0; i < _stats.Length; i++)
                _stats[i] += c.stats[i];

            OnChanged?.Invoke();
        }

        public void SubtractContribution(in TileContribution c)
        {
            for (int i = 0; i < _prodPerSec.Length; i++)
                _prodPerSec[i] -= c.prodPerSec[i];

            for (int i = 0; i < _stats.Length; i++)
                _stats[i] -= c.stats[i];

            OnChanged?.Invoke();
        }
    }
}
