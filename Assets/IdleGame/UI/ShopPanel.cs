using System.Collections.Generic;
using System.Linq;
using IdleCore;
using IdleCore.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 상점 탭 — 카드형 (BM 퍼널 순서 유지):
    /// 구독 카드(상태 점 + 혜택 요약) → 페이백 출석 카드 → 패키지 2열 그리드 →
    /// 마일리지 상점 → 수정 충전 3열 그리드.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private FakeStoreAdapter _fakeStore;
        private Text _toast, _mileageText;
        private RectTransform _list, _pickerContainer;
        private string _pickerProductId;
        private readonly List<System.Action> _refreshers = new List<System.Action>();

        private static readonly Dictionary<string, string> SubDesc = new Dictionary<string, string>
        {
            { "pension", "매일 수정 100 지급 — 30일 완주 시 2배 회수" },
            { "membership", "방치 상한 +4시간 · 보상 +50% · 매일 소탕권 2장" },
            { "ad_pass", "모든 광고 보상을 광고 없이 즉시 수령" },
        };

        public static ShopPanel Create(Transform root, GameSession session)
        {
            var rect = UIFactory.CreatePanel(root, "ShopPanel", UIFactory.Bg);
            UIFactory.Stretch(rect, 590, 150);
            var panel = rect.gameObject.AddComponent<ShopPanel>();
            panel.Rect = rect;
            panel._session = session;
            panel._fakeStore = new FakeStoreAdapter(session.Wallet);
            panel.Build();
            return panel;
        }

        private void Build()
        {
            _list = UIFactory.CreateScrollList(Rect, spacing: 14);

            _toast = UIFactory.CreateText(_list, "Toast", "", 26, TextAnchor.MiddleCenter, UIFactory.Gold);
            _toast.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;

            Header("월 정액");
            foreach (var sub in _session.Subscriptions.Defs.Values)
                BuildSubscriptionCard(sub);

            if (_session.PaybackAttendance != null)
                BuildPaybackCard();

            Header("패키지");
            BuildProductGrid(_session.Shop.Products.Values
                .Where(p => p.costCurrency == CurrencyIds.GemHard).ToList());

            Header("마일리지 상점");
            _mileageText = UIFactory.CreateText(_list, "Mileage", "", 26, TextAnchor.MiddleCenter, UIFactory.Gold);
            _mileageText.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            foreach (var product in _session.Shop.Products.Values.Where(p => p.costCurrency == CurrencyIds.Mileage))
                BuildMileageRow(product);
            _pickerContainer = UIFactory.CreatePanel(_list, "Picker", UIFactory.Bg);
            UIFactory.AddVerticalList(_pickerContainer, spacing: 6, padding: 0);
            _pickerContainer.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Header("수정 충전");
            BuildSkuGrid();
        }

        private void Header(string title)
        {
            var text = UIFactory.CreateText(_list, $"H_{title}", title, 30, TextAnchor.MiddleCenter, UIFactory.Gold);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
        }

        // ── 구독 카드: 이름 + 혜택 요약 + 상태 점 + 우측 구독 버튼 ──
        private void BuildSubscriptionCard(SubscriptionDef sub)
        {
            var card = UIFactory.CreatePanel(_list, $"Sub_{sub.id}", UIFactory.Panel);
            UIFactory.Roundify(card.GetComponent<Image>());
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 158;

            var name = UIFactory.CreateText(card, "N", sub.name, 30, TextAnchor.UpperLeft);
            UIFactory.TopBand(name.rectTransform, 20, 42, 28);

            var desc = UIFactory.CreateText(card, "D",
                SubDesc.TryGetValue(sub.id, out var d) ? d : "", 22, TextAnchor.UpperLeft, UIFactory.TextDim);
            UIFactory.TopBand(desc.rectTransform, 64, 36, 28);
            desc.rectTransform.offsetMax = new Vector2(-260, desc.rectTransform.offsetMax.y);

            var status = UIFactory.CreateText(card, "S", "", 23, TextAnchor.LowerLeft, UIFactory.TextDim);
            UIFactory.BottomBand(status.rectTransform, 16, 34, 28);

            var buy = UIFactory.CreateButton(card, "Buy", $"구독\n수정 {sub.price}", () =>
            {
                _toast.text = _session.Subscriptions.TryPurchase(sub.id)
                    ? $"{sub.name} 구독 시작!" : "수정이 부족합니다";
                Refresh();
            }, UIFactory.Accent, 24);
            var buyRect = (RectTransform)buy.transform;
            buyRect.anchorMin = buyRect.anchorMax = new Vector2(1, 0.5f);
            buyRect.pivot = new Vector2(1, 0.5f);
            buyRect.anchoredPosition = new Vector2(-20, 0);
            buyRect.sizeDelta = new Vector2(220, 110);

            _refreshers.Add(() =>
            {
                bool active = _session.Subscriptions.IsActive(sub.id);
                status.text = active
                    ? $"<color=#6fdd8b>● 활성</color> ~{_session.Subscriptions.ExpiresAt(sub.id):MM/dd}"
                    : "○ 미가입";
                buy.GetComponentInChildren<Text>().text = active ? $"연장\n수정 {sub.price}" : $"구독\n수정 {sub.price}";
            });
        }

        // ── 페이백 출석 카드 ──
        private void BuildPaybackCard()
        {
            var payback = _session.PaybackAttendance;
            var card = UIFactory.CreatePanel(_list, "Payback", new Color(0.24f, 0.18f, 0.32f));
            UIFactory.Roundify(card.GetComponent<Image>());
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 158;

            var name = UIFactory.CreateText(card, "N", $"💜 {payback.Def.name}", 30, TextAnchor.UpperLeft);
            UIFactory.TopBand(name.rectTransform, 20, 42, 28);
            var desc = UIFactory.CreateText(card, "D",
                $"수정 {payback.Def.price} 구매 → {payback.Def.days}일 출석 완주 시 전액 환급!", 22,
                TextAnchor.UpperLeft, UIFactory.TextDim);
            UIFactory.TopBand(desc.rectTransform, 64, 36, 28);
            var status = UIFactory.CreateText(card, "S", "", 23, TextAnchor.LowerLeft, UIFactory.Gold);
            UIFactory.BottomBand(status.rectTransform, 16, 34, 28);

            var action = UIFactory.CreateButton(card, "Act", "", () =>
            {
                if (!payback.IsActive) { if (payback.TryPurchase()) _toast.text = "환생 출석부 시작!"; else _toast.text = "수정이 부족합니다"; }
                else if (payback.TryClaimToday()) _toast.text = "출석 완료!";
                else _toast.text = "오늘은 이미 출석했습니다";
                Refresh();
            }, UIFactory.Accent, 24);
            var actionRect = (RectTransform)action.transform;
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(1, 0.5f);
            actionRect.pivot = new Vector2(1, 0.5f);
            actionRect.anchoredPosition = new Vector2(-20, 0);
            actionRect.sizeDelta = new Vector2(220, 110);

            _refreshers.Add(() =>
            {
                if (payback.State?.refunded == true)
                {
                    status.text = "✓ 완주 — 전액 환급 완료";
                    action.GetComponentInChildren<Text>().text = "완료";
                    action.interactable = false;
                }
                else if (payback.IsActive)
                {
                    status.text = $"진행 {payback.State.claimedDays}/{payback.Def.days}일차";
                    bool can = payback.CanClaimToday();
                    action.GetComponentInChildren<Text>().text = can ? "오늘 출석!" : "내일 출석";
                    action.interactable = can;
                }
                else
                {
                    status.text = "사실상 무료 — 첫 결제 추천";
                    action.GetComponentInChildren<Text>().text = $"시작\n수정 {payback.Def.price}";
                    action.interactable = true;
                }
            });
        }

        // ── 패키지 그리드 (2열) ──
        private void BuildProductGrid(List<ProductDef> products)
        {
            var grid = UIFactory.CreatePanel(_list, "PkgGrid", UIFactory.Bg);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.cellSize = new Vector2(496, 168);
            layout.spacing = new Vector2(14, 14);
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var product in products)
            {
                var p = product;
                var card = UIFactory.CreatePanel(grid, $"P_{p.id}", UIFactory.Panel);
                UIFactory.Roundify(card.GetComponent<Image>());

                var name = UIFactory.CreateText(card, "N", p.name, 27, TextAnchor.UpperLeft);
                UIFactory.TopBand(name.rectTransform, 14, 38, 20);
                string grants = string.Join(" · ", p.grants.Select(g =>
                    $"{GrowthPanel.CurrencyLabel(g.currency)} {UIFactory.FormatNumber(g.amount)}"));
                var desc = UIFactory.CreateText(card, "D", grants, 21, TextAnchor.UpperLeft, UIFactory.TextDim);
                UIFactory.TopBand(desc.rectTransform, 54, 34, 20);

                var buy = UIFactory.CreateButton(card, "B", $"수정 {p.price}", () =>
                {
                    _toast.text = _session.Shop.TryPurchase(p.id, _session.Progression.HighestClearedIndex)
                        ? $"{p.name} 구매 완료!" : "구매 불가 (재화/한정/해금 확인)";
                    Refresh();
                }, UIFactory.Accent, 24);
                UIFactory.BottomBand((RectTransform)buy.transform, 12, 62, 20);

                _refreshers.Add(() =>
                    buy.interactable = _session.Shop.IsPurchasable(p.id, _session.Progression.HighestClearedIndex)
                                       && _session.Wallet.CanAfford(p.costCurrency, p.price));
            }
        }

        // ── 마일리지 행 (선택권은 피커 펼침) ──
        private void BuildMileageRow(ProductDef product)
        {
            var p = product;
            var row = UIFactory.CreateButton(_list, $"M_{p.id}", $"{p.name}  —  마일리지 {p.price}", () =>
            {
                if (p.unitChoice != null) TogglePicker(p.id);
                else
                {
                    _toast.text = _session.Shop.TryPurchase(p.id, _session.Progression.HighestClearedIndex)
                        ? $"{p.name} 구매!" : "마일리지 부족 또는 주간 한도";
                    Refresh();
                }
            }, UIFactory.Panel, 26);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
        }

        private void TogglePicker(string productId)
        {
            foreach (Transform child in _pickerContainer) Destroy(child.gameObject);
            if (_pickerProductId == productId) { _pickerProductId = null; return; }
            _pickerProductId = productId;

            foreach (var candidate in _session.Shop.ChoiceCandidates(productId))
            {
                string unitId = candidate.id;
                var button = UIFactory.CreateButton(_pickerContainer, $"Pick_{unitId}",
                    $"[{GachaPanel.KindLabel(candidate.kind)}] {candidate.name} 받기",
                    () =>
                    {
                        bool ok = _session.Shop.TryPurchaseUnitChoice(
                            productId, _session.Progression.HighestClearedIndex, unitId);
                        _toast.text = ok ? $"{candidate.name} 획득!" : "마일리지 부족 또는 주간 한도";
                        _pickerProductId = null;
                        foreach (Transform child in _pickerContainer) Destroy(child.gameObject);
                        Refresh();
                    }, Color.Lerp(UIFactory.Panel, UIFactory.GradeColor(candidate.grade), 0.25f), 25);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
            }
        }

        // ── 수정 충전 그리드 (3열) ──
        private void BuildSkuGrid()
        {
            var grid = UIFactory.CreatePanel(_list, "SkuGrid", UIFactory.Bg);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.cellSize = new Vector2(326, 130);
            layout.spacing = new Vector2(12, 12);
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var sku in _session.Config.skus)
            {
                var s = sku;
                var tile = UIFactory.CreateButton(grid, $"K_{s.skuId}",
                    $"💎 {UIFactory.FormatNumber(s.grantHardGems)}\n₩{s.priceKrw:N0}", () =>
                    {
                        _fakeStore.Purchase(s, null);
                        _toast.text = $"수정 {s.grantHardGems} 충전! (결제 시뮬레이션)";
                        AudioManager.Play("coin", 0.7f);
                        Refresh();
                    }, new Color(0.2f, 0.28f, 0.45f), 25);
            }
        }

        public void Refresh()
        {
            if (_session == null || _toast == null) return;
            if (_mileageText != null)
                _mileageText.text = $"보유 마일리지 {_session.Wallet.Get(CurrencyIds.Mileage)}  (소환 1회 = 1 적립)";
            foreach (var refresher in _refreshers) refresher();
        }
    }
}
