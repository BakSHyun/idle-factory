using System;
using System.Collections.Generic;
using IdleCore.Stats;

namespace IdleCore.Gacha
{
    public sealed class OwnedUnit
    {
        public string unitId;
        public int copies;      // 보유 사본 (돌파에 소모하지 않고 누적 카운트로 표현)
        public int limitBreak;  // 현재 돌파 수치
        public bool equipped;
    }

    /// <summary>
    /// 유닛 보유·돌파·장착 상태. 스탯 기여는 ContributeEffects()로 StatSystem에 주입된다.
    /// </summary>
    public sealed class UnitInventory
    {
        private readonly Dictionary<string, UnitDef> _defs = new Dictionary<string, UnitDef>();
        private readonly Dictionary<string, OwnedUnit> _owned = new Dictionary<string, OwnedUnit>();

        /// <summary>돌파 1단계당 필요한 사본 수 (1돌 = 2번째 사본)</summary>
        public int CopiesPerLimitBreak = 1;
        public int MaxLimitBreak = 10;

        public event Action<string> UnitChanged;

        public UnitInventory(IEnumerable<UnitDef> defs)
        {
            foreach (var d in defs) _defs[d.id] = d;
        }

        public IReadOnlyDictionary<string, UnitDef> Defs => _defs;
        public OwnedUnit Get(string unitId) => _owned.TryGetValue(unitId, out var u) ? u : null;
        public IEnumerable<OwnedUnit> AllOwned() => _owned.Values;

        public void AddCopy(string unitId, int count = 1)
        {
            if (!_defs.ContainsKey(unitId)) throw new ArgumentException($"unknown unit: {unitId}");
            if (!_owned.TryGetValue(unitId, out var unit))
            {
                unit = new OwnedUnit { unitId = unitId };
                _owned[unitId] = unit;
            }
            unit.copies += count;
            // 자동 돌파: 사본이 충분하면 돌파 수치 상승 (수동 돌파를 원하면 여기서 분리)
            int possible = Math.Min(MaxLimitBreak, (unit.copies - 1) / CopiesPerLimitBreak);
            if (possible > unit.limitBreak) unit.limitBreak = possible;
            UnitChanged?.Invoke(unitId);
        }

        public void SetEquipped(string unitId, bool equipped)
        {
            var unit = Get(unitId);
            if (unit == null) throw new ArgumentException($"not owned: {unitId}");
            unit.equipped = equipped;
            UnitChanged?.Invoke(unitId);
        }

        /// <summary>
        /// StatSystem.SetExternalEffects()로 넘길 효과 목록.
        /// 장착 효과 + 돌파 임계 효과(장착 시) + 도감 보유 효과(항상).
        /// </summary>
        public List<StatEffect> ContributeEffects()
        {
            var result = new List<StatEffect>();
            foreach (var unit in _owned.Values)
            {
                var def = _defs[unit.unitId];
                result.AddRange(def.collectionEffects); // 보유만으로 기여
                if (!unit.equipped) continue;
                result.AddRange(def.baseEffects);
                foreach (var lb in def.limitBreakEffects)
                    if (unit.limitBreak >= lb.atLimitBreak)
                        result.Add(lb.effect);
            }
            return result;
        }

        public Dictionary<string, OwnedUnit> Export() => new Dictionary<string, OwnedUnit>(_owned);

        public void Import(Dictionary<string, OwnedUnit> owned)
        {
            _owned.Clear();
            if (owned != null)
                foreach (var kv in owned) _owned[kv.Key] = kv.Value;
        }
    }
}
