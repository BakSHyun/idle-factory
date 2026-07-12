using System;
using System.Collections.Generic;
using System.Linq;
using IdleCore.Stats;

namespace IdleCore.Gacha
{
    public sealed class OwnedUnit
    {
        public string unitId;
        public int copies;      // 보유 사본 (돌파에 소모하지 않고 누적 카운트로 표현)
        public int limitBreak;  // 현재 돌파 수치
        public int level = 1;   // 장비 레벨 (maxLevel=1인 유닛은 항상 1)
        public bool equipped;
    }

    /// <summary>
    /// 유닛 보유·돌파·장착 상태 (소헌키 구조):
    /// - kind별 장착 슬롯 제한 (무기 1 / 장신구 2 / 스킬 4 / 차사 4 등 — config 정의)
    /// - 장착 효과: 장착 중에만. 돌파 임계 효과: 장착 중에만.
    /// - 도감(보유) 효과: 보유만으로 영구 적용 + 등록 수 마일스톤 보너스.
    /// </summary>
    public sealed class UnitInventory
    {
        private readonly Dictionary<string, UnitDef> _defs = new Dictionary<string, UnitDef>();
        private readonly Dictionary<string, OwnedUnit> _owned = new Dictionary<string, OwnedUnit>();
        private readonly Dictionary<string, int> _equipSlots = new Dictionary<string, int>();
        private readonly List<CollectionMilestone> _milestones = new List<CollectionMilestone>();

        /// <summary>돌파 1단계당 필요한 사본 수 (1돌 = 2번째 사본)</summary>
        public int CopiesPerLimitBreak = 1;
        public int MaxLimitBreak = 10;

        public event Action<string> UnitChanged;
        /// <summary>장비 레벨업 성공 시 발생 (미션 집계용)</summary>
        public event Action<string> LeveledUp;

        public UnitInventory(IEnumerable<UnitDef> defs,
            Dictionary<string, int> equipSlots = null,
            List<CollectionMilestone> milestones = null)
        {
            foreach (var d in defs) _defs[d.id] = d;
            if (equipSlots != null) foreach (var kv in equipSlots) _equipSlots[kv.Key] = kv.Value;
            if (milestones != null) _milestones.AddRange(milestones.OrderBy(m => m.count));
        }

        public IReadOnlyDictionary<string, UnitDef> Defs => _defs;
        public OwnedUnit Get(string unitId) => _owned.TryGetValue(unitId, out var u) ? u : null;
        public IEnumerable<OwnedUnit> AllOwned() => _owned.Values;
        public int UniqueOwnedCount => _owned.Count;
        public IReadOnlyList<CollectionMilestone> Milestones => _milestones;

        public int SlotLimit(string kind) => _equipSlots.TryGetValue(kind, out var n) ? n : int.MaxValue;

        public int EquippedCount(string kind) =>
            _owned.Values.Count(u => u.equipped && _defs[u.unitId].kind == kind);

        public void AddCopy(string unitId, int count = 1)
        {
            if (!_defs.ContainsKey(unitId)) throw new ArgumentException($"unknown unit: {unitId}");
            if (!_owned.TryGetValue(unitId, out var unit))
            {
                unit = new OwnedUnit { unitId = unitId };
                _owned[unitId] = unit;
            }
            unit.copies += count;
            // 자동 돌파: 사본이 충분하면 돌파 수치 상승
            int possible = Math.Min(MaxLimitBreak, (unit.copies - 1) / CopiesPerLimitBreak);
            if (possible > unit.limitBreak) unit.limitBreak = possible;
            UnitChanged?.Invoke(unitId);
        }

        /// <summary>현재 장착된 해당 종류 유닛들의 속성 가짓수 (속성당 보너스 슬롯 +1, 최대 3).</summary>
        public int DistinctElementsEquipped(string kind) =>
            _owned.Values
                .Where(u => u.equipped && _defs[u.unitId].kind == kind)
                .Select(u => _defs[u.unitId].element)
                .Where(e => !string.IsNullOrEmpty(e))
                .Distinct().Count();

        /// <summary>실효 슬롯 한도 = 기본 슬롯 + 장착 속성 가짓수 (속성 다양화 인센티브).</summary>
        public int EffectiveSlotLimit(string kind) =>
            SlotLimit(kind) == int.MaxValue ? int.MaxValue
                : SlotLimit(kind) + Math.Min(3, DistinctElementsEquipped(kind));

        /// <summary>장착 시도 — 기본 슬롯 + 속성 보너스 슬롯(속성당 +1) 한도 검사.</summary>
        public bool TryEquip(string unitId)
        {
            var unit = Get(unitId);
            if (unit == null) return false;
            if (unit.equipped) return true;
            var def = _defs[unitId];
            var kind = def.kind;
            int baseLimit = SlotLimit(kind);
            if (baseLimit != int.MaxValue)
            {
                // 이 유닛을 포함했을 때의 속성 가짓수로 한도 계산
                var elements = _owned.Values
                    .Where(u => u.equipped && _defs[u.unitId].kind == kind)
                    .Select(u => _defs[u.unitId].element)
                    .Where(e => !string.IsNullOrEmpty(e))
                    .ToHashSet();
                if (!string.IsNullOrEmpty(def.element)) elements.Add(def.element);
                int limit = baseLimit + Math.Min(3, elements.Count);
                if (EquippedCount(kind) >= limit) return false;
            }
            unit.equipped = true;
            UnitChanged?.Invoke(unitId);
            return true;
        }

        /// <summary>장비 일괄 강화 (x10/x50/MAX). 얻은 레벨 수 반환.</summary>
        public int TryLevelUpMany(string unitId, Economy.Wallet wallet, int count)
        {
            int gained = 0;
            int guard = Math.Min(count, 10000);
            while (gained < guard && TryLevelUp(unitId, wallet)) gained++;
            return gained;
        }

        public void Unequip(string unitId)
        {
            var unit = Get(unitId);
            if (unit == null || !unit.equipped) return;
            unit.equipped = false;
            UnitChanged?.Invoke(unitId);
        }

        /// <summary>합성 비용: 같은 종류·등급의 잉여 사본 N개 → 상위 등급 1개.</summary>
        public int ComposeCost = 5;

        /// <summary>돌파에 더 쓸 수 없는 잉여 사본 수 (10돌 초과분) — 합성의 재료.</summary>
        public int SurplusCopies(string unitId)
        {
            var unit = Get(unitId);
            if (unit == null) return 0;
            return Math.Max(0, unit.copies - (1 + MaxLimitBreak * CopiesPerLimitBreak));
        }

        /// <summary>이 유닛을 승급할 수 있는가 (잉여 사본 충분 + 승급 체인 존재).</summary>
        public bool CanCompose(string unitId)
        {
            var unit = Get(unitId);
            if (unit == null) return false;
            var def = _defs[unitId];
            return def.upgradeToId != null && _defs.ContainsKey(def.upgradeToId)
                && SurplusCopies(unitId) >= ComposeCost;
        }

        /// <summary>
        /// 승급(합성): 같은 유닛의 잉여 사본 ComposeCost개 → 다음 서브등급/등급 1개.
        /// (초급1 낫 5개 → 초급2 낫 1개 → ... → 초급4 → 중급1. 소헌키 서브등급 체계)
        /// 잉여(10돌 초과분)만 소모하므로 돌파 수치는 절대 내려가지 않는다.
        /// </summary>
        public bool TryComposeUnit(string unitId, out string resultUnitId)
        {
            resultUnitId = null;
            if (!CanCompose(unitId)) return false;
            Get(unitId).copies -= ComposeCost;
            resultUnitId = _defs[unitId].upgradeToId;
            AddCopy(resultUnitId);
            return true;
        }

        /// <summary>장비 레벨업 비용 (다음 레벨). 레벨 불가 유닛이거나 만렙이면 -1.</summary>
        public long LevelUpCost(string unitId)
        {
            var unit = Get(unitId);
            if (unit == null) return -1;
            var def = _defs[unitId];
            if (unit.level >= def.maxLevel) return -1;
            return def.LevelUpCostAt(unit.level);
        }

        /// <summary>장비 레벨업 — 재화를 지불하고 레벨 +1 (최대 200, 등급이 높을수록 비쌈).</summary>
        public bool TryLevelUp(string unitId, Economy.Wallet wallet)
        {
            var unit = Get(unitId);
            if (unit == null) return false;
            var def = _defs[unitId];
            if (unit.level >= def.maxLevel) return false;
            if (!wallet.TrySpend(def.levelCostCurrency, def.LevelUpCostAt(unit.level))) return false;
            unit.level++;
            UnitChanged?.Invoke(unitId);
            LeveledUp?.Invoke(unitId);
            return true;
        }

        /// <summary>등급→돌파 순으로 슬롯을 채운다 (뉴비 자동 장착 / 봇 정책).</summary>
        public void AutoEquipBest()
        {
            foreach (var u in _owned.Values) u.equipped = false;
            foreach (var group in _owned.Values.GroupBy(u => _defs[u.unitId].kind))
            {
                int limit = SlotLimit(group.Key);
                foreach (var unit in group
                    .OrderByDescending(u => _defs[u.unitId].grade)
                    .ThenByDescending(u => u.limitBreak)
                    .Take(limit))
                    unit.equipped = true;
            }
            UnitChanged?.Invoke("");
        }

        /// <summary>
        /// StatSystem.SetExternalEffects()로 넘길 효과 목록.
        /// 장착 효과 + 돌파 임계 효과(장착 시) + 도감 보유 효과(항상) + 도감 마일스톤(항상).
        /// </summary>
        public List<StatEffect> ContributeEffects()
        {
            var result = new List<StatEffect>();
            foreach (var unit in _owned.Values)
            {
                var def = _defs[unit.unitId];
                result.AddRange(def.collectionEffects); // 보유만으로 기여 (도감)
                // 한계돌파 비례 보유 효과 (돌파할수록 강해짐 — 항상 적용)
                if (unit.limitBreak > 0)
                    foreach (var scaling in def.limitBreakScalingEffects)
                        result.Add(new StatEffect
                        {
                            stat = scaling.stat,
                            mode = scaling.mode,
                            value = new ValueCurve { baseValue = scaling.value.Evaluate(unit.limitBreak) },
                        });
                if (!unit.equipped) continue;
                result.AddRange(def.baseEffects);
                // 장비 레벨 효과: 곡선에 현재 레벨을 대입한 값을 즉석 효과로 변환
                foreach (var levelEffect in def.levelEffects)
                    result.Add(new StatEffect
                    {
                        stat = levelEffect.stat,
                        mode = levelEffect.mode,
                        value = new ValueCurve { baseValue = levelEffect.value.Evaluate(unit.level) },
                    });
                foreach (var lb in def.limitBreakEffects)
                    if (unit.limitBreak >= lb.atLimitBreak)
                        result.Add(lb.effect);
            }
            foreach (var milestone in _milestones)
                if (UniqueOwnedCount >= milestone.count)
                    result.AddRange(milestone.effects);
            return result;
        }

        public Dictionary<string, OwnedUnit> Export() => new Dictionary<string, OwnedUnit>(_owned);

        public void Import(Dictionary<string, OwnedUnit> owned)
        {
            _owned.Clear();
            if (owned != null)
                foreach (var kv in owned)
                    if (_defs.ContainsKey(kv.Key)) _owned[kv.Key] = kv.Value;
        }
    }
}
