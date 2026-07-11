using System.Collections.Generic;
using IdleCore;
using IdleCore.Economy;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class EconomyTests
    {
        [Test]
        public void Wallet_TracksLifetimeSpend()
        {
            var wallet = new Wallet();
            wallet.Earn(CurrencyIds.GemHard, 1000);
            wallet.TrySpend(CurrencyIds.GemHard, 300);
            wallet.TrySpend(CurrencyIds.GemHard, 200);
            Assert.AreEqual(500, wallet.Get(CurrencyIds.GemHard));
            Assert.AreEqual(500, wallet.LifetimeSpent(CurrencyIds.GemHard));
            Assert.AreEqual(1000, wallet.LifetimeEarned(CurrencyIds.GemHard));
        }

        [Test]
        public void Wallet_RejectsOverdraft()
        {
            var wallet = new Wallet();
            wallet.Earn(CurrencyIds.Gold, 100);
            Assert.IsFalse(wallet.TrySpend(CurrencyIds.Gold, 101));
            Assert.AreEqual(100, wallet.Get(CurrencyIds.Gold));
        }

        [Test]
        public void Shop_EnforcesDailyLimit()
        {
            var clock = new ManualClock();
            var wallet = new Wallet();
            wallet.Earn(CurrencyIds.GemHard, 10000);
            var product = new ProductDef
            {
                id = "daily_pack", price = 490,
                grants = new List<CurrencyGrant> { new CurrencyGrant { currency = CurrencyIds.Gold, amount = 1000 } },
                limitType = PurchaseLimitType.Daily, limitCount = 1,
            };
            var shop = new ShopSystem(new[] { product }, wallet, clock);

            Assert.IsTrue(shop.TryPurchase("daily_pack", 0));
            Assert.IsFalse(shop.TryPurchase("daily_pack", 0), "같은 날 2회 구매 불가");

            clock.Advance(System.TimeSpan.FromDays(1));
            Assert.IsTrue(shop.TryPurchase("daily_pack", 0), "다음 날은 다시 구매 가능");
            Assert.AreEqual(2000, wallet.Get(CurrencyIds.Gold));
        }

        [Test]
        public void Shop_ProgressGatedProduct()
        {
            var clock = new ManualClock();
            var wallet = new Wallet();
            wallet.Earn(CurrencyIds.GemHard, 10000);
            var product = new ProductDef { id = "stage_pack", price = 990, unlockStageIndex = 50 };
            var shop = new ShopSystem(new[] { product }, wallet, clock);

            Assert.IsFalse(shop.TryPurchase("stage_pack", 49), "해금 전");
            Assert.IsTrue(shop.TryPurchase("stage_pack", 50), "해금 후");
        }

        [Test]
        public void PaybackAttendance_FullRefundAfterCompletion()
        {
            var clock = new ManualClock();
            var wallet = new Wallet();
            wallet.Earn(CurrencyIds.GemHard, 490);
            var def = new PaybackAttendanceDef
            {
                id = "payback7", price = 490, days = 7,
                dailyReward = new List<CurrencyGrant>
                    { new CurrencyGrant { currency = CurrencyIds.GemSoft, amount = 10000 } },
            };
            var system = new PaybackAttendanceSystem(def, wallet, clock);

            Assert.IsTrue(system.TryPurchase());
            Assert.AreEqual(0, wallet.Get(CurrencyIds.GemHard));

            for (int day = 0; day < 7; day++)
            {
                Assert.IsTrue(system.TryClaimToday(), $"day {day + 1} 출석");
                Assert.IsFalse(system.TryClaimToday(), "하루 2회 불가");
                clock.Advance(System.TimeSpan.FromDays(1));
            }

            Assert.AreEqual(490, wallet.Get(CurrencyIds.GemHard), "완주 시 전액 환급");
            Assert.AreEqual(70000, wallet.Get(CurrencyIds.GemSoft));
            Assert.IsTrue(system.State.refunded);
        }

        [Test]
        public void CumulativeSpendPayback_TiersByLifetimeSpend()
        {
            var payback = new CumulativeSpendPayback
            {
                tiers = new List<CumulativeSpendPayback.Tier>
                {
                    new CumulativeSpendPayback.Tier { threshold = 100, reward = 50 },
                    new CumulativeSpendPayback.Tier { threshold = 300, reward = 100 },
                    new CumulativeSpendPayback.Tier { threshold = 1000, reward = 250 },
                },
            };
            var claimed = new HashSet<long>();
            var claimable = payback.ClaimableTiers(350, claimed);
            Assert.AreEqual(2, claimable.Count);

            claimed.Add(100);
            claimed.Add(300);
            Assert.AreEqual(0, payback.ClaimableTiers(350, claimed).Count);
        }

        [Test]
        public void Pass_PaidTrackRequiresUnlock()
        {
            var clock = new ManualClock();
            var wallet = new Wallet();
            wallet.Earn(CurrencyIds.GemHard, 990);
            var def = new PassDef
            {
                id = "stage_pass", keyType = PassKeyType.StageIndex, unlockPrice = 990,
                steps = new List<PassRewardStep>
                {
                    new PassRewardStep
                    {
                        atKey = 10,
                        freeRewards = new List<CurrencyGrant> { new CurrencyGrant { currency = CurrencyIds.GemSoft, amount = 100 } },
                        paidRewards = new List<CurrencyGrant> { new CurrencyGrant { currency = CurrencyIds.GemSoft, amount = 500 } },
                    },
                },
            };
            var pass = new PassSystem(def, wallet, clock);

            pass.ClaimAll(15);
            Assert.AreEqual(100, wallet.Get(CurrencyIds.GemSoft), "무료 트랙만");

            Assert.IsTrue(pass.TryUnlockPaid());
            pass.ClaimAll(15);
            Assert.AreEqual(600, wallet.Get(CurrencyIds.GemSoft), "유료 트랙 소급 수령");
        }

        [Test]
        public void FakeStore_GrantsHardCurrency()
        {
            var wallet = new Wallet();
            var store = new FakeStoreAdapter(wallet);
            bool completed = false;
            store.Purchase(new StoreSku { skuId = "gem_5500", grantHardGems = 550, priceKrw = 5500 },
                ok => completed = ok);
            Assert.IsTrue(completed);
            Assert.AreEqual(550, wallet.Get(CurrencyIds.GemHard));
        }
    }
}
