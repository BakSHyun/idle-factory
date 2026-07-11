using System;
using System.Collections.Generic;
using IdleCore;
using IdleCore.Data;
using IdleCore.Economy;
using IdleCore.Save;
using IdleCore.Stats;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class GameSessionTests
    {
        private static GameConfig MinimalConfig() => new GameConfig
        {
            gameId = "test",
            baseStats = new Dictionary<StatType, double>
            {
                { StatType.Attack, 100 },
                { StatType.AttackSpeed, 1 },
                { StatType.CritMultiplier, 1 },
            },
            axes = new List<GrowthAxisDef>
            {
                new GrowthAxisDef
                {
                    id = "training",
                    costCurrency = CurrencyIds.Gold,
                    cost = new ValueCurve { type = CurveType.Geometric, baseValue = 10, growth = 1.08 },
                    effects = new List<StatEffect>
                    {
                        new StatEffect { stat = StatType.Attack, mode = EffectMode.Add,
                            value = new ValueCurve { perLevel = 10 } },
                    },
                },
            },
        };

        [Test]
        public void SaveLoad_RoundTripsState()
        {
            var store = new InMemorySaveStore();
            var clock = new ManualClock();

            var session = new GameSession(MinimalConfig(), store, clock, new SeededRng(1));
            session.Wallet.Earn(CurrencyIds.Gold, 5000);
            session.Stats.LevelUp("training");
            session.Stats.LevelUp("training");
            session.Tick(60);
            session.Save();

            var restored = new GameSession(MinimalConfig(), store, clock, new SeededRng(1));
            Assert.AreEqual(session.Wallet.Get(CurrencyIds.Gold), restored.Wallet.Get(CurrencyIds.Gold));
            Assert.AreEqual(2, restored.Stats.GetLevel("training"));
            Assert.AreEqual(session.Progression.HighestClearedIndex, restored.Progression.HighestClearedIndex);
        }

        [Test]
        public void OfflineReward_GrantedOnResume()
        {
            var store = new InMemorySaveStore();
            var clock = new ManualClock();

            var session = new GameSession(MinimalConfig(), store, clock, new SeededRng(1));
            session.Tick(1);
            session.Save();

            clock.Advance(TimeSpan.FromHours(4));
            var resumed = new GameSession(MinimalConfig(), store, clock, new SeededRng(1));
            long goldBefore = resumed.Wallet.Get(CurrencyIds.Gold);
            var reward = resumed.ClaimOfflineReward();

            Assert.AreEqual(4, reward.CreditedHours, 0.01);
            Assert.Greater(reward.Gold, 0);
            Assert.AreEqual(goldBefore + reward.Gold, resumed.Wallet.Get(CurrencyIds.Gold));
        }

        [Test]
        public void ConfigJson_RoundTrips()
        {
            var config = MinimalConfig();
            string json = GameConfigLoader.ToJson(config);
            var parsed = GameConfigLoader.FromJson(json);
            Assert.AreEqual("test", parsed.gameId);
            Assert.AreEqual(1, parsed.axes.Count);
            Assert.AreEqual(CurveType.Geometric, parsed.axes[0].cost.type);
            Assert.AreEqual(100, parsed.baseStats[StatType.Attack]);
        }

        [Test]
        public void UnitAcquisition_UpdatesStats()
        {
            var config = MinimalConfig();
            config.units = new List<IdleCore.Gacha.UnitDef>
            {
                new IdleCore.Gacha.UnitDef
                {
                    id = "hero_1",
                    baseEffects = new List<StatEffect>
                    {
                        new StatEffect { stat = StatType.Attack, mode = EffectMode.Add,
                            value = new ValueCurve { baseValue = 50 } },
                    },
                },
            };
            var session = new GameSession(config, new InMemorySaveStore(), new ManualClock(), new SeededRng(1));

            double before = session.Stats.Snapshot().Get(StatType.Attack);
            session.Units.AddCopy("hero_1");
            Assert.IsTrue(session.Units.TryEquip("hero_1"));
            double after = session.Stats.Snapshot().Get(StatType.Attack);

            Assert.AreEqual(before + 50, after, "유닛 장착이 스탯에 자동 반영");
        }
    }
}
