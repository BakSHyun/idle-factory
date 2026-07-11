using System;
using System.Collections.Generic;
using IdleCore;
using IdleCore.Economy;
using IdleCore.Stats;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class SubscriptionTests
    {
        private static SubscriptionDef Pension() => new SubscriptionDef
        {
            id = "pension", price = 1490, durationDays = 30,
            dailyGrant = new List<CurrencyGrant>
                { new CurrencyGrant { currency = CurrencyIds.GemHard, amount = 100 } },
        };

        private static SubscriptionDef Membership() => new SubscriptionDef
        {
            id = "membership", price = 990, durationDays = 30,
            effects = new List<StatEffect>
            {
                new StatEffect { stat = StatType.OfflineCapHours, mode = EffectMode.Add,
                    value = new ValueCurve { baseValue = 4 } },
            },
        };

        private static SubscriptionDef AdPass() => new SubscriptionDef
        {
            id = "ad_pass", price = 990, durationDays = 30, adSkip = true,
        };

        private static (SubscriptionSystem, Wallet, ManualClock) Build(params SubscriptionDef[] defs)
        {
            var clock = new ManualClock();
            var wallet = new Wallet();
            wallet.Earn(CurrencyIds.GemHard, 100000);
            return (new SubscriptionSystem(defs, wallet, clock), wallet, clock);
        }

        [Test]
        public void Purchase_ActivatesForDuration()
        {
            var (subs, _, clock) = Build(Membership());
            Assert.IsFalse(subs.IsActive("membership"));
            Assert.IsTrue(subs.TryPurchase("membership"));
            Assert.IsTrue(subs.IsActive("membership"));

            clock.Advance(TimeSpan.FromDays(29));
            Assert.IsTrue(subs.IsActive("membership"));
            clock.Advance(TimeSpan.FromDays(2));
            Assert.IsFalse(subs.IsActive("membership"), "30일 후 만료");
        }

        [Test]
        public void Renewal_ExtendsFromExpiry_NotFromNow()
        {
            var (subs, _, clock) = Build(Membership());
            subs.TryPurchase("membership");
            clock.Advance(TimeSpan.FromDays(20)); // 10일 남음
            subs.TryPurchase("membership");       // 갱신
            clock.Advance(TimeSpan.FromDays(39)); // 총 59일 경과, 60일권
            Assert.IsTrue(subs.IsActive("membership"), "갱신은 만료일에 이어 붙는다 (기간 손실 없음)");
        }

        [Test]
        public void Pension_DailyClaim_OncePerDay()
        {
            var (subs, wallet, clock) = Build(Pension());
            long start = wallet.Get(CurrencyIds.GemHard);
            subs.TryPurchase("pension"); // -1490

            Assert.IsTrue(subs.TryClaimDaily("pension"));
            Assert.IsFalse(subs.TryClaimDaily("pension"), "하루 2회 불가");
            clock.Advance(TimeSpan.FromDays(1));
            Assert.IsTrue(subs.TryClaimDaily("pension"));

            Assert.AreEqual(start - 1490 + 200, wallet.Get(CurrencyIds.GemHard));
        }

        [Test]
        public void Pension_FullPeriod_DoublesValue()
        {
            var (subs, wallet, clock) = Build(Pension());
            subs.TryPurchase("pension");
            long claimed = 0;
            for (int day = 0; day < 30; day++)
            {
                if (subs.TryClaimDaily("pension")) claimed += 100;
                clock.Advance(TimeSpan.FromDays(1));
            }
            Assert.AreEqual(3000, claimed, "30일 완주 = 1,490 결제 → 3,000 회수 (2배 환급)");
        }

        [Test]
        public void MembershipEffects_OnlyWhileActive()
        {
            var (subs, _, clock) = Build(Membership());
            Assert.AreEqual(0, subs.ContributeEffects().Count);
            subs.TryPurchase("membership");
            Assert.AreEqual(1, subs.ContributeEffects().Count);
            clock.Advance(TimeSpan.FromDays(31));
            Assert.AreEqual(0, subs.ContributeEffects().Count, "만료 후 효과 소멸");
        }

        [Test]
        public void AdGateway_SkipSubscriber_NoAdShown()
        {
            var (subs, _, clock) = Build(AdPass());
            var adapter = new FakeAdAdapter();
            var gateway = new AdGateway(
                new[] { new AdSlotDef { id = "offline_double", dailyLimit = 3 } },
                adapter, subs, clock);

            subs.TryPurchase("ad_pass");
            bool granted = false;
            gateway.Use("offline_double", ok => granted = ok);

            Assert.IsTrue(granted, "스킵 구독자: 보상 즉시 수령");
            Assert.AreEqual(0, adapter.ShownCount, "광고는 재생되지 않음 (Skip'it)");
            Assert.AreEqual(2, gateway.RemainingToday("offline_double"), "횟수는 동일하게 소모");
        }

        [Test]
        public void AdGateway_NonSubscriber_WatchesAd_DailyLimitEnforced()
        {
            var (subs, _, clock) = Build(AdPass()); // 구매 안 함
            var adapter = new FakeAdAdapter();
            var gateway = new AdGateway(
                new[] { new AdSlotDef { id = "offline_double", dailyLimit = 2 } },
                adapter, subs, clock);

            int grantedCount = 0;
            for (int i = 0; i < 3; i++) gateway.Use("offline_double", ok => { if (ok) grantedCount++; });

            Assert.AreEqual(2, grantedCount, "일 한도 2회");
            Assert.AreEqual(2, adapter.ShownCount, "비구독자는 광고 시청");
            Assert.AreEqual(0, gateway.RemainingToday("offline_double"));

            clock.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual(2, gateway.RemainingToday("offline_double"), "자정 리셋");
        }
    }
}
