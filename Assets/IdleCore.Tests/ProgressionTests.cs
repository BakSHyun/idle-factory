using System;
using System.Collections.Generic;
using IdleCore;
using IdleCore.Economy;
using IdleCore.Progression;
using IdleCore.Stats;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class ProgressionTests
    {
        private static StatSystem StatsWithDps(double attack)
        {
            return new StatSystem(new GrowthAxisDef[0], new Dictionary<StatType, double>
            {
                { StatType.Attack, attack },
                { StatType.AttackSpeed, 1 },
                { StatType.CritMultiplier, 1 },
            });
        }

        [Test]
        public void StageId_ChapterStepDisplay()
        {
            var cfg = new StageCurveConfig { stepsPerChapter = 5 };
            Assert.AreEqual("1-1", new StageId(0).Display(cfg));
            Assert.AreEqual("1-5", new StageId(4).Display(cfg));
            Assert.AreEqual("2-1", new StageId(5).Display(cfg));
            Assert.IsTrue(new StageId(4).IsBossStage(cfg));
            Assert.IsFalse(new StageId(3).IsBossStage(cfg));
        }

        [Test]
        public void Advance_GrantsFarmRewards()
        {
            var cfg = new StageCurveConfig { enemyHpBase = 100, goldPerKillBase = 10, soulPerKillBase = 1 };
            var wallet = new Wallet();
            var progression = new ProgressionSystem(cfg, StatsWithDps(100), wallet);

            // DPS 100 vs HP 100 → 1초당 1킬. 10초 → 10킬
            var result = progression.Advance(10, autoPush: false);
            Assert.AreEqual(10, result.Kills);
            Assert.AreEqual(100, wallet.Get(CurrencyIds.Gold));
            Assert.AreEqual(10, wallet.Get(CurrencyIds.Soul));
        }

        [Test]
        public void Push_BlockedUntilDpsSufficient()
        {
            var cfg = new StageCurveConfig
            {
                enemyHpBase = 1000, enemyHpGrowth = 2.0,
                bossHpMultiplier = 10, bossTimeLimitSeconds = 10,
            };
            var wallet = new Wallet();

            // 스테이지 0 보스 EHP = 1000×10 = 10000, 제한 10초 → DPS 1000 필요
            var weak = new ProgressionSystem(cfg, StatsWithDps(500), wallet);
            Assert.IsFalse(weak.CanClearNext());
            Assert.IsFalse(weak.TryPush());

            var strong = new ProgressionSystem(cfg, StatsWithDps(1000), wallet);
            Assert.IsTrue(strong.CanClearNext());
            Assert.IsTrue(strong.TryPush());
            Assert.AreEqual(0, strong.HighestClearedIndex);
        }

        [Test]
        public void AutoPush_StopsAtWall()
        {
            var cfg = new StageCurveConfig
            {
                enemyHpBase = 10, enemyHpGrowth = 10.0, // 스테이지당 ×10 급성장 → 벽이 빨리 옴
                bossHpMultiplier = 1, bossTimeLimitSeconds = 1,
            };
            var wallet = new Wallet();
            var progression = new ProgressionSystem(cfg, StatsWithDps(1000), wallet);

            progression.Advance(1);
            // DPS 1000, 제한 1초: EHP 10(s0), 100(s1), 1000(s2)까지 클리어. 10000(s3)은 벽.
            Assert.AreEqual(2, progression.HighestClearedIndex);
            Assert.IsFalse(progression.CanClearNext());
        }

        [Test]
        public void OfflineReward_CappedAtLimit()
        {
            var offlineCfg = new OfflineConfig { baseCapHours = 8, efficiency = 1.0 };
            var stageCfg = new StageCurveConfig { enemyHpBase = 100, goldPerKillBase = 10 };
            var stats = StatsWithDps(100); // 1킬/초

            var reward24h = OfflineRewardCalculator.Calculate(
                offlineCfg, stageCfg, stats.Snapshot(), 0, TimeSpan.FromHours(24));
            Assert.AreEqual(8, reward24h.CreditedHours, "상한 8시간");

            var reward4h = OfflineRewardCalculator.Calculate(
                offlineCfg, stageCfg, stats.Snapshot(), 0, TimeSpan.FromHours(4));
            Assert.AreEqual(4, reward4h.CreditedHours);
            Assert.AreEqual(reward24h.Gold, reward4h.Gold * 2, "8h 상한 보상 = 4h의 2배");
        }

        [Test]
        public void OfflineReward_MembershipExtendsCap()
        {
            // 소울헌터 '별/달의 가호' 패턴: 멤버십이 OfflineCapHours를 확장
            var offlineCfg = new OfflineConfig { baseCapHours = 8, efficiency = 1.0 };
            var stageCfg = new StageCurveConfig();
            var stats = new StatSystem(new GrowthAxisDef[0], new Dictionary<StatType, double>
            {
                { StatType.Attack, 100 }, { StatType.AttackSpeed, 1 },
                { StatType.CritMultiplier, 1 }, { StatType.OfflineCapHours, 4 },
            });
            var reward = OfflineRewardCalculator.Calculate(
                offlineCfg, stageCfg, stats.Snapshot(), 0, TimeSpan.FromHours(24));
            Assert.AreEqual(12, reward.CreditedHours);
        }
    }
}
