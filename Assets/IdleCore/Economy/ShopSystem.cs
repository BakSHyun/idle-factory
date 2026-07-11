using System;
using System.Collections.Generic;

namespace IdleCore.Economy
{
    public sealed class PurchaseRecord
    {
        public string productId;
        public DateTime whenUtc;
        public long pricePaid;
    }

    /// <summary>
    /// 인게임 상점. 구매 이력을 남긴다 — 누적 구매 이벤트 상점("하드커런시로 산 것만 카운트"),
    /// 일일/주간/총 구매 제한, 페이백 계산의 데이터 소스.
    /// </summary>
    public sealed class ShopSystem
    {
        private readonly Dictionary<string, ProductDef> _products = new Dictionary<string, ProductDef>();
        private readonly List<PurchaseRecord> _history = new List<PurchaseRecord>();
        private readonly Wallet _wallet;
        private readonly IClock _clock;
        private readonly Gacha.UnitInventory _units;

        public event Action<ProductDef> Purchased;

        public ShopSystem(IEnumerable<ProductDef> products, Wallet wallet, IClock clock,
            Gacha.UnitInventory units = null)
        {
            foreach (var p in products) _products[p.id] = p;
            _wallet = wallet;
            _clock = clock;
            _units = units;
        }

        public IReadOnlyDictionary<string, ProductDef> Products => _products;
        public IReadOnlyList<PurchaseRecord> History => _history;

        public int PurchaseCount(string productId, DateTime? sinceUtc = null)
        {
            int count = 0;
            foreach (var r in _history)
                if (r.productId == productId && (sinceUtc == null || r.whenUtc >= sinceUtc))
                    count++;
            return count;
        }

        /// <summary>기간 내 '누적 구매 이벤트 카운트 대상' 구매 횟수 (이벤트 상점 마일스톤용)</summary>
        public int CountablePurchases(DateTime fromUtc, DateTime toUtc)
        {
            int count = 0;
            foreach (var r in _history)
            {
                if (r.whenUtc < fromUtc || r.whenUtc >= toUtc) continue;
                if (_products.TryGetValue(r.productId, out var p) && p.countsForPurchaseEvents)
                    count++;
            }
            return count;
        }

        public bool IsPurchasable(string productId, int currentStageIndex)
        {
            if (!_products.TryGetValue(productId, out var p)) return false;
            if (currentStageIndex < p.unlockStageIndex) return false;
            switch (p.limitType)
            {
                case PurchaseLimitType.Total:
                    return PurchaseCount(productId) < p.limitCount;
                case PurchaseLimitType.Daily:
                    return PurchaseCount(productId, _clock.UtcNow.Date) < p.limitCount;
                case PurchaseLimitType.Weekly:
                    var weekStart = _clock.UtcNow.Date.AddDays(-(int)_clock.UtcNow.DayOfWeek);
                    return PurchaseCount(productId, weekStart) < p.limitCount;
                default:
                    return true;
            }
        }

        public bool TryPurchase(string productId, int currentStageIndex)
        {
            if (!IsPurchasable(productId, currentStageIndex)) return false;
            var p = _products[productId];
            if (p.unitChoice != null) return false; // 선택권 상품은 TryPurchaseUnitChoice로만
            if (!_wallet.TrySpend(p.costCurrency, p.price)) return false;

            foreach (var g in p.grants) _wallet.Earn(g.currency, g.amount);
            _history.Add(new PurchaseRecord { productId = productId, whenUtc = _clock.UtcNow, pricePaid = p.price });
            Purchased?.Invoke(p);
            return true;
        }

        /// <summary>선택 가능한 유닛 목록 (선택권 상품 UI용).</summary>
        public List<Gacha.UnitDef> ChoiceCandidates(string productId)
        {
            var result = new List<Gacha.UnitDef>();
            if (_units == null || !_products.TryGetValue(productId, out var p) || p.unitChoice == null)
                return result;
            foreach (var def in _units.Defs.Values)
                if (def.grade == p.unitChoice.grade &&
                    (p.unitChoice.kind == null || def.kind == p.unitChoice.kind))
                    result.Add(def);
            return result;
        }

        /// <summary>선택권 구매: 조건에 맞는 유닛을 골라 사본 획득 (마일리지 상점의 핵심).</summary>
        public bool TryPurchaseUnitChoice(string productId, int currentStageIndex, string chosenUnitId)
        {
            if (_units == null || !IsPurchasable(productId, currentStageIndex)) return false;
            var p = _products[productId];
            if (p.unitChoice == null) return false;
            if (!ChoiceCandidates(productId).Exists(d => d.id == chosenUnitId)) return false;
            if (!_wallet.TrySpend(p.costCurrency, p.price)) return false;

            _units.AddCopy(chosenUnitId, p.unitChoice.copies);
            foreach (var g in p.grants) _wallet.Earn(g.currency, g.amount);
            _history.Add(new PurchaseRecord { productId = productId, whenUtc = _clock.UtcNow, pricePaid = p.price });
            Purchased?.Invoke(p);
            return true;
        }

        public List<PurchaseRecord> ExportHistory() => new List<PurchaseRecord>(_history);

        public void ImportHistory(List<PurchaseRecord> history)
        {
            _history.Clear();
            if (history != null) _history.AddRange(history);
        }
    }
}
