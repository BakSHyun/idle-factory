using System.Collections.Generic;

namespace IdleCore.Economy
{
    public enum PurchaseLimitType
    {
        None,
        Daily,
        Weekly,
        Total,
    }

    public sealed class CurrencyGrant
    {
        public string currency;
        public long amount;
    }

    /// <summary>
    /// 인게임 상점 상품. 가격은 하드커런시 사다리(490/990/1,990/2,990)를 따른다.
    /// 원화 상품(하드커런시 충전)은 IStoreAdapter의 SKU로 별도 정의된다.
    /// </summary>
    public sealed class ProductDef
    {
        public string id;
        public string name;
        public string costCurrency = CurrencyIds.GemHard;
        public long price;
        public List<CurrencyGrant> grants = new List<CurrencyGrant>();
        public PurchaseLimitType limitType = PurchaseLimitType.None;
        public int limitCount;
        /// <summary>해금 스테이지 인덱스 (진행도 트리거형 패키지: "시련의 탑 58단계 달성 패키지" 패턴)</summary>
        public int unlockStageIndex;
        /// <summary>누적 구매 이벤트 카운트 대상 여부 (하드커런시 상품만 true — 소울헌터 패턴)</summary>
        public bool countsForPurchaseEvents = true;
    }
}
