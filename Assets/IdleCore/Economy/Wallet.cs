using System;
using System.Collections.Generic;

namespace IdleCore.Economy
{
    public static class CurrencyIds
    {
        public const string GemHard = "gem_hard"; // 유료 재화 (소울헌터의 레드 다이아)
        public const string GemSoft = "gem_soft"; // 보상 재화 (블루 다이아)
        public const string Gold = "gold";
        public const string Soul = "soul";
        public const string Mileage = "mileage";         // 소환 1회 = 1 적립 (천장 대체)
        public const string SweepTicket = "sweep_ticket"; // 던전 소탕권 (병목 재화)
    }

    /// <summary>
    /// 재화 지갑. 누적 획득/소비를 함께 기록한다 — 누적 사용 페이백, 누적 구매 이벤트의 데이터 소스.
    /// </summary>
    public sealed class Wallet
    {
        private readonly Dictionary<string, long> _balances = new Dictionary<string, long>();
        private readonly Dictionary<string, long> _lifetimeEarned = new Dictionary<string, long>();
        private readonly Dictionary<string, long> _lifetimeSpent = new Dictionary<string, long>();

        /// <summary>(currencyId, delta, newBalance)</summary>
        public event Action<string, long, long> BalanceChanged;

        public long Get(string currency) => _balances.TryGetValue(currency, out var v) ? v : 0;
        public long LifetimeEarned(string currency) => _lifetimeEarned.TryGetValue(currency, out var v) ? v : 0;
        public long LifetimeSpent(string currency) => _lifetimeSpent.TryGetValue(currency, out var v) ? v : 0;

        public void Earn(string currency, long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            _balances[currency] = Get(currency) + amount;
            _lifetimeEarned[currency] = LifetimeEarned(currency) + amount;
            BalanceChanged?.Invoke(currency, amount, _balances[currency]);
        }

        public bool CanAfford(string currency, long amount) => Get(currency) >= amount;

        public bool TrySpend(string currency, long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!CanAfford(currency, amount)) return false;
            _balances[currency] = Get(currency) - amount;
            _lifetimeSpent[currency] = LifetimeSpent(currency) + amount;
            BalanceChanged?.Invoke(currency, -amount, _balances[currency]);
            return true;
        }

        public Dictionary<string, long> ExportBalances() => new Dictionary<string, long>(_balances);
        public Dictionary<string, long> ExportLifetimeEarned() => new Dictionary<string, long>(_lifetimeEarned);
        public Dictionary<string, long> ExportLifetimeSpent() => new Dictionary<string, long>(_lifetimeSpent);

        public void Import(Dictionary<string, long> balances, Dictionary<string, long> earned, Dictionary<string, long> spent)
        {
            _balances.Clear(); _lifetimeEarned.Clear(); _lifetimeSpent.Clear();
            if (balances != null) foreach (var kv in balances) _balances[kv.Key] = kv.Value;
            if (earned != null) foreach (var kv in earned) _lifetimeEarned[kv.Key] = kv.Value;
            if (spent != null) foreach (var kv in spent) _lifetimeSpent[kv.Key] = kv.Value;
        }
    }
}
