using System.Collections.Generic;
using System.Linq;
using IdleCore;
using IdleCore.Economy;
using IdleCore.Gacha;
using IdleCore.Stats;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class GachaTests
    {
        private static List<UnitDef> Units() => new List<UnitDef>
        {
            new UnitDef { id = "hero_r1", grade = UnitGrade.Rare },
            new UnitDef { id = "hero_r2", grade = UnitGrade.Rare },
            new UnitDef { id = "hero_e1", grade = UnitGrade.Epic },
            new UnitDef { id = "hero_m1", grade = UnitGrade.Mythic },
            new UnitDef { id = "hero_a1", grade = UnitGrade.Ancient },
            new UnitDef { id = "hero_a2", grade = UnitGrade.Ancient },
        };

        private static BannerDef Banner(int pity = 150) => new BannerDef
        {
            id = "standard",
            costCurrency = CurrencyIds.GemSoft,
            costPerPull = 200,
            costPerTen = 1800,
            gradeRates = new Dictionary<UnitGrade, double>
            {
                { UnitGrade.Rare, 0.70 },
                { UnitGrade.Epic, 0.235 },
                { UnitGrade.Mythic, 0.06 },
                { UnitGrade.Ancient, 0.005 },
            },
            pool = new List<BannerPoolEntry>
            {
                new BannerPoolEntry { unitId = "hero_r1" }, new BannerPoolEntry { unitId = "hero_r2" },
                new BannerPoolEntry { unitId = "hero_e1" }, new BannerPoolEntry { unitId = "hero_m1" },
                new BannerPoolEntry { unitId = "hero_a1" }, new BannerPoolEntry { unitId = "hero_a2" },
            },
            pityThreshold = pity,
            pityGrade = UnitGrade.Ancient,
            pickupUnitId = "hero_a1",
            pickupShare = 1.0,
        };

        private static (GachaSystem, UnitInventory, Wallet) Build(int seed, int pity = 150)
        {
            var wallet = new Wallet();
            var inventory = new UnitInventory(Units());
            var gacha = new GachaSystem(new[] { Banner(pity) }, inventory, wallet, new SeededRng(seed));
            return (gacha, inventory, wallet);
        }

        [Test]
        public void Pull_SpendsCurrency_TenPullDiscount()
        {
            var (gacha, _, wallet) = Build(1);
            wallet.Earn(CurrencyIds.GemSoft, 2000);
            Assert.IsTrue(gacha.TryPull("standard", 10, out var result));
            Assert.AreEqual(10, result.UnitIds.Count);
            Assert.AreEqual(200, wallet.Get(CurrencyIds.GemSoft), "10연 할인 1800 차감");
        }

        [Test]
        public void Pull_FailsWithoutFunds()
        {
            var (gacha, _, wallet) = Build(1);
            wallet.Earn(CurrencyIds.GemSoft, 100);
            Assert.IsFalse(gacha.TryPull("standard", 1, out _));
            Assert.AreEqual(100, wallet.Get(CurrencyIds.GemSoft));
        }

        [Test]
        public void Pity_GuaranteesTopGradeWithinThreshold()
        {
            var (gacha, inventory, wallet) = Build(42, pity: 150);
            wallet.Earn(CurrencyIds.GemSoft, 200L * 150);
            gacha.TryPull("standard", 150, out var result);

            bool gotAncient = result.UnitIds.Any(id => inventory.Defs[id].grade == UnitGrade.Ancient);
            Assert.IsTrue(gotAncient, "150연 안에 최고 등급 확정 (천장)");
        }

        [Test]
        public void Pity_ResetsAfterTopGrade()
        {
            var (gacha, _, wallet) = Build(7, pity: 5);
            wallet.Earn(CurrencyIds.GemSoft, 200L * 20);
            gacha.TryPull("standard", 20, out _);
            Assert.Less(gacha.PityCounter("standard"), 5, "천장 발동 후 카운터 리셋");
        }

        [Test]
        public void GradeDistribution_MatchesRates_Statistically()
        {
            var (gacha, inventory, wallet) = Build(1234);
            const int pulls = 20000;
            wallet.Earn(CurrencyIds.GemSoft, 200L * pulls);
            gacha.TryPull("standard", pulls, out var result);

            double rareShare = result.UnitIds.Count(id => inventory.Defs[id].grade == UnitGrade.Rare) / (double)pulls;
            Assert.AreEqual(0.70, rareShare, 0.02, "표기 확률과 실제 분포 일치 (±2%p)");
        }

        [Test]
        public void Dupes_AutoLimitBreak_ThresholdEffectActivates()
        {
            var units = Units();
            units[0].limitBreakEffects = new List<LimitBreakEffect>
            {
                new LimitBreakEffect
                {
                    atLimitBreak = 9, // "9돌파 임계" 패턴
                    effect = new StatEffect { stat = StatType.FinalDamage, mode = EffectMode.Mul,
                        value = new ValueCurve { baseValue = 5.0 } },
                },
            };
            var inventory = new UnitInventory(units);

            for (int i = 0; i < 9; i++) inventory.AddCopy("hero_r1");
            Assert.IsTrue(inventory.TryEquip("hero_r1"));
            Assert.AreEqual(8, inventory.Get("hero_r1").limitBreak);
            Assert.IsFalse(inventory.ContributeEffects().Any(e => e.stat == StatType.FinalDamage),
                "8돌파: 임계 효과 미발동");

            inventory.AddCopy("hero_r1"); // 10번째 사본 → 9돌파
            Assert.AreEqual(9, inventory.Get("hero_r1").limitBreak);
            Assert.IsTrue(inventory.ContributeEffects().Any(e => e.stat == StatType.FinalDamage),
                "9돌파: 임계 효과 발동");
        }

        [Test]
        public void CollectionEffects_ApplyWithoutEquip()
        {
            var units = Units();
            units[0].collectionEffects = new List<StatEffect>
            {
                new StatEffect { stat = StatType.Attack, mode = EffectMode.Add,
                    value = new ValueCurve { baseValue = 3 } },
            };
            var inventory = new UnitInventory(units);
            inventory.AddCopy("hero_r1");
            // 장착하지 않아도 도감 효과는 기여 ('잉여 제로')
            Assert.IsTrue(inventory.ContributeEffects().Any(e => e.stat == StatType.Attack));
        }
    }
}
