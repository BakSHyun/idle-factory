using System.Collections.Generic;
using System.Linq;
using IdleCore;
using IdleCore.Gacha;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 소환 탭 (소헌키 스타일):
    /// 배너 선택 → 소환 레벨/확률 → 소환 버튼 → [일괄 승급][자동 장착] →
    /// 전체 카탈로그 그리드 (보유 = 등급색 활성 / 미보유 = 실루엣 비활성, 승급 게이지 n/5)
    /// </summary>
    public sealed class GachaPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private Text _pityText, _resultText, _collectionText;
        private Button _pull1Button, _pull10Button, _adPullButton;
        private RectTransform _catalogGrid;
        private readonly List<Button> _bannerButtons = new List<Button>();
        private string _bannerId;

        public static GachaPanel Create(Transform root, GameSession session)
        {
            var rect = UIFactory.CreatePanel(root, "GachaPanel", UIFactory.Bg);
            UIFactory.Stretch(rect, 590, 150);
            var panel = rect.gameObject.AddComponent<GachaPanel>();
            panel.Rect = rect;
            panel._session = session;
            panel.Build();
            return panel;
        }

        private string CurrentKind => _bannerId.Replace("banner_", "");

        private void Build()
        {
            _bannerId = _session.Gacha.Banners.Keys.First();
            var list = UIFactory.CreateScrollList(Rect, spacing: 14);

            // 배너 선택 바
            var bannerBar = UIFactory.CreatePanel(list, "BannerBar", UIFactory.Bg);
            bannerBar.gameObject.AddComponent<LayoutElement>().preferredHeight = 88;
            var barLayout = bannerBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = true;
            barLayout.spacing = 8;
            foreach (var banner in _session.Gacha.Banners.Values)
            {
                string id = banner.id;
                var button = UIFactory.CreateButton(bannerBar, $"Sel_{id}", banner.name.Replace(" 소환", ""),
                    () => { _bannerId = id; Refresh(); }, UIFactory.Panel, 27);
                _bannerButtons.Add(button);
            }

            _pityText = UIFactory.CreateText(list, "Pity", "", 25, TextAnchor.MiddleCenter, UIFactory.TextDim);
            _pityText.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;

            _pull1Button = UIFactory.CreateButton(list, "Pull1", "", () => Pull(1));
            _pull1Button.gameObject.AddComponent<LayoutElement>().preferredHeight = 96;

            _pull10Button = UIFactory.CreateButton(list, "Pull10", "", () => Pull(10), UIFactory.Gold);
            _pull10Button.GetComponentInChildren<Text>().color = Color.black;
            _pull10Button.gameObject.AddComponent<LayoutElement>().preferredHeight = 96;

            // 광고 무료 소환 (소헌키의 보라색 광고 버튼 — IAA 슬롯, 프리패스 구독자는 즉시)
            _adPullButton = UIFactory.CreateButton(list, "AdPull", "", () =>
            {
                if (!_session.Ads.CanUse("free_summon")) { _resultText.text = "오늘의 무료 소환을 다 썼습니다"; return; }
                _session.Ads.Use("free_summon", ok =>
                {
                    if (!ok) return;
                    if (_session.Gacha.TryPullFree(_bannerId, 1, out var result))
                    {
                        var def = _session.Units.Defs[result.UnitIds[0]];
                        _resultText.text = $"📺 무료 소환: [{GradeLabel(def.grade)}] {def.name}";
                    }
                    Refresh();
                });
            }, new Color(0.55f, 0.3f, 0.75f), 27);
            _adPullButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;

            _resultText = UIFactory.CreateText(list, "Result", "", 26, TextAnchor.UpperCenter);
            _resultText.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;

            // 일괄 승급 / 자동 장착 (소헌키의 일괄 합성 / 자동 장착)
            var actionBar = UIFactory.CreatePanel(list, "Actions", UIFactory.Bg);
            actionBar.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
            var actionLayout = actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.childControlWidth = true;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = true;
            actionLayout.spacing = 10;
            UIFactory.CreateButton(actionBar, "ComposeAll", "⚗ 일괄 승급", () =>
            {
                int composed = 0;
                for (int guard = 0; guard < 500; guard++)
                {
                    var target = _session.Units.AllOwned().FirstOrDefault(u => _session.Units.CanCompose(u.unitId));
                    if (target == null || !_session.Units.TryComposeUnit(target.unitId, out _)) break;
                    composed++;
                }
                _resultText.text = composed > 0 ? $"일괄 승급 완료 — {composed}회 합성!" : "승급 가능한 잉여 사본이 없습니다 (같은 유닛 잉여 5개 필요)";
                Refresh();
            }, new Color(0.32f, 0.24f, 0.15f), 27);
            UIFactory.CreateButton(actionBar, "AutoEquip", "✦ 자동 장착", () =>
            {
                _session.Units.AutoEquipBest();
                _resultText.text = "자동 장착 완료 (등급·돌파 순)";
                Refresh();
            }, new Color(0.2f, 0.3f, 0.5f), 27);

            _collectionText = UIFactory.CreateText(list, "Collection", "", 26, TextAnchor.MiddleLeft, UIFactory.Gold);
            _collectionText.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            // 전체 카탈로그 그리드 (4열)
            var gridPanel = UIFactory.CreatePanel(list, "Catalog", UIFactory.Bg);
            _catalogGrid = gridPanel;
            var grid = gridPanel.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.cellSize = new Vector2(238, 300);
            grid.spacing = new Vector2(12, 12);
            gridPanel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void Pull(int count)
        {
            if (!_session.Gacha.TryPull(_bannerId, count, out var result))
            {
                _resultText.text = "영옥이 부족합니다";
                return;
            }
            var lines = result.UnitIds
                .GroupBy(id => id)
                .OrderByDescending(g => _session.Units.Defs[g.Key].grade)
                .Take(5)
                .Select(g =>
                {
                    var def = _session.Units.Defs[g.Key];
                    return $"[{GradeLabel(def.grade)}] {def.name} ×{g.Count()}";
                });
            _resultText.text = string.Join("\n", lines);
            Refresh();
        }

        public void Refresh()
        {
            if (_session == null || _pityText == null) return;
            var banner = _session.Gacha.Banners[_bannerId];

            int i = 0;
            foreach (var b in _session.Gacha.Banners.Values)
                _bannerButtons[i++].image.color = b.id == _bannerId ? UIFactory.Accent : UIFactory.Panel;

            int summonLevel = _session.Gacha.SummonLevel(_bannerId);
            long toNext = _session.Gacha.PullsToNextLevel(_bannerId);
            var rates = _session.Gacha.CurrentRates(_bannerId)
                .Where(kv => kv.Value > 0)
                .OrderByDescending(kv => kv.Key)
                .Select(kv => $"{GradeLabel(kv.Key)} {kv.Value * 100:0.###}%");
            _pityText.text = $"소환 Lv.{summonLevel}" +
                (toNext > 0 ? $" — 다음 레벨까지 {toNext}회" : " (최고)") +
                $"\n{string.Join(" · ", rates)}";
            _pull1Button.GetComponentInChildren<Text>().text = $"1회 소환  (영옥 {banner.costPerPull})";
            _pull10Button.GetComponentInChildren<Text>().text = $"10연 소환  (영옥 {banner.CostFor(10)})";
            int adLeft = _session.Ads.RemainingToday("free_summon");
            _adPullButton.GetComponentInChildren<Text>().text =
                _session.Subscriptions.HasAdSkip()
                    ? $"✨ 무료 소환 1회 (프리패스, 남은 {adLeft}회)"
                    : $"📺 광고 보고 무료 소환 (남은 {adLeft}회)";
            _adPullButton.interactable = adLeft > 0;

            int owned = _session.Units.UniqueOwnedCount;
            int total = _session.Units.Defs.Count;
            var next = _session.Units.Milestones.FirstOrDefault(m => owned < m.count);
            _collectionText.text = next == null
                ? $"📖 도감 {owned}/{total} — 완성!"
                : $"📖 도감 {owned}/{total} — 다음 보너스까지 {next.count - owned}종";

            RebuildCatalog();
        }

        /// <summary>전체 카탈로그: 보유 = 등급색 활성, 미보유 = 실루엣 비활성 (소헌키 스타일).</summary>
        private void RebuildCatalog()
        {
            foreach (Transform child in _catalogGrid) Destroy(child.gameObject);

            var defs = _session.Units.Defs.Values
                .Where(d => d.kind == CurrentKind)
                .OrderBy(d => d.grade).ThenBy(d => d.subTier).ThenBy(d => d.id);

            foreach (var def in defs)
            {
                var unit = _session.Units.Get(def.id);
                bool isOwned = unit != null;
                var gradeColor = UIFactory.GradeColor(def.grade);

                var tile = UIFactory.CreateButton(_catalogGrid, $"T_{def.id}", "", () =>
                {
                    UnitDetailView.Open(Rect.parent, _session, def.id, Refresh); // 상세 팝업 (장착/강화/승급)
                }, isOwned
                    ? Color.Lerp(UIFactory.Panel, gradeColor, unit.equipped ? 0.45f : 0.18f)
                    : new Color(0.10f, 0.09f, 0.14f), 22);
                Destroy(tile.GetComponentInChildren<Text>().gameObject); // 기본 라벨 제거, 커스텀 구성

                // 등급색 테두리 (보유 시)
                if (isOwned)
                {
                    var outline = tile.gameObject.AddComponent<Outline>();
                    outline.effectColor = gradeColor;
                    outline.effectDistance = new Vector2(3, 3);
                }

                // 아이콘 (미보유 = 검은 실루엣)
                var icon = UIFactory.LoadSprite($"art/units/{def.artId ?? def.id}.png");
                if (icon != null)
                {
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(tile.transform, false);
                    var iconImage = iconGo.GetComponent<Image>();
                    iconImage.sprite = icon;
                    iconImage.preserveAspect = true;
                    iconImage.color = isOwned ? Color.white : new Color(0.06f, 0.05f, 0.09f);
                    iconImage.raycastTarget = false;
                    var iconRect = (RectTransform)iconGo.transform;
                    iconRect.anchorMin = new Vector2(0.5f, 1);
                    iconRect.anchorMax = new Vector2(0.5f, 1);
                    iconRect.pivot = new Vector2(0.5f, 1);
                    iconRect.anchoredPosition = new Vector2(0, -12);
                    iconRect.sizeDelta = new Vector2(150, 150);
                }

                // 이름
                var nameText = UIFactory.CreateText(tile.transform, "Name", def.name, 23,
                    TextAnchor.MiddleCenter, isOwned ? gradeColor : UIFactory.TextDim);
                nameText.raycastTarget = false;
                UIFactory.BottomBand(nameText.rectTransform, 84, 40, 6);

                // 상태: 장착/레벨/돌파 + 승급 게이지 n/5
                string status;
                if (!isOwned) status = "미보유";
                else
                {
                    var parts = new List<string>();
                    if (unit.equipped) parts.Add("★장착");
                    if (def.maxLevel > 1) parts.Add($"Lv.{unit.level}");
                    parts.Add($"{unit.limitBreak}돌");
                    status = string.Join(" ", parts);
                }
                var statusText = UIFactory.CreateText(tile.transform, "Status", status, 21,
                    TextAnchor.MiddleCenter, isOwned ? UIFactory.TextMain : UIFactory.TextDim);
                statusText.raycastTarget = false;
                UIFactory.BottomBand(statusText.rectTransform, 46, 36, 6);

                // 승급 게이지 (잉여 n/5, 소헌키의 N/4 게이지)
                if (isOwned && def.upgradeToId != null)
                {
                    int surplus = _session.Units.SurplusCopies(def.id);
                    var gaugeText = UIFactory.CreateText(tile.transform, "Gauge",
                        surplus >= _session.Units.ComposeCost
                            ? $"⚗ 승급 가능 {surplus}/{_session.Units.ComposeCost}"
                            : $"승급 {surplus}/{_session.Units.ComposeCost}",
                        20, TextAnchor.MiddleCenter,
                        surplus >= _session.Units.ComposeCost ? UIFactory.Gold : UIFactory.TextDim);
                    gaugeText.raycastTarget = false;
                    UIFactory.BottomBand(gaugeText.rectTransform, 10, 34, 6);
                }
            }
        }

        public static string GradeLabel(UnitGrade grade) => grade switch
        {
            UnitGrade.Eternal => "영원",
            UnitGrade.Ancient => "고대",
            UnitGrade.Mythic => "신화",
            UnitGrade.Epic => "전설",
            UnitGrade.Rare => "특급",
            UnitGrade.Advanced => "상급",
            UnitGrade.Intermediate => "중급",
            _ => "초급",
        };

        public static string KindLabel(string kind) => kind switch
        {
            "weapon" => "낫",
            "orb" => "오브",
            "ornament" => "장식",
            "skill" => "스킬",
            "hero" => "차사",
            _ => kind,
        };
    }
}
