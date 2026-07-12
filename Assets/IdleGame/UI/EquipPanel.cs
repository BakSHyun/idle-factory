using System.Linq;
using IdleCore;
using IdleCore.Gacha;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 편성 탭 — 종류(낫/오브/장식/스킬/차사) 필터 + 전체 카탈로그 그리드.
    /// 보유 = 등급색 활성 / 미보유 = 실루엣. 타일 → 상세 팝업(장착/강화/승급).
    /// 모든 변경은 전투력 변화 토스트로 피드백.
    /// </summary>
    public sealed class EquipPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private Text _infoText, _collectionText;
        private RectTransform _catalogGrid;
        private readonly System.Collections.Generic.List<Button> _kindButtons
            = new System.Collections.Generic.List<Button>();
        private static readonly string[] Kinds = { "weapon", "orb", "ornament", "skill", "hero", "costume" };
        private string _kind = "weapon";

        public static EquipPanel Create(Transform root, GameSession session)
        {
            var rect = UIFactory.CreatePanel(root, "EquipPanel", UIFactory.Bg);
            UIFactory.Stretch(rect, 590, 150);
            var panel = rect.gameObject.AddComponent<EquipPanel>();
            panel.Rect = rect;
            panel._session = session;
            panel.Build();
            return panel;
        }

        private void Build()
        {
            var list = UIFactory.CreateScrollList(Rect, spacing: 14);

            // 종류 필터 탭
            var kindBar = UIFactory.CreatePanel(list, "KindBar", UIFactory.Bg);
            kindBar.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
            var barLayout = kindBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = true;
            barLayout.spacing = 8;
            foreach (var kind in Kinds)
            {
                string k = kind;
                var button = UIFactory.CreateButton(kindBar, $"K_{k}", GachaPanel.KindLabel(k),
                    () => { _kind = k; Refresh(); }, UIFactory.Panel, 27);
                _kindButtons.Add(button);
            }

            _infoText = UIFactory.CreateText(list, "Info", "", 25, TextAnchor.MiddleCenter, UIFactory.TextDim);
            _infoText.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;

            // 일괄 승급 / 자동 장착
            var actionBar = UIFactory.CreatePanel(list, "Actions", UIFactory.Bg);
            actionBar.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
            var actionLayout = actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.childControlWidth = true;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = true;
            actionLayout.spacing = 10;
            UIFactory.CreateButton(actionBar, "ComposeAll", " 일괄 승급", () =>
                PowerToast.Wrap(_session, Rect.parent, () =>
                {
                    int composed = 0;
                    for (int guard = 0; guard < 500; guard++)
                    {
                        var target = _session.Units.AllOwned().FirstOrDefault(u => _session.Units.CanCompose(u.unitId));
                        if (target == null || !_session.Units.TryComposeUnit(target.unitId, out _)) break;
                        composed++;
                    }
                    _infoText.text = composed > 0 ? $"일괄 승급 {composed}회 완료!" : "승급 가능한 잉여 사본 없음 (같은 유닛 잉여 5개)";
                    Refresh();
                }), new Color(0.32f, 0.24f, 0.15f), 27);
            UIFactory.CreateButton(actionBar, "AutoEquip", " 자동 장착", () =>
                PowerToast.Wrap(_session, Rect.parent, () =>
                {
                    _session.Units.AutoEquipBest();
                    _infoText.text = "자동 장착 완료";
                    Refresh();
                }), new Color(0.2f, 0.3f, 0.5f), 27);

            _collectionText = UIFactory.CreateText(list, "Coll", "", 26, TextAnchor.MiddleLeft, UIFactory.Gold);
            _collectionText.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;

            var gridPanel = UIFactory.CreatePanel(list, "Catalog", UIFactory.Bg);
            _catalogGrid = gridPanel;
            var grid = gridPanel.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.cellSize = new Vector2(238, 300);
            grid.spacing = new Vector2(12, 12);
            gridPanel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public void Refresh()
        {
            if (_session == null || _catalogGrid == null) return;

            int i = 0;
            foreach (var kind in Kinds)
                _kindButtons[i++].image.color = kind == _kind ? UIFactory.Accent : UIFactory.Panel;

            // 슬롯 현황 (속성 보너스 포함)
            int equipped = _session.Units.EquippedCount(_kind);
            int baseLimit = _session.Units.SlotLimit(_kind);
            int effective = _session.Units.EffectiveSlotLimit(_kind);
            _infoText.text = effective > baseLimit
                ? $"장착 {equipped}/{effective}  (기본 {baseLimit} + 속성 보너스 {effective - baseLimit})"
                : $"장착 {equipped}/{baseLimit}  — 속성을 다양하게 쓰면 슬롯 +1씩 (최대 +3)";

            int owned = _session.Units.UniqueOwnedCount;
            int total = _session.Units.Defs.Count;
            var next = _session.Units.Milestones.FirstOrDefault(m => owned < m.count);
            _collectionText.text = next == null
                ? $" 도감 {owned}/{total} 완성!"
                : $" 도감 {owned}/{total} — 다음 보너스까지 {next.count - owned}종";

            RebuildCatalog();
        }

        private void RebuildCatalog()
        {
            foreach (Transform child in _catalogGrid) Destroy(child.gameObject);

            var defs = _session.Units.Defs.Values
                .Where(d => d.kind == _kind)
                .OrderBy(d => d.grade).ThenBy(d => d.subTier).ThenBy(d => d.id);

            foreach (var def in defs)
            {
                var unit = _session.Units.Get(def.id);
                bool isOwned = unit != null;
                var gradeColor = UIFactory.GradeColor(def.grade);

                var tile = UIFactory.CreateButton(_catalogGrid, $"T_{def.id}", "", () =>
                    UnitDetailView.Open(Rect.parent, _session, def.id, Refresh),
                    isOwned
                        ? Color.Lerp(UIFactory.Panel, gradeColor, unit.equipped ? 0.45f : 0.18f)
                        : new Color(0.10f, 0.09f, 0.14f), 22);
                Destroy(tile.GetComponentInChildren<Text>().gameObject);

                if (isOwned)
                {
                    var outline = tile.gameObject.AddComponent<Outline>();
                    outline.effectColor = gradeColor;
                    outline.effectDistance = new Vector2(3, 3);
                }

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

                if (!string.IsNullOrEmpty(def.element))
                {
                    var elem = UIFactory.CreateText(tile.transform, "Elem",
                        Elements.Icon(def.element), 26, TextAnchor.UpperLeft);
                    elem.raycastTarget = false;
                    UIFactory.TopBand(elem.rectTransform, 8, 36, 10);
                }

                var nameText = UIFactory.CreateText(tile.transform, "Name", def.name, 23,
                    TextAnchor.MiddleCenter, isOwned ? gradeColor : UIFactory.TextDim);
                nameText.raycastTarget = false;
                UIFactory.BottomBand(nameText.rectTransform, 84, 40, 6);

                string status;
                if (!isOwned) status = "미보유";
                else
                {
                    var parts = new System.Collections.Generic.List<string>();
                    if (unit.equipped) parts.Add("★장착");
                    if (def.maxLevel > 1) parts.Add($"Lv.{unit.level}");
                    parts.Add($"{unit.limitBreak}돌");
                    status = string.Join(" ", parts);
                }
                var statusText = UIFactory.CreateText(tile.transform, "Status", status, 21,
                    TextAnchor.MiddleCenter, isOwned ? UIFactory.TextMain : UIFactory.TextDim);
                statusText.raycastTarget = false;
                UIFactory.BottomBand(statusText.rectTransform, 46, 36, 6);

                if (isOwned && def.upgradeToId != null)
                {
                    int surplus = _session.Units.SurplusCopies(def.id);
                    var gaugeText = UIFactory.CreateText(tile.transform, "Gauge",
                        surplus >= _session.Units.ComposeCost
                            ? $" 승급 가능 {surplus}/{_session.Units.ComposeCost}"
                            : $"승급 {surplus}/{_session.Units.ComposeCost}",
                        20, TextAnchor.MiddleCenter,
                        surplus >= _session.Units.ComposeCost ? UIFactory.Gold : UIFactory.TextDim);
                    gaugeText.raycastTarget = false;
                    UIFactory.BottomBand(gaugeText.rectTransform, 10, 34, 6);
                }
            }
        }
    }
}
