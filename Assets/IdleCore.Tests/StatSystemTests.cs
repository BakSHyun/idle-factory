using System.Collections.Generic;
using IdleCore.Stats;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class StatSystemTests
    {
        private static GrowthAxisDef AttackTraining() => new GrowthAxisDef
        {
            id = "training_attack",
            costCurrency = "gold",
            cost = new ValueCurve { type = CurveType.Geometric, baseValue = 10, growth = 1.1 },
            effects = new List<StatEffect>
            {
                new StatEffect { stat = StatType.Attack, mode = EffectMode.Add,
                    value = new ValueCurve { baseValue = 0, perLevel = 5 } },
            },
        };

        private static GrowthAxisDef FinalDamageAxis(string id, double perLevel) => new GrowthAxisDef
        {
            id = id,
            effects = new List<StatEffect>
            {
                new StatEffect { stat = StatType.FinalDamage, mode = EffectMode.Mul,
                    value = new ValueCurve { baseValue = 0, perLevel = perLevel } },
            },
        };

        private static Dictionary<StatType, double> BaseStats() => new Dictionary<StatType, double>
        {
            { StatType.Attack, 10 },
            { StatType.AttackSpeed, 1 },
            { StatType.CritMultiplier, 1.5 },
        };

        [Test]
        public void AddEffects_StackAdditively()
        {
            var stats = new StatSystem(new[] { AttackTraining() }, BaseStats());
            stats.SetLevel("training_attack", 4);
            Assert.AreEqual(10 + 5 * 4, stats.Snapshot().Get(StatType.Attack));
        }

        [Test]
        public void MulAxes_StackMultiplicatively_AcrossAxes()
        {
            // 곱연산 검증: 두 축이 각각 ×2 → 최종 ×4 (가산이었다면 ×3)
            var stats = new StatSystem(
                new[] { FinalDamageAxis("axis_a", 1.0), FinalDamageAxis("axis_b", 1.0) }, BaseStats());
            stats.SetLevel("axis_a", 1);
            stats.SetLevel("axis_b", 1);
            Assert.AreEqual(4.0, stats.Snapshot().Get(StatType.FinalDamage), 1e-9);
        }

        [Test]
        public void Dps_UsesCritExpectation()
        {
            var stats = new StatSystem(new GrowthAxisDef[0], new Dictionary<StatType, double>
            {
                { StatType.Attack, 100 },
                { StatType.AttackSpeed, 2 },
                { StatType.CritChance, 0.5 },
                { StatType.CritMultiplier, 3 },
            });
            // 100 × 2 × (1 + 0.5×(3-1)) = 400
            Assert.AreEqual(400, stats.Snapshot().Dps(), 1e-9);
        }

        [Test]
        public void CostCurve_Geometric_Grows()
        {
            var axis = AttackTraining();
            Assert.AreEqual(10, axis.CostAtLevel(0));
            Assert.Greater(axis.CostAtLevel(10), axis.CostAtLevel(0));
        }

        [Test]
        public void ExternalEffects_ContributeOnce()
        {
            var stats = new StatSystem(new GrowthAxisDef[0], BaseStats());
            stats.SetExternalEffects(new[]
            {
                new StatEffect { stat = StatType.Attack, mode = EffectMode.Add,
                    value = new ValueCurve { baseValue = 15 } },
            });
            Assert.AreEqual(25, stats.Snapshot().Get(StatType.Attack));
        }
    }
}
