using System;
using System.Collections.Generic;
using IdleCore.Economy;
using IdleCore.Stats;

namespace IdleCore.Progression
{
    /// <summary>
    /// 재화별 전용 던전 (소헌키의 골드/영혼/각성석 던전).
    /// 층 등반형 — 입장하면 최대한 밀고, 보상은 최고 클리어 층 기준. 소탕권 = 입장 없이 즉시 보상.
    /// </summary>
    public sealed class DungeonDef
    {
        public string id;
        public string name;
        public int unlockStageIndex;
        public int maxFloor = 200;
        public int freeEntriesPerDay = 3;
        public double timeLimitSeconds = 30;
        public double enemyHpBase = 2000;
        public double enemyHpGrowth = 1.35;
        public string rewardCurrency = CurrencyIds.Gold;
        public double rewardBase = 5000;
        public double rewardGrowth = 1.22;

        public double FloorHp(int floor) => enemyHpBase * Math.Pow(enemyHpGrowth, floor - 1);
        public long FloorReward(int floor) =>
            floor <= 0 ? 0 : (long)Math.Ceiling(rewardBase * Math.Pow(rewardGrowth, floor - 1));
    }

    public sealed class DungeonState
    {
        public string defId;
        public int highestFloorCleared;
        public int entriesUsedToday;
        public DateTime lastEntryDateUtc;
    }

    public sealed class DungeonRunResult
    {
        public int FloorsCleared;        // 이번 입장에서 새로 민 층수
        public int HighestFloor;
        public string RewardCurrency;
        public long RewardAmount;
    }

    public sealed class DungeonSystem
    {
        private readonly Dictionary<string, DungeonDef> _defs = new Dictionary<string, DungeonDef>();
        private readonly Dictionary<string, DungeonState> _states = new Dictionary<string, DungeonState>();
        private readonly Wallet _wallet;
        private readonly StatSystem _stats;
        private readonly IClock _clock;

        /// <summary>던전 입장(도전) 시 발생 — 미션 집계용</summary>
        public event Action<string> Challenged;

        public DungeonSystem(IEnumerable<DungeonDef> defs, Wallet wallet, StatSystem stats, IClock clock)
        {
            foreach (var d in defs) _defs[d.id] = d;
            _wallet = wallet;
            _stats = stats;
            _clock = clock;
        }

        public IReadOnlyDictionary<string, DungeonDef> Defs => _defs;

        public DungeonState State(string dungeonId)
        {
            if (!_states.TryGetValue(dungeonId, out var s))
            {
                s = new DungeonState { defId = dungeonId };
                _states[dungeonId] = s;
            }
            if (s.lastEntryDateUtc.Date != _clock.UtcNow.Date) s.entriesUsedToday = 0;
            return s;
        }

        public bool IsUnlocked(string dungeonId, int highestClearedStage) =>
            _defs.TryGetValue(dungeonId, out var d) && highestClearedStage >= d.unlockStageIndex;

        public int RemainingFreeEntries(string dungeonId) =>
            Math.Max(0, _defs[dungeonId].freeEntriesPerDay - State(dungeonId).entriesUsedToday);

        /// <summary>
        /// 무료 입장 1회 소모 → DPS가 닿는 만큼 층을 밀고, 최고 층 보상을 지급한다.
        /// (층을 못 밀어도 최고 층 파밍 보상은 지급 — 입장이 헛되지 않게)
        /// </summary>
        public DungeonRunResult TryChallenge(string dungeonId, int highestClearedStage)
        {
            if (!IsUnlocked(dungeonId, highestClearedStage)) return null;
            if (RemainingFreeEntries(dungeonId) <= 0) return null;

            var def = _defs[dungeonId];
            var state = State(dungeonId);
            state.entriesUsedToday++;
            state.lastEntryDateUtc = _clock.UtcNow;
            Challenged?.Invoke(dungeonId);

            double dps = _stats.Snapshot().Dps();
            int cleared = 0;
            while (state.highestFloorCleared < def.maxFloor)
            {
                int next = state.highestFloorCleared + 1;
                if (def.FloorHp(next) > dps * def.timeLimitSeconds) break;
                state.highestFloorCleared = next;
                cleared++;
            }

            return GrantReward(def, state, cleared);
        }

        /// <summary>소탕권 1장 → 최고 층 보상 즉시 수령 (1층 이상 클리어 필요).</summary>
        public DungeonRunResult TrySweep(string dungeonId)
        {
            var state = State(dungeonId);
            if (state.highestFloorCleared < 1) return null;
            if (!_wallet.TrySpend(CurrencyIds.SweepTicket, 1)) return null;
            return GrantReward(_defs[dungeonId], state, 0);
        }

        /// <summary>광고 소탕 — 티켓 없이 소탕 보상 (AdGateway.Use 성공 콜백에서 호출).</summary>
        public DungeonRunResult GrantAdSweep(string dungeonId)
        {
            var state = State(dungeonId);
            if (state.highestFloorCleared < 1) return null;
            return GrantReward(_defs[dungeonId], state, 0);
        }

        private DungeonRunResult GrantReward(DungeonDef def, DungeonState state, int cleared)
        {
            long reward = def.FloorReward(state.highestFloorCleared);
            if (reward > 0) _wallet.Earn(def.rewardCurrency, reward);
            return new DungeonRunResult
            {
                FloorsCleared = cleared,
                HighestFloor = state.highestFloorCleared,
                RewardCurrency = def.rewardCurrency,
                RewardAmount = reward,
            };
        }

        public Dictionary<string, DungeonState> Export() => new Dictionary<string, DungeonState>(_states);

        public void Import(Dictionary<string, DungeonState> states)
        {
            _states.Clear();
            if (states != null) foreach (var kv in states) _states[kv.Key] = kv.Value;
        }
    }
}
