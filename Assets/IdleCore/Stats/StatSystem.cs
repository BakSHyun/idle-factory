using System;
using System.Collections.Generic;

namespace IdleCore.Stats
{
    /// <summary>계산 완료된 스탯 스냅샷. 축 레벨이 바뀔 때만 재계산한다.</summary>
    public sealed class StatSnapshot
    {
        private readonly Dictionary<StatType, double> _values;

        internal StatSnapshot(Dictionary<StatType, double> values) => _values = values;

        public double Get(StatType stat) => _values.TryGetValue(stat, out var v) ? v : 0;

        /// <summary>유효 체력 — 생존 판정의 단일 출처 (방어도가 체력을 증폭).</summary>
        public double EffectiveHp()
        {
            double hp = Math.Max(1, Get(StatType.Health));
            double defense = Math.Max(0, Get(StatType.Defense));
            return hp * (1 + defense / 100.0);
        }

        /// <summary>전투력 — DPS와 생존력의 종합 지표 (HUD 표시용).</summary>
        public double CombatPower() => Dps() * 8 + EffectiveHp() * 4;

        /// <summary>초당 피해량 — 전투 수식의 단일 출처.</summary>
        public double Dps()
        {
            double attack = Get(StatType.Attack);
            double speed = Math.Max(0.1, Get(StatType.AttackSpeed));
            double critChance = Math.Min(1.0, Get(StatType.CritChance));
            double critMult = Math.Max(1.0, Get(StatType.CritMultiplier));
            double final = Get(StatType.FinalDamage);
            if (final <= 0) final = 1; // 배율 스탯의 부재 = 1 (0이면 DPS가 붕괴한다)
            double critFactor = 1 + critChance * (critMult - 1);
            return attack * speed * critFactor * final;
        }
    }

    /// <summary>
    /// 성장 축들의 레벨을 보관하고 스탯을 계산한다.
    /// 같은 스탯: Add끼리 합산 → Mul은 (1+v)씩 곱산. 축 사이의 곱연산은 Mul 효과로 표현된다.
    /// </summary>
    public sealed class StatSystem
    {
        private readonly Dictionary<string, GrowthAxisDef> _axes = new Dictionary<string, GrowthAxisDef>();
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();
        private readonly List<StatEffect> _externalEffects = new List<StatEffect>();
        private readonly Dictionary<StatType, double> _baseStats = new Dictionary<StatType, double>();
        private StatSnapshot _cached;

        public event Action SnapshotInvalidated;
        /// <summary>성장 축 레벨업 시 발생 (미션 집계용)</summary>
        public event Action<string> LeveledUp;

        public StatSystem(IEnumerable<GrowthAxisDef> axes, Dictionary<StatType, double> baseStats = null)
        {
            foreach (var axis in axes) _axes[axis.id] = axis;
            if (baseStats != null) _baseStats = baseStats;
        }

        public IReadOnlyDictionary<string, GrowthAxisDef> Axes => _axes;

        public int GetLevel(string axisId) => _levels.TryGetValue(axisId, out var l) ? l : 0;

        public void SetLevel(string axisId, int level)
        {
            _levels[axisId] = level;
            Invalidate();
        }

        public long NextCost(string axisId) => _axes[axisId].CostAtLevel(GetLevel(axisId));

        public bool CanLevelUp(string axisId) =>
            _axes.TryGetValue(axisId, out var axis) && GetLevel(axisId) < axis.maxLevel;

        public void LevelUp(string axisId)
        {
            if (!CanLevelUp(axisId)) throw new InvalidOperationException($"axis maxed or unknown: {axisId}");
            _levels[axisId] = GetLevel(axisId) + 1;
            Invalidate();
            LeveledUp?.Invoke(axisId);
        }

        /// <summary>일괄 강화: 재화가 되는 만큼 최대 count 레벨 (x10/x50/MAX 버튼용). 얻은 레벨 수 반환.</summary>
        public int LevelUpMany(string axisId, Economy.Wallet wallet, int count)
        {
            int gained = 0;
            int guard = Math.Min(count, 10000);
            while (gained < guard && CanLevelUp(axisId))
            {
                var axis = _axes[axisId];
                if (!wallet.TrySpend(axis.costCurrency, axis.CostAtLevel(GetLevel(axisId)))) break;
                _levels[axisId] = GetLevel(axisId) + 1;
                gained++;
                LeveledUp?.Invoke(axisId);
            }
            if (gained > 0) Invalidate();
            return gained;
        }

        /// <summary>축 외 효과(유닛 돌파, 도감 보너스, 패스 버프 등)를 주입하는 통로.</summary>
        public void SetExternalEffects(IEnumerable<StatEffect> effects)
        {
            _externalEffects.Clear();
            _externalEffects.AddRange(effects);
            Invalidate();
        }

        public Dictionary<string, int> ExportLevels() => new Dictionary<string, int>(_levels);

        public void ImportLevels(Dictionary<string, int> levels)
        {
            _levels.Clear();
            if (levels != null)
                foreach (var kv in levels) _levels[kv.Key] = kv.Value;
            Invalidate();
        }

        public StatSnapshot Snapshot()
        {
            if (_cached != null) return _cached;

            var adds = new Dictionary<StatType, double>(_baseStats);
            var muls = new Dictionary<StatType, double>();

            void Apply(StatEffect effect, int level)
            {
                double v = effect.value.Evaluate(level);
                if (effect.mode == EffectMode.Add)
                {
                    adds.TryGetValue(effect.stat, out var cur);
                    adds[effect.stat] = cur + v;
                }
                else
                {
                    muls.TryGetValue(effect.stat, out var cur);
                    muls[effect.stat] = (cur == 0 ? 1 : cur) * (1 + v);
                }
            }

            foreach (var kv in _axes)
            {
                int level = GetLevel(kv.Key);
                if (level <= 0) continue;
                foreach (var effect in kv.Value.effects) Apply(effect, level);
            }
            // 외부 효과는 효과 자체가 최종값이므로 level=1 기준으로 평가
            foreach (var effect in _externalEffects) Apply(effect, 1);

            var final = new Dictionary<StatType, double>();
            foreach (var kv in adds) final[kv.Key] = kv.Value;
            foreach (var kv in muls)
            {
                final.TryGetValue(kv.Key, out var baseV);
                // Add가 전혀 없는 배율 스탯(FinalDamage 등)은 1을 기저로 곱한다
                final[kv.Key] = (baseV == 0 ? 1 : baseV) * kv.Value;
            }

            _cached = new StatSnapshot(final);
            return _cached;
        }

        private void Invalidate()
        {
            _cached = null;
            SnapshotInvalidated?.Invoke();
        }
    }
}
