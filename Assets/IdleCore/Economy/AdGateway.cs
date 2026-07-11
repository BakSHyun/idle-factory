using System;
using System.Collections.Generic;

namespace IdleCore.Economy
{
    /// <summary>보상형 광고 슬롯 — 어디서(슬롯) 하루 몇 번(dailyLimit) 광고 보상을 줄지 데이터로 정의.</summary>
    public sealed class AdSlotDef
    {
        public string id;           // 예: "offline_double", "dungeon_sweep_bonus"
        public string name;
        public int dailyLimit = 3;
    }

    /// <summary>
    /// 보상형 광고의 단일 관문. BM의 근본 규칙이 여기 있다:
    /// 광고 스킵 구독자는 광고를 '보지 않고' 같은 보상을 받는다 (Skip'it 패턴).
    /// 보상 지급 자체는 호출부 책임 — 이 클래스는 시청/스킵/횟수만 판정한다.
    /// </summary>
    public sealed class AdGateway
    {
        private readonly Dictionary<string, AdSlotDef> _slots = new Dictionary<string, AdSlotDef>();
        private readonly Dictionary<string, int> _todayUses = new Dictionary<string, int>();
        private DateTime _todayDate;
        private readonly IAdAdapter _adapter;
        private readonly SubscriptionSystem _subscriptions;
        private readonly IClock _clock;

        public AdGateway(IEnumerable<AdSlotDef> slots, IAdAdapter adapter, SubscriptionSystem subscriptions, IClock clock)
        {
            foreach (var s in slots) _slots[s.id] = s;
            _adapter = adapter;
            _subscriptions = subscriptions;
            _clock = clock;
            _todayDate = clock.UtcNow.Date;
        }

        public IReadOnlyDictionary<string, AdSlotDef> Slots => _slots;

        public int RemainingToday(string slotId)
        {
            RollDateIfNeeded();
            if (!_slots.TryGetValue(slotId, out var slot)) return 0;
            _todayUses.TryGetValue(slotId, out var used);
            return Math.Max(0, slot.dailyLimit - used);
        }

        /// <summary>이 슬롯을 지금 쓸 수 있는가 (횟수 + 광고 준비 여부. 스킵 구독자는 광고 준비 불필요).</summary>
        public bool CanUse(string slotId) =>
            RemainingToday(slotId) > 0 && (_subscriptions.HasAdSkip() || _adapter.IsRewardedAdReady);

        /// <summary>
        /// 슬롯 사용: 스킵 구독자는 즉시, 아니면 광고 시청 후 onGranted(true).
        /// onGranted(false)는 광고 중도 이탈/실패 — 횟수를 소모하지 않는다.
        /// </summary>
        public void Use(string slotId, Action<bool> onGranted)
        {
            if (!CanUse(slotId)) { onGranted?.Invoke(false); return; }

            if (_subscriptions.HasAdSkip())
            {
                Consume(slotId);
                onGranted?.Invoke(true);
                return;
            }
            _adapter.ShowRewardedAd(ok =>
            {
                if (ok) Consume(slotId);
                onGranted?.Invoke(ok);
            });
        }

        private void Consume(string slotId)
        {
            RollDateIfNeeded();
            _todayUses.TryGetValue(slotId, out var used);
            _todayUses[slotId] = used + 1;
        }

        private void RollDateIfNeeded()
        {
            if (_clock.UtcNow.Date == _todayDate) return;
            _todayDate = _clock.UtcNow.Date;
            _todayUses.Clear();
        }

        public Dictionary<string, int> ExportTodayUses() => new Dictionary<string, int>(_todayUses);
        public DateTime ExportTodayDate() => _todayDate;

        public void Import(Dictionary<string, int> uses, DateTime date)
        {
            _todayUses.Clear();
            if (uses != null) foreach (var kv in uses) _todayUses[kv.Key] = kv.Value;
            _todayDate = date;
            RollDateIfNeeded();
        }
    }
}
