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

        /// <summary>장착 시도 — kind 슬롯이 가득 차 있으면 실패.</summary>
        public bool TryEquip(string unitId)
        {
            var unit = Get(unitId);
            if (unit == null) return false;
            if (unit.equipped) return true;
            var kind = _defs[unitId].kind;
            if (EquippedCount(kind) >= SlotLimit(kind)) return false;
            unit.equipped = true;
            UnitChanged?.Invoke(unitId);
            return true;
        }

        public void Unequip(string unitId)
        {
            var unit = Get(unitId);
            if (unit == null || !unit.equipped) return;
            unit.equipped = false;
            UnitChanged?.Invoke(unitId);
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
