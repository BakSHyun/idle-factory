using System;
using System.Collections.Generic;
using IdleCore.Stats;

namespace IdleCore.Economy
{
    /// <summary>
    /// 구독 상품 정의 — BM의 기반 캐시플로우.
    /// 연금(매일 하드 지급), 멤버십(오프라인 확장 + 성장 축), 광고 스킵이 전부 이 하나로 표현된다.
    /// </summary>
    public sealed class SubscriptionDef
    {
        public string id;
        public string name;
        /// <summary>구매 가격 (하드커런시, 가격 사다리 준수)</summary>
        public long price;
        public int durationDays = 30;
        /// <summary>구독 중 상시 적용되는 스탯 효과 (OfflineCapHours, OfflineRate, GoldGain 등)</summary>
        public List<StatEffect> effects = new List<StatEffect>();
        /// <summary>매일 1회 수령 보상 (연금: 매일 수정 100)</summary>
        public List<CurrencyGrant> dailyGrant = new List<CurrencyGrant>();
        /// <summary>보상형 광고를 보지 않고 보상 수령 (광고 스킵 — BM의 근본)</summary>
        public bool adSkip;
    }

    public sealed class SubscriptionState
    {
        public string defId;
        public DateTime expiresUtc;
        /// <summary>일일 보상 마지막 수령일 (UTC 날짜)</summary>
        public DateTime lastDailyClaimUtc;
    }

    /// <summary>
    /// 구독 상태 머신. 만료 판정·갱신·일일 보상·광고 스킵 여부의 단일 출처.
    /// </summary>
    public sealed class SubscriptionSystem
    {
        private readonly Dictionary<string, SubscriptionDef> _defs = new Dictionary<string, SubscriptionDef>();
        private readonly Dictionary<string, SubscriptionState> _states = new Dictionary<string, SubscriptionState>();
        private readonly Wallet _wallet;
        private readonly IClock _clock;

        public event Action<string> SubscriptionChanged;

        public SubscriptionSystem(IEnumerable<SubscriptionDef> defs, Wallet wallet, IClock clock)
        {
            foreach (var d in defs) _defs[d.id] = d;
            _wallet = wallet;
            _clock = clock;
        }

        public IReadOnlyDictionary<string, SubscriptionDef> Defs => _defs;

        public bool IsActive(string subId) =>
            _states.TryGetValue(subId, out var s) && s.expiresUtc > _clock.UtcNow;

        public DateTime? ExpiresAt(string subId) =>
            _states.TryGetValue(subId, out var s) ? s.expiresUtc : (DateTime?)null;

        /// <summary>구매/갱신. 이미 활성 상태면 만료일에 기간을 이어 붙인다 (기간 손실 없음).</summary>
        public bool TryPurchase(string subId)
        {
            if (!_defs.TryGetValue(subId, out var def)) return false;
            if (!_wallet.TrySpend(CurrencyIds.GemHard, def.price)) return false;

            var now = _clock.UtcNow;
            if (_states.TryGetValue(subId, out var state) && state.expiresUtc > now)
                state.expiresUtc = state.expiresUtc.AddDays(def.durationDays);
            else
                _states[subId] = new SubscriptionState { defId = subId, expiresUtc = now.AddDays(def.durationDays) };
            SubscriptionChanged?.Invoke(subId);
            return true;
        }

        /// <summary>오늘의 구독 일일 보상 수령 (연금). 구독별 1일 1회.</summary>
        public bool TryClaimDaily(string subId)
        {
            if (!IsActive(subId)) return false;
            var def = _defs[subId];
            if (def.dailyGrant.Count == 0) return false;
            var state = _states[subId];
            if (state.lastDailyClaimUtc.Date >= _clock.UtcNow.Date) return false;

            state.lastDailyClaimUtc = _clock.UtcNow;
            foreach (var g in def.dailyGrant) _wallet.Earn(g.currency, g.amount);
            return true;
        }

        /// <summary>활성 구독 중 광고 스킵 보유 여부 — AdGateway가 조회한다.</summary>
        public bool HasAdSkip()
        {
            foreach (var kv in _defs)
                if (kv.Value.adSkip && IsActive(kv.Key)) return true;
            return false;
        }

        /// <summary>활성 구독의 스탯 효과 — StatSystem 외부 효과로 주입된다.</summary>
        public List<StatEffect> ContributeEffects()
        {
            var result = new List<StatEffect>();
            foreach (var kv in _defs)
                if (IsActive(kv.Key)) result.AddRange(kv.Value.effects);
            return result;
        }

        public Dictionary<string, SubscriptionState> Export() => new Dictionary<string, SubscriptionState>(_states);

        public void Import(Dictionary<string, SubscriptionState> states)
        {
            _states.Clear();
            if (states != null) foreach (var kv in states) _states[kv.Key] = kv.Value;
        }
    }
}
