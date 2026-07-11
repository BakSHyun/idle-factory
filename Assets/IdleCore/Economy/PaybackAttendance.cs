using System;
using System.Collections.Generic;

namespace IdleCore.Economy
{
    public sealed class PaybackAttendanceDef
    {
        public string id;
        public string name;
        /// <summary>구매 가격 (하드커런시). 완주 시 전액 환급 — "무료나 다름없는 첫 결제" 장치.</summary>
        public long price = 490;
        public int days = 7;
        /// <summary>일차별 보상 (days 길이). 마지막 날 보상에 환급이 포함되지 않아도 완주 시 price 전액 환급.</summary>
        public List<CurrencyGrant> dailyReward = new List<CurrencyGrant>();
    }

    public sealed class PaybackAttendanceState
    {
        public string defId;
        public DateTime purchasedUtc;
        public int claimedDays;
        public bool refunded;
    }

    /// <summary>
    /// 페이백 출석 (소울헌터 v15): 490 결제 → 구매일부터 7일 출석 → 완주 시 전액 환급.
    /// 결제 습관 형성이 목적이므로 환급 재화는 반드시 하드커런시.
    /// </summary>
    public sealed class PaybackAttendanceSystem
    {
        private readonly PaybackAttendanceDef _def;
        private readonly Wallet _wallet;
        private readonly IClock _clock;

        public PaybackAttendanceState State { get; private set; }

        public PaybackAttendanceSystem(PaybackAttendanceDef def, Wallet wallet, IClock clock, PaybackAttendanceState state = null)
        {
            _def = def;
            _wallet = wallet;
            _clock = clock;
            State = state;
        }

        public PaybackAttendanceDef Def => _def;
        public bool IsActive => State != null && !State.refunded;

        public bool TryPurchase()
        {
            if (IsActive) return false;
            if (!_wallet.TrySpend(CurrencyIds.GemHard, _def.price)) return false;
            State = new PaybackAttendanceState { defId = _def.id, purchasedUtc = _clock.UtcNow };
            return true;
        }

        /// <summary>오늘 출석 보상을 받을 수 있는가 (구매일 = 1일차, 하루 1회).</summary>
        public bool CanClaimToday()
        {
            if (!IsActive || State.claimedDays >= _def.days) return false;
            int dayNumber = (int)(_clock.UtcNow.Date - State.purchasedUtc.Date).TotalDays + 1;
            return dayNumber > State.claimedDays;
        }

        public bool TryClaimToday()
        {
            if (!CanClaimToday()) return false;
            State.claimedDays++;
            foreach (var g in _def.dailyReward) _wallet.Earn(g.currency, g.amount);
            if (State.claimedDays >= _def.days)
            {
                _wallet.Earn(CurrencyIds.GemHard, _def.price); // 완주 → 전액 환급
                State.refunded = true;
            }
            return true;
        }
    }

    /// <summary>
    /// 누적 사용 페이백 (소울헌터의 '누적 사용 이벤트'): 기간 내 재화 '소비'량 구간별 환급.
    /// 보유가 아니라 소비에 보상 → 가챠/성장 회전율을 높인다.
    /// </summary>
    public sealed class CumulativeSpendPayback
    {
        public sealed class Tier
        {
            public long threshold;
            public long reward;
        }

        public string currency = CurrencyIds.GemHard;
        public List<Tier> tiers = new List<Tier>();

        /// <param name="spentInWindow">이벤트 기간 내 소비량 (Wallet lifetime 스냅샷 차이로 계산)</param>
        /// <param name="alreadyClaimedThresholds">이미 수령한 구간</param>
        public List<Tier> ClaimableTiers(long spentInWindow, HashSet<long> alreadyClaimedThresholds)
        {
            var result = new List<Tier>();
            foreach (var tier in tiers)
                if (spentInWindow >= tier.threshold && !alreadyClaimedThresholds.Contains(tier.threshold))
                    result.Add(tier);
            return result;
        }
    }
}
