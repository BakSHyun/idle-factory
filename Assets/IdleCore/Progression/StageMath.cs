using System;

namespace IdleCore.Progression
{
    /// <summary>
    /// 스테이지 테이블은 JSON에 790챕터를 나열하지 않는다 — 곡선 설정에서 즉석 계산한다.
    /// (실제 방치형의 표준: 확장 = 상한만 올리면 됨)
    /// </summary>
    public sealed class StageCurveConfig
    {
        public int stepsPerChapter = 5;
        public int maxChapter = 100;          // 격주 업데이트 = 이 값 올리기 + 밸런스 재검
        public double enemyHpBase = 50;
        public double enemyHpGrowth = 1.13;   // 스테이지 인덱스당 지수 성장
        public double enemyCountPerWave = 5;
        public double bossHpMultiplier = 8;   // 보스 EHP = 일반 몹 HP × 이 배율
        public double goldPerKillBase = 10;
        public double goldPerKillGrowth = 1.09;
        public double soulPerKillBase = 1;
        public double soulPerKillGrowth = 1.05;
        /// <summary>보스 제한시간(초) — 이 안에 못 잡으면 도전 실패 = 파밍 모드</summary>
        public double bossTimeLimitSeconds = 30;
        /// <summary>적 공격력 (0 = 반격 없음/무적 파밍). 체력이 달리면 스테이지에서 밀려난다</summary>
        public double enemyAttackBase = 0;
        public double enemyAttackGrowth = 1.14;
        /// <summary>생존 판정: 몇 마리를 연속으로 버틸 체력이 필요한가</summary>
        public double survivalKillBuffer = 3;
        /// <summary>보스 등장에 필요한 처치 수 (0 = 게이트 없음). '왜 진행 안 되는지'를 보이게 하는 장치</summary>
        public int killsToBoss = 0;
        /// <summary>수문장 공격력 = 일반 몹 공격력 × 이 배율</summary>
        public double bossAttackMultiplier = 2.5;
        /// <summary>적 방어도 (0 = 없음). '방어 무시' 스탯(장식)이 상쇄한다</summary>
        public double enemyDefenseBase = 0;
        public double enemyDefenseGrowth = 1.10;
    }

    /// <summary>챕터-단계 (예: 12-3). 내부적으로는 0부터 시작하는 선형 인덱스.</summary>
    public readonly struct StageId
    {
        public readonly int Index;

        public StageId(int index) => Index = Math.Max(0, index);

        public int Chapter(StageCurveConfig cfg) => Index / cfg.stepsPerChapter + 1;
        public int Step(StageCurveConfig cfg) => Index % cfg.stepsPerChapter + 1;
        public bool IsBossStage(StageCurveConfig cfg) => Step(cfg) == cfg.stepsPerChapter;
        public StageId Next() => new StageId(Index + 1);

        public string Display(StageCurveConfig cfg) => $"{Chapter(cfg)}-{Step(cfg)}";
    }

    public static class StageMath
    {
        public static double EnemyHp(StageCurveConfig cfg, int stageIndex) =>
            cfg.enemyHpBase * Math.Pow(cfg.enemyHpGrowth, stageIndex);

        public static double BossHp(StageCurveConfig cfg, int stageIndex) =>
            EnemyHp(cfg, stageIndex) * cfg.bossHpMultiplier;

        public static double EnemyAttack(StageCurveConfig cfg, int stageIndex) =>
            cfg.enemyAttackBase <= 0 ? 0 : cfg.enemyAttackBase * Math.Pow(cfg.enemyAttackGrowth, stageIndex);

        public static double EnemyDefense(StageCurveConfig cfg, int stageIndex) =>
            cfg.enemyDefenseBase <= 0 ? 0 : cfg.enemyDefenseBase * Math.Pow(cfg.enemyDefenseGrowth, stageIndex);

        /// <summary>적 방어도를 반영한 실효 DPS — 방어 무시(장식)가 방어도를 상쇄한다.</summary>
        public static double MitigatedDps(StageCurveConfig cfg, double dps, double defensePierce, int stageIndex)
        {
            double defense = Math.Max(0, EnemyDefense(cfg, stageIndex) - Math.Max(0, defensePierce));
            return dps * 100.0 / (100.0 + defense);
        }

        public static double GoldPerKill(StageCurveConfig cfg, int stageIndex) =>
            cfg.goldPerKillBase * Math.Pow(cfg.goldPerKillGrowth, stageIndex);

        public static double SoulPerKill(StageCurveConfig cfg, int stageIndex) =>
            cfg.soulPerKillBase * Math.Pow(cfg.soulPerKillGrowth, stageIndex);

        public static int MaxStageIndex(StageCurveConfig cfg) =>
            cfg.maxChapter * cfg.stepsPerChapter - 1;
    }
}
