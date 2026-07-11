using System;
using IdleCore.Stats;

namespace IdleCore.Progression
{
    public sealed class OfflineConfig
    {
        /// <summary>기본 오프라인 보상 상한(시간). 멤버십/성장 축이 OfflineCapHours로 확장한다.</summary>
        public double baseCapHours = 8;
        /// <summary>오프라인은 온라인 파밍 효율의 몇 %인가 (1.0 = 동일)</summary>
        public double efficiency = 0.6;
    }

    public sealed class OfflineReward
    {
        public double CreditedHours;
        public long Gold;
        public long Soul;
    }

    /// <summary>
    /// 오프라인(방치) 보상 = 경과시간(상한 적용) × 현재 스테이지 파밍 효율 × 오프라인 배율.
    /// 소울헌터의 '별/달의 가호' 패턴: 멤버십이 OfflineRate/OfflineCapHours 축을 올린다.
    /// </summary>
    public static class OfflineRewardCalculator
    {
        public static OfflineReward Calculate(
            OfflineConfig cfg,
            StageCurveConfig stageCfg,
            StatSnapshot stats,
            int currentStageIndex,
            TimeSpan elapsed)
        {
            double capHours = cfg.baseCapHours + Math.Max(0, stats.Get(StatType.OfflineCapHours));
            double hours = Math.Min(capHours, Math.Max(0, elapsed.TotalHours));

            double dps = stats.Dps();
            double killSeconds = StageMath.EnemyHp(stageCfg, currentStageIndex) / Math.Max(0.0001, dps);
            double killsPerHour = 3600.0 / Math.Max(0.05, killSeconds);

            double rateMul = stats.Get(StatType.OfflineRate);
            if (rateMul <= 0) rateMul = 1;

            double totalKills = killsPerHour * hours * cfg.efficiency * rateMul;
            return new OfflineReward
            {
                CreditedHours = hours,
                Gold = (long)(totalKills * StageMath.GoldPerKill(stageCfg, currentStageIndex)),
                Soul = (long)(totalKills * StageMath.SoulPerKill(stageCfg, currentStageIndex)),
            };
        }
    }
}
