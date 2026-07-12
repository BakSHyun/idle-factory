using System;
using System.Collections.Generic;
using IdleCore.Economy;

namespace IdleCore.LiveOps
{
    /// <summary>
    /// 일일 미션 — 일일 루틴의 뼈대. 메트릭(kill/summon/dungeon/levelup/login/offline_claim)을
    /// 시스템 이벤트로 자동 집계하고, 유저가 수동으로 보상을 수령한다 (수령 행위 = 접속 루틴).
    /// </summary>
    public sealed class MissionDef
    {
        public string id;
        public string name;
        /// <summary>집계 메트릭 id — GameSession이 시스템 이벤트를 이 이름으로 Report한다</summary>
        public string metric;
        public long target = 1;
        public List<CurrencyGrant> rewards = new List<CurrencyGrant>();
    }

    public sealed class MissionState
    {
        public string defId;
        public long progress;
        public bool claimed;
        public DateTime lastResetUtc;
    }

    public sealed class MissionSystem
    {
        private readonly Dictionary<string, MissionDef> _defs = new Dictionary<string, MissionDef>();
        private readonly Dictionary<string, MissionState> _states = new Dictionary<string, MissionState>();
        private readonly Wallet _wallet;
        private readonly IClock _clock;

        /// <summary>보상 배율 훅 (재화 id → 배율). 영옥 획득량 스탯(장식 보유 효과) 적용 통로.</summary>
        public Func<string, double> RewardMultiplier;

        public MissionSystem(IEnumerable<MissionDef> defs, Wallet wallet, IClock clock)
        {
            foreach (var d in defs) _defs[d.id] = d;
            _wallet = wallet;
            _clock = clock;
        }

        private long Scaled(CurrencyGrant g) =>
            (long)(g.amount * (RewardMultiplier?.Invoke(g.currency) ?? 1.0));

        public IReadOnlyDictionary<string, MissionDef> Defs => _defs;

        public MissionState State(string missionId)
        {
            if (!_states.TryGetValue(missionId, out var s))
            {
                s = new MissionState { defId = missionId, lastResetUtc = _clock.UtcNow };
                _states[missionId] = s;
            }
            if (s.lastResetUtc.Date != _clock.UtcNow.Date) // 일일 리셋
            {
                s.progress = 0;
                s.claimed = false;
                s.lastResetUtc = _clock.UtcNow;
            }
            return s;
        }

        /// <summary>시스템 이벤트 집계 (GameSession이 배선).</summary>
        public void Report(string metric, long amount)
        {
            if (amount <= 0) return;
            foreach (var def in _defs.Values)
            {
                if (def.metric != metric) continue;
                var state = State(def.id);
                state.progress = Math.Min(def.target, state.progress + amount);
            }
        }

        public bool CanClaim(string missionId)
        {
            if (!_defs.TryGetValue(missionId, out var def)) return false;
            var state = State(missionId);
            return !state.claimed && state.progress >= def.target;
        }

        public bool TryClaim(string missionId)
        {
            if (!CanClaim(missionId)) return false;
            State(missionId).claimed = true;
            foreach (var g in _defs[missionId].rewards) _wallet.Earn(g.currency, Scaled(g));
            return true;
        }

        public int ClaimAll()
        {
            int claimed = 0;
            foreach (var id in _defs.Keys)
                if (TryClaim(id)) claimed++;
            return claimed;
        }

        public Dictionary<string, MissionState> Export() => new Dictionary<string, MissionState>(_states);

        public void Import(Dictionary<string, MissionState> states)
        {
            _states.Clear();
            if (states != null) foreach (var kv in states) _states[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// 무료 출석부 — 28일 사이클, 연속 아니어도 됨 (누적 방식, 소헌키 스타일).
    /// 하루 1회 수령, 사이클 완주 시 처음부터 반복.
    /// </summary>
    public sealed class AttendanceDay
    {
        public List<CurrencyGrant> rewards = new List<CurrencyGrant>();
    }

    public sealed class AttendanceState
    {
        public int totalClaims;
        public DateTime lastClaimUtc;
    }

    public sealed class AttendanceSystem
    {
        private readonly List<AttendanceDay> _days;
        private readonly Wallet _wallet;
        private readonly IClock _clock;

        /// <summary>보상 배율 훅 (영옥 획득량 스탯 적용 통로).</summary>
        public Func<string, double> RewardMultiplier;

        public AttendanceState State { get; private set; }

        public AttendanceSystem(List<AttendanceDay> days, Wallet wallet, IClock clock, AttendanceState state = null)
        {
            _days = days ?? new List<AttendanceDay>();
            _wallet = wallet;
            _clock = clock;
            State = state ?? new AttendanceState();
        }

        public int DayCount => _days.Count;
        /// <summary>이번 사이클에서 오늘 받을 일차 (1부터)</summary>
        public int CurrentDay => _days.Count == 0 ? 0 : State.totalClaims % _days.Count + 1;

        public bool CanClaimToday() =>
            _days.Count > 0 && State.lastClaimUtc.Date < _clock.UtcNow.Date;

        public bool TryClaimToday()
        {
            if (!CanClaimToday()) return false;
            var day = _days[State.totalClaims % _days.Count];
            foreach (var g in day.rewards)
                _wallet.Earn(g.currency, (long)(g.amount * (RewardMultiplier?.Invoke(g.currency) ?? 1.0)));
            State.totalClaims++;
            State.lastClaimUtc = _clock.UtcNow;
            return true;
        }
    }
}
