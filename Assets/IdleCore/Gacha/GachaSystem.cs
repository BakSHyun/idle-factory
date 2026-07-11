using System;
using System.Collections.Generic;
using System.Linq;
using IdleCore.Economy;

namespace IdleCore.Gacha
{
    public sealed class BannerPoolEntry
    {
        public string unitId;
        public double weight = 1;
    }

    public sealed class BannerDef
    {
        public string id;
        public string name;
        public string costCurrency = CurrencyIds.GemSoft;
        public long costPerPull = 200;
        /// <summary>10연 할인가 (0이면 costPerPull×10)</summary>
        public long costPerTen;
        /// <summary>등급별 확률 (합 1.0)</summary>
        public Dictionary<UnitGrade, double> gradeRates = new Dictionary<UnitGrade, double>();
        /// <summary>등급별 풀</summary>
        public List<BannerPoolEntry> pool = new List<BannerPoolEntry>();
        /// <summary>천장: 이 횟수 안에 최고 등급 미출현 시 확정 (0 = 천장 없음)</summary>
        public int pityThreshold = 150;
        public UnitGrade pityGrade = UnitGrade.Ancient;
        /// <summary>픽업 유닛: 해당 등급 당첨 시 이 유닛이 나올 지분 (0~1)</summary>
        public string pickupUnitId;
        public double pickupShare = 0.5;

        public long CostFor(int count) =>
            count == 10 && costPerTen > 0 ? costPerTen : costPerPull * count;
    }

    public sealed class GachaResult
    {
        public List<string> UnitIds = new List<string>();
        public int PityCounterAfter;
    }

    /// <summary>
    /// 배너 기반 가챠. 천장(pity)·픽업 지원. 결과는 UnitInventory에 반영된다.
    /// 확률 검증은 결정론 RNG로 테스트 가능.
    /// </summary>
    public sealed class GachaSystem
    {
        private readonly Dictionary<string, BannerDef> _banners = new Dictionary<string, BannerDef>();
        private readonly Dictionary<string, int> _pityCounters = new Dictionary<string, int>();
        private readonly UnitInventory _inventory;
        private readonly Wallet _wallet;
        private readonly IRng _rng;

        public GachaSystem(IEnumerable<BannerDef> banners, UnitInventory inventory, Wallet wallet, IRng rng)
        {
            foreach (var b in banners) _banners[b.id] = b;
            _inventory = inventory;
            _wallet = wallet;
            _rng = rng;
        }

        public IReadOnlyDictionary<string, BannerDef> Banners => _banners;
        public int PityCounter(string bannerId) => _pityCounters.TryGetValue(bannerId, out var v) ? v : 0;

        public bool TryPull(string bannerId, int count, out GachaResult result)
        {
            result = null;
            if (!_banners.TryGetValue(bannerId, out var banner)) return false;
            long cost = banner.CostFor(count);
            if (!_wallet.TrySpend(banner.costCurrency, cost)) return false;

            result = new GachaResult();
            int pity = PityCounter(bannerId);

            for (int i = 0; i < count; i++)
            {
                pity++;
                UnitGrade grade = RollGrade(banner);
                if (banner.pityThreshold > 0 && pity >= banner.pityThreshold)
                    grade = banner.pityGrade;
                if (grade >= banner.pityGrade) pity = 0;

                string unitId = RollUnit(banner, grade);
                _inventory.AddCopy(unitId);
                result.UnitIds.Add(unitId);
            }

            _pityCounters[bannerId] = pity;
            result.PityCounterAfter = pity;
            return true;
        }

        private UnitGrade RollGrade(BannerDef banner)
        {
            double roll = _rng.NextDouble();
            double cumulative = 0;
            foreach (var kv in banner.gradeRates.OrderBy(kv => kv.Key))
            {
                cumulative += kv.Value;
                if (roll < cumulative) return kv.Key;
            }
            return banner.gradeRates.Keys.Min();
        }

        private string RollUnit(BannerDef banner, UnitGrade grade)
        {
            // 픽업: 최고 등급 당첨 시 지분만큼 픽업 유닛 확정
            if (banner.pickupUnitId != null && grade == banner.pityGrade && _rng.NextDouble() < banner.pickupShare)
                return banner.pickupUnitId;

            var candidates = banner.pool
                .Where(p => _inventory.Defs.TryGetValue(p.unitId, out var d) && d.grade == grade)
                .ToList();
            if (candidates.Count == 0)
                throw new InvalidOperationException($"banner {banner.id}: empty pool for grade {grade}");

            double total = candidates.Sum(c => c.weight);
            double roll = _rng.NextDouble() * total;
            double acc = 0;
            foreach (var c in candidates)
            {
                acc += c.weight;
                if (roll < acc) return c.unitId;
            }
            return candidates[candidates.Count - 1].unitId;
        }

        public Dictionary<string, int> ExportPity() => new Dictionary<string, int>(_pityCounters);

        public void ImportPity(Dictionary<string, int> pity)
        {
            _pityCounters.Clear();
            if (pity != null) foreach (var kv in pity) _pityCounters[kv.Key] = kv.Value;
        }
    }
}
