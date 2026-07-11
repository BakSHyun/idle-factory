using System.Collections.Generic;
using System.Linq;
using IdleCore;
using IdleCore.Economy;
using IdleCore.Gacha;
using IdleCore.Stats;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class SummonLevelTests
    {
        private static List<UnitDef> Units()
        {
            var list = new List<UnitDef>();
            foreach (var grade in new[]
            {
                UnitGrade.Beginner, UnitGrade.Intermediate, UnitGrade.Advanced,
                UnitGrade.Rare, UnitGrade.Epic, UnitGrade.Mythic,
            })
                list.Add(new UnitDef { id = $"w_{grade}", kind = "weapon", grade = grade });
            return list;
        }

        private static BannerDef Banner() => new BannerDef
        {
            id = "weapon",
            costCurrency = CurrencyIds.GemSoft,
            costPerPull = 200,
            costPerTen = 1800,
            rateTable = new List<SummonRateTier>
            {
                new SummonRateTier { level = 1, rates = new Dictionary<UnitGrade, double>
                    { { UnitGrade.Beginner, 90 }, { UnitGrade.Intermediate, 10 } } },
                new SummonRateTier { level = 3, rates = new Dictionary<UnitGrade, double>
                    { { UnitGrade.Beginner, 78 }, { UnitGrade.Intermediate, 19 }, { UnitGrade.Advanced, 3 } } },
            },
            levelUpPulls = new List<long> { 30, 80 }, // Lv2=30회, Lv3=80회
            pool = Units().Select(u => new BannerPoolEntry { unitId = u.id }).ToList(),
            pityThreshold = 0,
        };

        private static (GachaSystem, UnitInventory, Wallet) Build(int seed)
        {
            var wallet = new Wallet();
            var inventory = new UnitInventory(Units());
            var gacha = new GachaSystem(new[] { Banner() }, inventory, wallet, new SeededRng(seed));
            return (gacha, inventory, wallet);
        }

        [Test]
        public void SummonLevel_IncreasesWithPulls()
        {
            var (gacha, _, wallet) = Build(1);
            wallet.Earn(CurrencyIds.GemSoft, 200L * 1000);

            Assert.AreEqual(1, gacha.SummonLevel("weapon"));
            Assert.AreEqual(30, gacha.PullsToNextLevel("weapon"));

            gacha.TryPull("weapon", 10, out _);
            gacha.TryPull("weapon", 10, out _);
            gacha.TryPull("weapon", 10, out _);
            Assert.AreEqual(2, gacha.SummonLevel("weapon"), "누적 30회 → Lv2");
            Assert.AreEqual(50, gacha.PullsToNextLevel("weapon"));

            for (int i = 0; i < 5; i++) gacha.TryPull("weapon", 10, out _);
            Assert.AreEqual(3, gacha.SummonLevel("weapon"), "누적 80회 → Lv3 (최고)");
            Assert.AreEqual(0, gacha.PullsToNextLevel("weapon"));
        }

        [Test]
        public void Rates_FollowSummonLevel()
        {
            var (gacha, _, _) = Build(1);
            var lv1 = gacha.CurrentRates("weapon");
            Assert.AreEqual(0.90, lv1[UnitGrade.Beginner], 1e-9);
            Assert.IsFalse(lv1.ContainsKey(UnitGrade.Advanced), "Lv1: 상급 없음");
        }

        [Test]
        public void Lv1_Distribution_Is90to10()
        {
            var (gacha, inventory, wallet) = Build(777);
            wallet.Earn(CurrencyIds.GemSoft, 200L * 30);
            // Lv1 상태에서 정확히 29회만 (Lv2 진입 전)
            for (int i = 0; i < 29; i++) gacha.TryPull("weapon", 1, out _);
            long beginners = inventory.Get("w_Beginner")?.copies ?? 0;
            Assert.Greater(beginners, 20, "Lv1 확률 90%면 29회 중 초급이 대부분");
        }

        [Test]
        public void EquipmentLevel_CostsScaleAndCap()
        {
            var defs = Units();
            defs[0].maxLevel = 200; // 초급 낫
            defs[0].levelCost = new ValueCurve { type = CurveType.Geometric, baseValue = 80, growth = 1.045 };
            defs[5].maxLevel = 200; // 신화 낫
            defs[5].levelCost = new ValueCurve { type = CurveType.Geometric, baseValue = 9000, growth = 1.045 };
            defs[5].levelEffects = new List<StatEffect>
            {
                new StatEffect { stat = StatType.Attack, mode = EffectMode.Mul,
                    value = new ValueCurve { perLevel = 0.1 } },
            };
            var inventory = new UnitInventory(defs);
            var wallet = new Wallet();
            inventory.AddCopy("w_Beginner");
            inventory.AddCopy("w_Mythic");

            Assert.Greater(inventory.LevelUpCost("w_Mythic"), inventory.LevelUpCost("w_Beginner"),
                "등급이 높을수록 강화비가 비싸다");

            wallet.Earn(CurrencyIds.Gold, 100000);
            Assert.IsTrue(inventory.TryLevelUp("w_Mythic", wallet));
            Assert.AreEqual(2, inventory.Get("w_Mythic").level);

            // 레벨 효과는 장착 중에만, 레벨에 비례
            inventory.TryEquip("w_Mythic");
            var effect = inventory.ContributeEffects()
                .First(e => e.stat == StatType.Attack && e.mode == EffectMode.Mul);
            Assert.AreEqual(0.2, effect.value.Evaluate(1), 1e-9, "Lv2 × 0.1/레벨");

            // 만렙 캡
            inventory.Get("w_Mythic").level = 200;
            Assert.AreEqual(-1, inventory.LevelUpCost("w_Mythic"));
            Assert.IsFalse(inventory.TryLevelUp("w_Mythic", wallet));
        }
    }
}
