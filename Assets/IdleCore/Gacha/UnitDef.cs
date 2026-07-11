using System.Collections.Generic;
using IdleCore.Stats;

namespace IdleCore.Gacha
{
    public enum UnitGrade
    {
        Rare,      // 특급
        Epic,      // 전설
        Mythic,    // 신화
        Ancient,   // 고대
    }

    /// <summary>돌파 임계 효과 — "9돌파에서 핵심 효과 발동" 패턴을 데이터로 표현.</summary>
    public sealed class LimitBreakEffect
    {
        /// <summary>이 돌파 수치에 도달해야 발동</summary>
        public int atLimitBreak;
        public StatEffect effect = new StatEffect();
    }

    /// <summary>
    /// 소환형 수집 유닛 (스킬/동료/유물 등 — kind는 표기용, 시스템 동작은 동일).
    /// </summary>
    public sealed class UnitDef
    {
        public string id;
        public string name;
        public string kind = "hero"; // hero | skill | relic ... 게임 스킨이 정의
        public UnitGrade grade = UnitGrade.Rare;
        /// <summary>장착 시 발동하는 기본 효과</summary>
        public List<StatEffect> baseEffects = new List<StatEffect>();
        /// <summary>돌파 누적 효과 (도달한 임계까지 전부 적용)</summary>
        public List<LimitBreakEffect> limitBreakEffects = new List<LimitBreakEffect>();
        /// <summary>도감(보유) 효과 — 장착하지 않아도 계정에 기여 ('잉여 제로' 원칙)</summary>
        public List<StatEffect> collectionEffects = new List<StatEffect>();
    }
}
