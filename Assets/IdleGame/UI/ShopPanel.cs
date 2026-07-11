using IdleCore;
using IdleCore.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 상점 탭 — 구독(기반 캐시플로우) → 페이백 출석 → 패키지 → 수정 충전(SKU) 순서로 배치.
    /// BM 기획서의 결제 퍼널 순서를 UI 배치가 그대로 따른다.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private FakeStoreAdapter _fakeStore;
        private Text _status;
        private RectTransform _list;

        public static ShopPanel Create(Transform root, GameSession session)
        {
            var rect = UIFactory.CreatePanel(root, "ShopPanel", UIFactory.Bg);
            UIFactory.TopBand(rect, 590, 1190);
            var panel = rect.gameObject.AddComponent<ShopPanel>();
            panel.Rect = rect;
            panel._session = session;
            panel._fakeStore = new FakeStoreAdapter(session.Wallet);
            panel.Build();
            return panel;
        }

        private void Build()
        {
            // 스크롤 영역
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            scrollGo.transform.SetParent(Rect, false);
            var scrollRect = (RectTransform)scrollGo.transform;
            UIFactory.Fill(scrollRect);
            scrollGo.GetComponent<Image>().color = UIFactory.Bg;

            _list = UIFactory.CreatePanel(scrollGo.transform, "Content", UIFactory.Bg);
            _list.anchorMin = new Vector2(0, 1);
            _list.anchorMax = new Vector2(1, 1);
            _list.pivot = new Vector2(0.5f, 1);
            UIFactory.AddVerticalList(_list);
            var fitter = _list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = _list;
            scroll.horizontal = false;

            Header("월 정액 (기반)");
            foreach (var sub in _session.Subscriptions.Defs.Values)
                Row($"{sub.name} — 수정 {sub.price} / {sub.durationDays}일",
                    () => { _session.Subscriptions.TryPurchase(sub.id); Refresh(); });

            if (_session.PaybackAttendance != null)
            {
                Header("첫 결제");
                var payback = _session.PaybackAttendance;
                Row($"{payback.Def.name} — 수정 {payback.Def.price} (완주 시 전액 환급)",
                    () => { payback.TryPurchase(); Refresh(); });
                Row("오늘 출석 보상 받기",
                    () => { payback.TryClaimToday(); Refresh(); });
            }

            Header("패키지");
            foreach (var product in _session.Shop.Products.Values)
                Row($"{product.name} — 수정 {product.price}",
                    () => { _session.Shop.TryPurchase(product.id, _session.Progression.HighestClearedIndex); Refresh(); });

            Header("수정 충전 (결제 시뮬레이션)");
            foreach (var sku in _session.Config.skus)
                Row($"{sku.displayName} — ₩{sku.priceKrw:N0}",
                    () => { _fakeStore.Purchase(sku, null); Refresh(); });

            _status = UIFactory.CreateText(_list, "Status", "", 26, TextAnchor.UpperLeft, UIFactory.TextDim);
            _status.gameObject.AddComponent<LayoutElement>().preferredHeight = 300;
        }

        private void Header(string title)
        {
            var text = UIFactory.CreateText(_list, $"Header_{title}", $"── {title} ──", 30,
                TextAnchor.MiddleLeft, UIFactory.Gold);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
        }

        private void Row(string label, UnityEngine.Events.UnityAction onClick)
        {
            var button = UIFactory.CreateButton(_list, $"Row_{label}", label, onClick, UIFactory.Panel, 27);
            button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            var labelRect = button.GetComponentInChildren<Text>().rectTransform;
            labelRect.offsetMin = new Vector2(24, 0);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 92;
        }

        public void Refresh()
        {
            if (_session == null || _status == null) return;
            var subs = _session.Subscriptions;
            string Line(string id, string name)
            {
                var expires = subs.ExpiresAt(id);
                return subs.IsActive(id)
                    ? $"{name}: 활성 (~{expires:MM/dd})"
                    : $"{name}: 미가입";
            }
            var payback = _session.PaybackAttendance;
            string paybackLine = payback == null ? "" :
                payback.IsActive
                    ? $"환생 출석부: {payback.State.claimedDays}/{payback.Def.days}일차"
                    : payback.State?.refunded == true ? "환생 출석부: 완주 (환급 완료)" : "환생 출석부: 미구매";

            _status.text = "── 내 구독 현황 ──\n"
                + Line("pension", "윤회 연금") + "\n"
                + Line("membership", "명부 계약") + "\n"
                + Line("ad_pass", "저승 프리패스") + "\n"
                + paybackLine;
        }
    }
}
