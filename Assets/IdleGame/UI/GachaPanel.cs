using System.Collections.Generic;
using System.Linq;
using IdleCore;
using IdleCore.Gacha;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 소환 탭 — 배너 4종(낫/장신구/스킬/차사) 선택, 천장, 소환, 도감 진행,
    /// 보유 목록(행 탭 = 장착/해제 토글, 슬롯 제한 적용).
    /// </summary>
    public sealed class GachaPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private Text _pityText, _resultText, _collectionText;
        private Button _pull1Button, _pull10Button;
        private RectTransform _ownedList;
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

        private void Build()
        {
            _bannerId = _session.Gacha.Banners.Keys.First();
            var list = UIFactory.CreateScrollList(Rect, spacing: 16);

            // 배너 선택 바
            var bannerBar = UIFactory.CreatePanel(list, "BannerBar", UIFactory.Bg);
            bannerBar.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
            var barLayout = bannerBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = true;
            barLayout.spacing = 8;
            foreach (var banner in _session.Gacha.Banners.Values)
            {
                string id = banner.id;
                var button = UIFactory.CreateButton(bannerBar, $"Sel_{id}", banner.name.Replace(" 소환", ""),
                    () => { _bannerId = id; Refresh(); }, UIFactory.Panel, 28);
                _bannerButtons.Add(button);
            }

            _pityText = UIFactory.CreateText(list, "Pity", "", 26, TextAnchor.MiddleCenter, UIFactory.TextDim);
            _pityText.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;

            _pull1Button = UIFactory.CreateButton(list, "Pull1", "", () => Pull(1));
            _pull1Button.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

            _pull10Button = UIFactory.CreateButton(list, "Pull10", "", () => Pull(10), UIFactory.Gold);
            _pull10Button.GetComponentInChildren<Text>().color = Color.black;
            _pull10Button.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

            _resultText = UIFactory.CreateText(list, "Result", "", 27, TextAnchor.UpperCenter);
            _resultText.gameObject.AddComponent<LayoutElement>().preferredHeight = 220;

            _collectionText = UIFactory.CreateText(list, "Collection", "", 27, TextAnchor.MiddleLeft, UIFactory.Gold);
            _collectionText.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;

            var ownedHeader = UIFactory.CreateText(list, "OwnedHeader",
                "── 명부 (탭하여 장착/해제) ──", 28, TextAnchor.MiddleLeft, UIFactory.TextDim);
            ownedHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;

            _ownedList = UIFactory.CreatePanel(list, "OwnedList", UIFactory.Bg);
            UIFactory.AddVerticalList(_ownedList, spacing: 8, padding: 0);
            _ownedList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void Pull(int count)
        {
            if (!_session.Gacha.TryPull(_bannerId, count, out var result))
            {
                _resultText.text = "영옥이 부족합니다";
                return;
            }
            _session.Units.AutoEquipBest(); // 뉴비 편의: 슬롯 내 최적 자동 장착

            var lines = result.UnitIds
                .GroupBy(id => id)
                .OrderByDescending(g => _session.Units.Defs[g.Key].grade)
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
                (toNext > 0 ? $" — 다음 레벨까지 {toNext}회 소환" : " (최고)") +
                $"\n{string.Join(" · ", rates)}";
            _pull1Button.GetComponentInChildren<Text>().text = $"1회 소환  (영옥 {banner.costPerPull})";
            _pull10Button.GetComponentInChildren<Text>().text = $"10연 소환  (영옥 {banner.CostFor(10)})";

            // 도감 진행
            int owned = _session.Units.UniqueOwnedCount;
            int total = _session.Units.Defs.Count;
            var next = _session.Units.Milestones.FirstOrDefault(m => owned < m.count);
            _collectionText.text = next == null
                ? $"📖 도감 {owned}/{total} — 완성!"
                : $"📖 도감 {owned}/{total} — 다음 보너스까지 {next.count - owned}종";

            RebuildOwnedList();
        }

        private void RebuildOwnedList()
        {
            foreach (Transform child in _ownedList) Destroy(child.gameObject);

            var groups = _session.Units.AllOwned()
                .GroupBy(u => _session.Units.Defs[u.unitId].kind)
                .OrderBy(g => g.Key);
            foreach (var group in groups)
            {
                string kind = group.Key;
                var header = UIFactory.CreateText(_ownedList, $"Kind_{kind}",
                    $"{KindLabel(kind)}  ({_session.Units.EquippedCount(kind)}/{_session.Units.SlotLimit(kind)} 장착)",
                    26, TextAnchor.MiddleLeft, UIFactory.TextDim);
                header.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;

                // 합성: 잉여(10돌 초과) 사본 5개 → 상위 등급 1개. 잉여가 있는 최저 등급부터
                foreach (UnitGrade grade in System.Enum.GetValues(typeof(UnitGrade)))
                {
                    int surplus = _session.Units.TotalSurplus(kind, grade);
                    if (surplus < _session.Units.ComposeCost) continue;
                    if (!_session.Units.Defs.Values.Any(d => d.kind == kind && d.grade == grade + 1)) continue;
                    UnitGrade g = grade;
                    var composeButton = UIFactory.CreateButton(_ownedList, $"Compose_{kind}_{grade}",
                        $"⚗ 합성: {GradeLabel(grade)} {_session.Units.ComposeCost}개 → {GradeLabel(grade + 1)} 1개  (잉여 {surplus})",
                        () =>
                        {
                            if (_session.Units.TryCompose(kind, g, new SeededRng(System.Environment.TickCount), out var newUnit))
                                _resultText.text = $"합성 성공! [{GradeLabel(_session.Units.Defs[newUnit].grade)}] {_session.Units.Defs[newUnit].name} 획득";
                            Refresh();
                        }, new Color(0.32f, 0.24f, 0.15f), 24);
                    composeButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
                    break; // 종류당 하나만 노출 (최저 등급)
                }

                foreach (var unit in group.OrderByDescending(u => _session.Units.Defs[u.unitId].grade)
                                          .ThenByDescending(u => u.limitBreak))
                {
                    var def = _session.Units.Defs[unit.unitId];
                    bool levelable = def.maxLevel > 1;
                    string star = unit.equipped ? "★ " : "";
                    string levelTag = levelable ? $" Lv.{unit.level}" : "";
                    string label = $"{star}[{GradeLabel(def.grade)}] {def.name}{levelTag}  {unit.limitBreak}돌";
                    string unitId = unit.unitId;
                    var row = UIFactory.CreateButton(_ownedList, $"U_{unitId}", label, () =>
                    {
                        var u = _session.Units.Get(unitId);
                        if (u.equipped) _session.Units.Unequip(unitId);
                        else if (!_session.Units.TryEquip(unitId))
                            _resultText.text = $"{KindLabel(def.kind)} 슬롯이 가득 찼습니다 — 먼저 해제하세요";
                        Refresh();
                    }, unit.equipped ? new Color(0.25f, 0.20f, 0.42f) : UIFactory.Panel, 26);
                    var rowLabel = row.GetComponentInChildren<Text>();
                    rowLabel.alignment = TextAnchor.MiddleLeft;
                    rowLabel.color = UIFactory.GradeColor(def.grade); // 등급 컬러 토큰
                    row.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;

                    // 유닛 아이콘 (아트 파이프라인 산출물, 없으면 라벨만)
                    var icon = UIFactory.LoadSprite($"art/units/{unitId}.png");
                    if (icon != null)
                    {
                        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                        iconGo.transform.SetParent(row.transform, false);
                        var iconImage = iconGo.GetComponent<Image>();
                        iconImage.sprite = icon;
                        iconImage.preserveAspect = true;
                        var iconRect = (RectTransform)iconGo.transform;
                        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, 0.5f);
                        iconRect.pivot = new Vector2(0, 0.5f);
                        iconRect.anchoredPosition = new Vector2(10, 0);
                        iconRect.sizeDelta = new Vector2(62, 62);
                        rowLabel.rectTransform.offsetMin = new Vector2(84, 0);
                    }
                    else
                    {
                        rowLabel.rectTransform.offsetMin = new Vector2(20, 0);
                    }

                    // 장비: 우측에 강화 버튼 (골드, 등급이 높을수록 비쌈, 최대 200)
                    if (levelable)
                    {
                        long cost = _session.Units.LevelUpCost(unitId);
                        string costLabel = cost < 0 ? "MAX" : $"강화 {UIFactory.FormatNumber(cost)}";
                        var upButton = UIFactory.CreateButton(row.transform, "LevelUp", costLabel, () =>
                        {
                            if (_session.Units.TryLevelUp(unitId, _session.Wallet)) Refresh();
                        }, UIFactory.Accent, 22);
                        var upRect = (RectTransform)upButton.transform;
                        upRect.anchorMin = new Vector2(1, 0.5f);
                        upRect.anchorMax = new Vector2(1, 0.5f);
                        upRect.pivot = new Vector2(1, 0.5f);
                        upRect.anchoredPosition = new Vector2(-10, 0);
                        upRect.sizeDelta = new Vector2(220, 60);
                        upButton.interactable = cost >= 0 &&
                            _session.Wallet.CanAfford(def.levelCostCurrency, cost);
                    }
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
