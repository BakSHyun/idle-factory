using System.Collections.Generic;

namespace IdleCore.Stats
{
    public enum EffectMode
    {
        /// <summary>스탯 가산치에 더한다 (같은 스탯의 Add끼리는 합산).</summary>
        Add,
        /// <summary>스탯 배율 곱에 (1 + value)로 곱한다 — 곱연산 축.</summary>
        Mul,
    }

    public enum CurveType
    {
        Linear,    // value = baseValue + perLevel * level
        Geometric, // value = baseValue * growth^level
    }

    public sealed class ValueCurve
    {
        public CurveType type = CurveType.Linear;
        public double baseValue;
        public double perLevel;   // Linear용
        public double growth = 1; // Geometric용

        public double Evaluate(int level)
        {
            switch (type)
            {
                case CurveType.Geometric:
                    return baseValue * System.Math.Pow(growth, level);
                default:
                    return baseValue + perLevel * level;
            }
        }
    }

    public sealed class StatEffect
    {
        public StatType stat;
        public EffectMode mode = EffectMode.Add;
        public ValueCurve value = new ValueCurve();
    }

    /// <summary>
    /// 성장 축 — 훈련/장비 강화/속성/유물 등 모든 성장 시스템의 공통 표현.
    /// 신규 성장 축 추가 = 이 정의를 JSON에 하나 추가하는 것 (코드 수정 없음).
    /// </summary>
    public sealed class GrowthAxisDef
    {
        public string id;
        public string name;
        public int maxLevel = int.MaxValue;
        /// <summary>레벨업 비용 재화 id (예: "gold", "soul")</summary>
        public string costCurrency = "gold";
        public ValueCurve cost = new ValueCurve();
        public List<StatEffect> effects = new List<StatEffect>();
        /// <summary>해금 스테이지 (챕터*1000+단계 인덱스, 0이면 처음부터)</summary>
        public int unlockStageIndex;

        public long CostAtLevel(int currentLevel)
        {
            double c = cost.Evaluate(currentLevel);
            return c <= 0 ? 0 : (long)System.Math.Ceiling(c);
        }
    }
}
