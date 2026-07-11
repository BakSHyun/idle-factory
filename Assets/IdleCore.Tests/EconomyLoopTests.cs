using System.Collections.Generic;
using System.Linq;
using IdleCore;
using IdleCore.Economy;
using IdleCore.Gacha;
using IdleCore.Progression;
using IdleCore.Stats;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class EconomyLoopTests
    {
        // ── 마일리지 ──

        [Test]
        public void Gacha_AccruesMileagePerPull()
        {
            var wallet = new Wallet();
            var units = new List<UnitDef> { new UnitDef { id = "u1", grade = UnitGrade.Rare } };
            var banner = new BannerDef
            {
                id = "b", costPerPull = 200, costPerTen = 1800, mileagePerPull = 1,
                gradeRates = new Dictionary<UnitGrade, double> { { UnitGrade.Rare, 1.0 } },
                pool = new List<BannerPoolEntry> { new BannerPoolEntry { unitId = "u1" } },
                pityThreshold = 0,
            };
            var gacha = new GachaSystem(new[] { banner }, new UnitInventory(units), wallet, new SeededRng(1));
            wallet.Earn(CurrencyIds.GemSoft, 1800);
            gacha.TryPull("b", 10, out _);
            Assert.AreEqual(10, wallet.Get(CurrencyIds.Mileage), "소환 10회 = 마일리지 10");
        }

        [Test]
        public void MileageShop_UnitChoicePurchase()
        {
            var clock = new ManualClock();
            var wallet = new Wallet();
            var units = new List<UnitDef>
            {
                new UnitDef { id = "m1", name = "신화A", kind = "hero", grade = UnitGrade.Mythic },
                new UnitDef { id = "m2", name = "신화B", kind = "skill", grade = UnitGrade.Mythic },
                new UnitDef { id = "e1", name = "전설A", kind = "hero", grade = UnitGrade.Epic },
            };
            var inventory = new UnitInventory(units);
            var product = new ProductDef
            {
                id = "select_mythic", costCurrency = CurrencyIds.Mileage, price = 200,
                limitType = PurchaseLimitType.Weekly, limitCount = 1,
                unitChoice = new UnitChoiceDef { grade = UnitGrade.Mythic },
            };
            var shop = new ShopSystem(new[] { product }, wallet, clock, inventory);

            Assert.AreEqual(2, shop.ChoiceCandidates("select_mythic").Count, "신화만 후보");
            Assert.IsFalse(shop.TryPurchase("select_mythic", 0), "선택권은 일반 구매 불가");

            wallet.Earn(CurrencyIds.Mileage, 400);
            Assert.IsFalse(shop.TryPurchaseUnitChoice("select_mythic", 0, "e1"), "등급 불일치 거부");
            Assert.IsTrue(shop.TryPurchaseUnitChoice("select_mythic", 0, "m1"));
            Assert.AreEqual(1, inventory.Get("m1").copies);
            Assert.IsFalse(shop.TryPurchaseUnitChoice("select_mythic", 0, "m2"), "주간 1회 한도");
        }

        // ── 합성 ──

        [Test]
        public void Compose_TierChain_ConsumesOnlySurplus()
        {
            // 서브등급 체인: 초급1 → 초급2 → ... → 초급4 → 중급1
            var units = new List<UnitDef>
            {
                new UnitDef { id = "w_b_t1", kind = "weapon", grade = UnitGrade.Beginner, subTier = 1, upgradeToId = "w_b_t2" },
                new UnitDef { id = "w_b_t2", kind = "weapon", grade = UnitGrade.Beginner, subTier = 2, upgradeToId = "w_b_t3" },
                new UnitDef { id = "w_b_t3", kind = "weapon", grade = UnitGrade.Beginner, subTier = 3, upgradeToId = "w_b_t4" },
                new UnitDef { id = "w_b_t4", kind = "weapon", grade = UnitGrade.Beginner, subTier = 4, upgradeToId = "w_i_t1" },
                new UnitDef { id = "w_i_t1", kind = "weapon", grade = UnitGrade.Intermediate, subTier = 1 },
            };
            var inventory = new UnitInventory(units) { ComposeCost = 5 };

            inventory.AddCopy("w_b_t1", 11); // 10돌 만렙 정확히 — 잉여 0
            Assert.AreEqual(0, inventory.SurplusCopies("w_b_t1"));
            Assert.IsFalse(inventory.CanCompose("w_b_t1"));

            inventory.AddCopy("w_b_t1", 5); // 잉여 5
            Assert.IsTrue(inventory.TryComposeUnit("w_b_t1", out var result));
            Assert.AreEqual("w_b_t2", result, "초급1 → 초급2");
            Assert.AreEqual(10, inventory.Get("w_b_t1").limitBreak, "승급해도 돌파는 유지");

            // 4티어 → 다음 등급 1티어
            inventory.AddCopy("w_b_t4", 11 + 5);
            Assert.IsTrue(inventory.TryComposeUnit("w_b_t4", out var next));
            Assert.AreEqual("w_i_t1", next, "초급4 → 중급1");

            // 체인 끝 (upgradeToId 없음)
            Assert.IsFalse(inventory.CanCompose("w_i_t1"));
        }

        // ── 던전 ──

        private static (DungeonSystem, Wallet, ManualClock) BuildDungeon(double attack)
        {
            var clock = new ManualClock();
            var wallet = new Wallet();
            var stats = new StatSystem(new GrowthAxisDef[0], new Dictionary<StatType, double>
            {
                { StatType.Attack, attack }, { StatType.AttackSpeed, 1 }, { StatType.CritMultiplier, 1 },
            });
            var def = new DungeonDef
            {
                id = "d", unlockStageIndex = 10, freeEntriesPerDay = 3,
                enemyHpBase = 1000, enemyHpGrowth = 2.0, timeLimitSeconds = 10,
                rewardCurrency = CurrencyIds.Gold, rewardBase = 100, rewardGrowth = 1.5,
            };
            return (new DungeonSystem(new[] { def }, wallet, stats, clock), wallet, clock);
        }

        [Test]
        public void Dungeon_ClimbsWhileDpsAllows()
        {
            var (dungeons, wallet, _) = BuildDungeon(attack: 400); // DPS 400×10s=4000 → 1층 1000, 2층 2000, 3층 4000 클리어
            Assert.IsNull(dungeons.TryChallenge("d", 5), "해금 전");

            var result = dungeons.TryChallenge("d", 10);
            Assert.AreEqual(3, result.HighestFloor);
            Assert.AreEqual(3, result.FloorsCleared);
            Assert.AreEqual(dungeons.Defs["d"].FloorReward(3), wallet.Get(CurrencyIds.Gold));
        }

        [Test]
        public void Dungeon_DailyEntriesAndSweep()
        {
            var (dungeons, wallet, clock) = BuildDungeon(attack: 400);
            for (int i = 0; i < 3; i++) Assert.IsNotNull(dungeons.TryChallenge("d", 10));
            Assert.IsNull(dungeons.TryChallenge("d", 10), "일 3회 소진");

            Assert.IsNull(dungeons.TrySweep("d"), "소탕권 없음");
            wallet.Earn(CurrencyIds.SweepTicket, 2);
            long before = wallet.Get(CurrencyIds.Gold);
            Assert.IsNotNull(dungeons.TrySweep("d"));
            Assert.AreEqual(1, wallet.Get(CurrencyIds.SweepTicket));
            Assert.Greater(wallet.Get(CurrencyIds.Gold), before, "소탕 = 최고 층 보상");

            clock.Advance(System.TimeSpan.FromDays(1));
            Assert.IsNotNull(dungeons.TryChallenge("d", 10), "자정 리셋");
        }

        [Test]
        public void Dungeon_AdSweep_NoTicketNeeded()
        {
            var (dungeons, wallet, _) = BuildDungeon(attack: 400);
            Assert.IsNull(dungeons.GrantAdSweep("d"), "1층 미클리어 시 불가");
            dungeons.TryChallenge("d", 10);
            long before = wallet.Get(CurrencyIds.Gold);
            Assert.IsNotNull(dungeons.GrantAdSweep("d"));
            Assert.Greater(wallet.Get(CurrencyIds.Gold), before);
            Assert.AreEqual(0, wallet.Get(CurrencyIds.SweepTicket), "티켓 소모 없음");
        }
    }
}
