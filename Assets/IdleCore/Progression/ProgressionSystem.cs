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
    /// 보스전 시뮬레이션 결과 — 실제 전투 타임라인 (UI가 이걸 연출로 재생한다).
    /// 승패는 세 시계의 경주: 보스를 잡는 시간 vs 내가 죽는 시간 vs 제한시간.
    /// </summary>
    public sealed class BossFightPreview
    {
        public double TimeToKillBoss;   // 보스 처치까지 (초)
        public double TimeToDie;        // 내 사망까지 (초, 반격 없으면 무한대)
        public double TimeLimit;        // 제한시간
        public bool Victory;
        /// <summary>clear | death(먼저 죽음) | timeout(시간 초과)</summary>
        public string Reason;
        /// <summary>전투 종료 시각 (셋 중 먼저 온 것)</summary>
        public double EndTime;
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
        /// <summary>이 스테이지에서의 처치 수 — killsToBoss를 채우면 보스 도전 가능</summary>
        public long KillsOnStage { get; set; }

        /// <summary>보스 등장 조건(처치 수) 충족 여부.</summary>
        public bool BossGateOpen =>
            _cfg.killsToBoss <= 0 || KillsOnStage >= _cfg.killsToBoss;

        public event Action<int> StageCleared;
        private double _carrySeconds;

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

        /// <summary>
        /// 다음 스테이지 수문장과의 전투를 시뮬레이션한다 (결정론).
        /// UI는 이 타임라인을 압축 재생해 '실제로 투닥거리는' 전투를 보여준다.
        /// </summary>
        public BossFightPreview SimulateBossFight()
        {
            int next = HighestClearedIndex + 1;
            if (next > StageMath.MaxStageIndex(_cfg))
                return new BossFightPreview { Victory = false, Reason = "timeout", TimeLimit = 0 };

            var snapshot = _stats.Snapshot();
            double dps = Math.Max(0.0001, snapshot.Dps());
            double timeToKill = StageMath.BossHp(_cfg, next) / dps;

            double bossDps = StageMath.EnemyAttack(_cfg, next) * _cfg.bossAttackMultiplier;
            double timeToDie = bossDps > 0 ? snapshot.EffectiveHp() / bossDps : double.PositiveInfinity;

            double limit = _cfg.bossTimeLimitSeconds;
            var preview = new BossFightPreview
            {
                TimeToKillBoss = timeToKill,
                TimeToDie = timeToDie,
                TimeLimit = limit,
            };
            if (timeToKill <= Math.Min(timeToDie, limit))
            {
                preview.Victory = true;
                preview.Reason = "clear";
                preview.EndTime = timeToKill;
            }
            else if (timeToDie < limit)
            {
                preview.Reason = "death";
                preview.EndTime = timeToDie;
            }
            else
            {
                preview.Reason = "timeout";
                preview.EndTime = limit;
            }
            return preview;
        }

        /// <summary>다음 스테이지 수문장을 이길 수 있는가 (시뮬레이션 결과).</summary>
        public bool CanClearNext() => SimulateBossFight().Victory;

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

        /// <summary>보스 도전. 처치 게이트가 열려 있고 격파·생존 가능해야 한다. 성공 시 게이트 리셋.</summary>
        public bool TryPush()
        {
            if (!BossGateOpen || !CanClearNext()) return false;
            HighestClearedIndex += 1;
            Current = new StageId(HighestClearedIndex);
            KillsOnStage = 0;
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
            double killSeconds = Math.Max(0.05, SecondsPerKill());
            // 진행 캐리: 킬 시간이 틱보다 길어도 소수점 진행이 누적된다 (0킬 고착 버그 방지)
            _carrySeconds += seconds;
            long kills = (long)Math.Floor(_carrySeconds / killSeconds);
            _carrySeconds -= kills * killSeconds;
            if (_carrySeconds > killSeconds * 2) _carrySeconds = killSeconds * 2; // 스탯 급변 시 폭주 방지
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
                KillsOnStage += kills;
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
