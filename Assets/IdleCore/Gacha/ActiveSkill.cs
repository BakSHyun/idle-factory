using System.Collections.Generic;

namespace IdleCore.Gacha
{
    /// <summary>
    /// 액티브 스킬 정의 (차사/스킬 유닛 공용):
    /// - cooldown형: N초마다 자동 시전
    /// - proc형: 평타 1회당 N% 확률 발동
    /// 피해량은 'DPS × damagePerHit초어치 × 타격수'로 환산되어 실제 파밍 보상에 반영된다.
    /// </summary>
    public sealed class ActiveSkillSpec
    {
        public string name = "스킬";
        /// <summary>cooldown | proc</summary>
        public string trigger = "cooldown";
        public double cooldown = 8;
        /// <summary>proc형: 평타당 발동 확률 (0~1)</summary>
        public double procChance;
        /// <summary>타격 1회 피해 = DPS × 이 값(초어치)</summary>
        public double damagePerHit = 1.5;
        public int hitCount = 1;
        /// <summary>상태이상 (burn/shock/chaos — 속성 연동 표기)</summary>
        public string statusEffect;
        /// <summary>시전 시 상태이상 부여 확률 (0~1). 부여 시 1초어치 추가 피해</summary>
        public double statusChance;
    }

    /// <summary>
    /// 돌파 마일스톤 — 1/3/6/9/10성에서 자기 스킬을 보조/증폭.
    /// (스탯형 돌파 효과는 기존 limitBreakEffects 사용 — 예: 10성 공격력 +200%)
    /// </summary>
    public sealed class LimitBreakSkillMod
    {
        public int atLimitBreak;
        public string description;
        /// <summary>쿨타임 감소 (초)</summary>
        public double cooldownReduction;
        /// <summary>스킬 피해량 증가 (0.3 = +30%)</summary>
        public double damageBonus;
        /// <summary>발동/상태이상 확률 증가 (proc형은 발동 확률, cooldown형은 상태이상 확률)</summary>
        public double chanceBonus;
        /// <summary>타격수 추가</summary>
        public int extraHits;
    }
}
