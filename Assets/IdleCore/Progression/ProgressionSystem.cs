using System;
using IdleCore.Economy;
using IdleCore.Stats;

namespace IdleCore.Progression
{
    public sealed class FarmResult
    {
        public double Seconds;
        public long Kills;
        public long Gold;
        public long Soul;
        public bool StagePushed;
        public int NewStageIndex = -1;
        /// <summary>체력 부족으로 스테이지에서 밀려남 (적 반격에 사망)</summary>
        public bool Retreated;
    }

    /// <summary>
    /// 방치형 코어 루프: 현재 스테이지에서 파밍 → DPS가 충분해지면 다음 스테이지 돌파.
    /// 전투는 틱이 아니라 수식(DPS vs EHP)으로 평가한다 — 뷰는 결과를 연출만 한다.
    /// </summary>
    public sealed class ProgressionSystem
    {
        private readonly StageCurveConfig _cfg;
        private readonly StatSystem _stats;
        private readonly Wallet _wallet;

        /// <summary>현재 파밍 중인 스테이지 (도전은 다음 스테이지에)</summary>
        public StageId Current { get; private set; }
        public int HighestClearedIndex { get; private set; } = -1;

        public event Action<int> StageCleared;

        public ProgressionSystem(StageCurveConfig cfg, StatSystem stats, Wallet wallet, int currentStageIndex = 0, int highestClearedIndex = -1)
        {
            _cfg = cfg;
            _stats = stats;
            _wallet = wallet;
            Current = new StageId(currentStageIndex);
            HighestClearedIndex = highestClearedIndex;
        }

        public StageCurveConfig Config => _cfg;

        /// <summary>현재 스테이지에서 일반 몹 1마리 처치에 걸리는 초.</summary>
        public double SecondsPerKill()
        {
            double dps = _stats.Snapshot().Dps();
            return StageMath.EnemyHp(_cfg, Current.Index) / Math.Max(0.0001, dps);
        }

        /// <summary>
        /// 이 스테이지에서 생존 가능한가 — 적 반격을 buffer 마리 연속으로 버틸 체력이 있는가.
        /// (enemyAttackBase가 0이면 반격 없음 = 항상 생존)
        /// </summary>
        public bool CanSurvive(int stageIndex)
        {
            double enemyAttack = StageMath.EnemyAttack(_cfg, stageIndex);
            if (enemyAttack <= 0) return true;
            var snapshot = _stats.Snapshot();
            double killSeconds = StageMath.EnemyHp(_cfg, stageIndex) / Math.Max(0.0001, snapshot.Dps());
            double damageTaken = enemyAttack * killSeconds * _cfg.survivalKillBuffer;
            return snapshot.EffectiveHp() >= damageTaken;
        }

        /// <summary>다음 스테이지 보스를 제한시간 안에 잡고 살아남을 수 있는가.</summary>
        public bool CanClearNext()
        {
            int next = HighestClearedIndex + 1;
            if (next > StageMath.MaxStageIndex(_cfg)) return false;
            double dps = _stats.Snapshot().Dps();
            double bossKillSeconds = StageMath.BossHp(_cfg, next) / Math.Max(0.0001, dps);
            return bossKillSeconds <= _cfg.bossTimeLimitSeconds && CanSurvive(next);
        }

        /// <summary>보스 도전이 화력은 충분한데 체력이 부족한 상태인가 (UI 안내용).</summary>
        public bool BlockedBySurvival()
        {
            int next = HighestClearedIndex + 1;
            if (next > StageMath.MaxStageIndex(_cfg)) return false;
            double dps = _stats.Snapshot().Dps();
            double bossKillSeconds = StageMath.BossHp(_cfg, next) / Math.Max(0.0001, dps);
            return bossKillSeconds <= _cfg.bossTimeLimitSeconds && !CanSurvive(next);
        }

        /// <summary>
        /// 다음 스테이지 보스에게 제한시간 동안 넣을 수 있는 피해 비율 (1.0 이상 = 격파 가능).
        /// UI의 '예상 피해 N%' 연출과 도전 버튼 활성 판정의 단일 출처.
        /// </summary>
        public double NextBossDamageRatio()
        {
            int next = HighestClearedIndex + 1;
            if (next > StageMath.MaxStageIndex(_cfg)) return 0;
            double dps = _stats.Snapshot().Dps();
            return dps * _cfg.bossTimeLimitSeconds / StageMath.BossHp(_cfg, next);
        }

        /// <summary>보스 도전. 성공 시 현재 파밍 스테이지도 함께 전진한다.</summary>
        public bool TryPush()
        {
            if (!CanClearNext()) return false;
            HighestClearedIndex += 1;
            Current = new StageId(HighestClearedIndex);
            StageCleared?.Invoke(HighestClearedIndex);
            return true;
        }

        /// <summary>
        /// 경과 시간만큼 파밍을 진행하고 보상을 지갑에 넣는다.
        /// autoPush=true면 시간 진행 후 가능한 만큼 스테이지를 민다.
        /// </summary>
        public FarmResult Advance(double seconds, bool autoPush = true)
        {
            var result = new FarmResult { Seconds = seconds };
            if (seconds <= 0) return result;

            // 적 반격: 현재 스테이지에서 못 버티면 생존 가능한 곳까지 후퇴
            while (Current.Index > 0 && !CanSurvive(Current.Index))
            {
                Current = new StageId(Current.Index - 1);
                result.Retreated = true;
            }

            var snapshot = _stats.Snapshot();
            double killSeconds = SecondsPerKill();
            long kills = (long)Math.Floor(seconds / Math.Max(0.05, killSeconds));
            if (kills > 0)
            {
                double goldMul = Math.Max(0.01, snapshot.Get(StatType.GoldGain) == 0 ? 1 : snapshot.Get(StatType.GoldGain));
                double soulMul = Math.Max(0.01, snapshot.Get(StatType.SoulGain) == 0 ? 1 : snapshot.Get(StatType.SoulGain));
                long gold = (long)(kills * StageMath.GoldPerKill(_cfg, Current.Index) * goldMul);
                long soul = (long)(kills * StageMath.SoulPerKill(_cfg, Current.Index) * soulMul);
                _wallet.Earn(CurrencyIds.Gold, gold);
                _wallet.Earn(CurrencyIds.Soul, soul);
                result.Kills = kills;
                result.Gold = gold;
                result.Soul = soul;
            }

            if (autoPush)
            {
                while (TryPush())
                {
                    result.StagePushed = true;
                    result.NewStageIndex = HighestClearedIndex;
                }
            }
            return result;
        }
    }
}
