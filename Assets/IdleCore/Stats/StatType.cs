namespace IdleCore.Stats
{
    /// <summary>
    /// 전투/경제 수식에 들어가는 스탯. 성장 축(GrowthAxis)의 효과 대상.
    /// </summary>
    public enum StatType
    {
        Attack,          // 기본 공격력 (가산 기반)
        AttackSpeed,     // 초당 공격 횟수 배율
        CritChance,      // 치명타 확률 (0~1)
        CritMultiplier,  // 치명타 피해 배율
        FinalDamage,     // 최종 피해 배율 (곱연산 축들의 주 무대)
        Health,
        Defense,
        GoldGain,        // 골드 획득 배율
        SoulGain,        // 소울(성장 재화) 획득 배율
        OfflineRate,     // 오프라인 보상 배율
        OfflineCapHours, // 오프라인 보상 상한 (시간, 가산)
        FireDamage,      // 불 속성 유닛 데미지 배율
        LightningDamage, // 번개 속성 유닛 데미지 배율
        DarkDamage,      // 어둠 속성 유닛 데미지 배율
        AllElementDamage,// 모든 속성 피해 배율 (오브 장착 효과)
        DefensePierce,   // 방어 무시 (적 방어도 상쇄, 장식 장착 효과)
        SoftGemGain,     // 영옥 획득량 배율 (미션/출석 보상에 적용)
        HeroSkillDamage, // 영웅(차사) 스킬 피해량 배율 (차사 보유 효과)
    }
}
