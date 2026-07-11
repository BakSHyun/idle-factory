using System;
using System.Collections.Generic;

namespace IdleCore.Economy
{
    /// <summary>원화 결제 SKU — 스토어(구글/애플)에 등록되는 상품. 하드커런시 충전이 주 용도.</summary>
    public sealed class StoreSku
    {
        public string skuId;          // 예: "gem_5500"
        public string displayName;
        public long grantHardGems;
        /// <summary>참고용 원화 가격 (실제 가격은 스토어 콘솔이 진실)</summary>
        public int priceKrw;
    }

    /// <summary>
    /// 실결제(IAP) 추상화. 실제 구현은 Unity IAP 어댑터, 개발/시뮬레이션은 FakeStoreAdapter.
    /// </summary>
    public interface IStoreAdapter
    {
        void Purchase(StoreSku sku, Action<bool> onComplete);
    }

    /// <summary>보상형 광고 추상화. 실제 구현은 AppLovin MAX 어댑터.</summary>
    public interface IAdAdapter
    {
        bool IsRewardedAdReady { get; }
        void ShowRewardedAd(Action<bool> onRewarded);
    }

    /// <summary>개발/테스트용: 항상 성공하는 가짜 스토어. 결제 이력을 기록한다.</summary>
    public sealed class FakeStoreAdapter : IStoreAdapter
    {
        private readonly Wallet _wallet;
        public readonly List<StoreSku> PurchaseLog = new List<StoreSku>();

        public FakeStoreAdapter(Wallet wallet) => _wallet = wallet;

        public void Purchase(StoreSku sku, Action<bool> onComplete)
        {
            _wallet.Earn(CurrencyIds.GemHard, sku.grantHardGems);
            PurchaseLog.Add(sku);
            onComplete?.Invoke(true);
        }
    }

    /// <summary>개발/테스트용: 항상 준비된 가짜 광고.</summary>
    public sealed class FakeAdAdapter : IAdAdapter
    {
        public int ShownCount { get; private set; }
        public bool IsRewardedAdReady => true;

        public void ShowRewardedAd(Action<bool> onRewarded)
        {
            ShownCount++;
            onRewarded?.Invoke(true);
        }
    }
}
