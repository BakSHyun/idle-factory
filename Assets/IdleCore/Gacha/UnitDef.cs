using System.Collections.Generic;
using IdleCore.Stats;

namespace IdleCore.Gacha
{
    public enum UnitGrade
    {
        Beginner,     // 초급
        Intermediate, // 중급
        Advanced,     // 상급
        Rare,         // 특급
        Epic,         // 전설
        Mythic,       // 신화
        Ancient,      // 고대 (런칭 미출시 — 업데이트 카드)
        Eternal,      // 영원 (런칭 미출시)
    }

    /// <summary>돌파 임계 효과 — "9돌파에서 핵심 효과 발동" 패턴을 데이터로 표현.</summary>
    public sealed class LimitBreakEffect
    {
        /// <summary>이 돌파 수치에 도달해야 발동</summary>
        public int atLimitBreak;
        public StatEffect effect = new StatEffect();
    }

    /// <summary>도감 마일스톤 — 고유 유닛 N종 등록 시 계정 전체 효과 (소헌키 도감 패턴).</summary>
    public sealed class CollectionMilestone
    {
        public int count;
        public List<StatEffect> effects = new List<StatEffect>();
    }

    /// <summary>
    /// 소환형 수집 유닛. kind(hero/skill)별로 장착 슬롯이 제한된다 — 보유 효과(도감)와 장착 효과는 별개.
    /// </summary>
    public sealed class UnitDef
    {
        public string id;
        public string name;
        public string kind = "hero"; // weapon | orb | ornament | skill | hero — 게임 스킨이 정의
        public UnitGrade grade = UnitGrade.Rare;
        /// <summary>장착 시 발동하는 기본 효과</summary>
        public List<StatEffect> baseEffects = new List<StatEffect>();
        /// <summary>돌파 누적 효과 (도달한 임계까지 전부 적용)</summary>
        public List<LimitBreakEffect> limitBreakEffects = new List<LimitBreakEffect>();
        /// <summary>도감(보유) 효과 — 장착하지 않아도 계정에 기여 ('잉여 제로' 원칙)</summary>
        public List<StatEffect> collectionEffects = new List<StatEffect>();

        // ── 장비 레벨 (소헌키: 대낫/오브/장식은 개별 강화, 최대 200) ──
        /// <summary>1이면 레벨 없음(스킬/차사), 장비는 200</summary>
        public int maxLevel = 1;
        public string levelCostCurrency = "gold";
        /// <summary>레벨업 비용 곡선 — 등급이 높을수록 baseValue가 크다 (config에서 설정)</summary>
        public ValueCurve levelCost = new ValueCurve();
        /// <summary>레벨에 비례해 커지는 효과 (장착 중에만, level을 곡선에 대입)</summary>
        public List<StatEffect> levelEffects = new List<StatEffect>();

        public long LevelUpCostAt(int currentLevel)
        {
            double c = levelCost.Evaluate(currentLevel);
            return c <= 0 ? 0 : (long)System.Math.Ceiling(c);
        }
    }
}
