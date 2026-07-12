using System.Linq;
using IdleCore;
using IdleCore.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 정보 탭 — 한눈에 보는 대시보드:
    /// 스탯 카드 그리드(2열) + 장착 슬롯 프리뷰(아이콘) + 도감 진행바.
    /// </summary>
    public sealed class StatusPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;

        private readonly System.Collections.Generic.Dictionary<string, (Text value, Text sub)> _cards
            = new System.Collections.Generic.Dictionary<string, (Text, Text)>();
        private RectTransform _slotArea;
        private Image _collectionFill;
        private Text _collectionText;

        public static StatusPanel Create(Transform root, GameSession session)
        {
            var rect = UIFactory.CreatePanel(root, "StatusPanel", UIFactory.Bg);
            UIFactory.Stretch(rect, 590, 150);
            var panel = rect.gameObject.AddComponent<StatusPanel>();
            panel.Rect = rect;
            panel._session = session;
            panel.Build();
            return panel;
        }

        private void Build()
        {
            var list = UIFactory.CreateScrollList(Rect, spacing: 18);

            // ── 전투력 히어로 카드 (가로 전체, 큰 숫자) ──
            var heroCard = UIFactory.CreatePanel(list, "PowerCard", new Color(0.20f, 0.16f, 0.30f));
            UIFactory.Roundify(heroCard.GetComponent<Image>());
            heroCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
            var heroTitle = UIFactory.CreateText(heroCard, "T", " 전투력", 26, TextAnchor.UpperCenter, UIFactory.TextDim);
            UIFactory.TopBand(heroTitle.rectTransform, 16, 36);
            var heroValue = UIFactory.CreateText(heroCard, "V", "0", 56, TextAnchor.MiddleCenter, UIFactory.Gold);
            UIFactory.BottomBand(heroValue.rectTransform, 14, 86);
            _cards["power"] = (heroValue, null);

            // ── 스탯 카드 그리드 (2열) ──
            var grid = UIFactory.CreatePanel(list, "StatGrid", UIFactory.Bg);
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 2;
            gridLayout.cellSize = new Vector2(496, 118);
            gridLayout.spacing = new Vector2(14, 14);
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddStatCard(grid, "dps", "DPS");
            AddStatCard(grid, "attack", "공격력");
            AddStatCard(grid, "hp", "체력");
            AddStatCard(grid, "speed", "공격 속도");
            AddStatCard(grid, "crit", "치명타");
            AddStatCard(grid, "final", "최종 데미지");
            AddStatCard(grid, "gain", "획득 배율");
            AddStatCard(grid, "offline", "방치 보상");

            // ── 장착 현황 (슬롯 프리뷰) ──
            Header(list, "장착 현황");
            _slotArea = UIFactory.CreatePanel(list, "Slots", UIFactory.Bg);
            UIFactory.AddVerticalList(_slotArea, spacing: 8, padding: 0);
            _slotArea.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── 도감 진행 ──
            Header(list, "도감");
            var barBg = UIFactory.CreatePanel(list, "CollBar", new Color(0, 0, 0, 0.5f));
            UIFactory.Roundify(barBg.GetComponent<Image>(), shadow: false);
            barBg.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barBg, false);
            _collectionFill = fillGo.GetComponent<Image>();
            _collectionFill.color = UIFactory.Accent;
            UIFactory.Roundify(_collectionFill, shadow: false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.offsetMin = new Vector2(4, 4);
            fillRect.offsetMax = new Vector2(-4, -4);
            _collectionText = UIFactory.CreateText(list, "CollText", "", 26, TextAnchor.MiddleCenter, UIFactory.TextMain);
            _collectionText.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
        }

        private static void Header(RectTransform list, string title)
        {
            var text = UIFactory.CreateText(list, $"H_{title}", title, 30, TextAnchor.MiddleCenter, UIFactory.Gold);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
        }

        private void AddStatCard(RectTransform grid, string key, string title)
        {
            var card = UIFactory.CreatePanel(grid, $"C_{key}", UIFactory.Panel);
            UIFactory.Roundify(card.GetComponent<Image>());
            var titleText = UIFactory.CreateText(card, "T", title, 23, TextAnchor.UpperLeft, UIFactory.TextDim);
            UIFactory.TopBand(titleText.rectTransform, 12, 32, 20);
            var valueText = UIFactory.CreateText(card, "V", "0", 36, TextAnchor.MiddleLeft, UIFactory.TextMain);
            UIFactory.BottomBand(valueText.rectTransform, 12, 66, 20);
            var subText = UIFactory.CreateText(card, "S", "", 21, TextAnchor.LowerRight, UIFactory.TextDim);
            UIFactory.BottomBand(subText.rectTransform, 12, 30, 20);
            _cards[key] = (valueText, subText);
        }

        private void SetCard(string key, string value, string sub = "")
        {
            if (!_cards.TryGetValue(key, out var card)) return;
            card.value.text = value;
            if (card.sub != null) card.sub.text = sub;
        }

        public void Refresh()
        {
            if (_session == null || _slotArea == null) return;
            var s = _session.Stats.Snapshot();

            double finalMul = s.Get(StatType.FinalDamage); if (finalMul <= 0) finalMul = 1;
            double goldMul = s.Get(StatType.GoldGain); if (goldMul <= 0) goldMul = 1;
            double soulMul = s.Get(StatType.SoulGain); if (soulMul <= 0) soulMul = 1;
            double offRate = s.Get(StatType.OfflineRate); if (offRate <= 0) offRate = 1;

            SetCard("power", UIFactory.FormatNumber(s.CombatPower()));
            SetCard("dps", UIFactory.FormatNumber(s.Dps()));
            SetCard("attack", UIFactory.FormatNumber(s.Get(StatType.Attack)));
            SetCard("hp", UIFactory.FormatNumber(s.Get(StatType.Health)), $"유효 {UIFactory.FormatNumber(s.EffectiveHp())}");
            SetCard("speed", $"{s.Get(StatType.AttackSpeed):0.00}/초");
            SetCard("crit", $"{s.Get(StatType.CritChance) * 100:0.#}%", $"피해 ×{s.Get(StatType.CritMultiplier):0.0}");
            SetCard("final", $"×{finalMul:0.00}");
            SetCard("gain", $"골드 ×{goldMul:0.0}", $"영혼 ×{soulMul:0.0}");
            SetCard("offline", $"×{offRate:0.0}", $"상한 {_session.Config.offline.baseCapHours + s.Get(StatType.OfflineCapHours):0.#}h");

            RebuildSlots();

            int owned = _session.Units.UniqueOwnedCount;
            int total = _session.Units.Defs.Count;
            var fillRect = (RectTransform)_collectionFill.transform;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(owned / (float)Mathf.Max(1, total)), 1);
            var next = _session.Units.Milestones.FirstOrDefault(m => owned < m.count);
            _collectionText.text = next == null
                ? $"{owned}/{total} 완성!"
                : $"{owned}/{total} 등록 — 다음 보너스 {next.count}종";
        }

        /// <summary>종류별 장착 슬롯을 아이콘 타일로 표시 (빈 슬롯 = + 표시).</summary>
        private void RebuildSlots()
        {
            foreach (Transform child in _slotArea) Destroy(child.gameObject);

            foreach (var kind in _session.Config.equipSlots.Keys)
            {
                int limit = _session.Units.SlotLimit(kind);
                var row = UIFactory.CreatePanel(_slotArea, $"Row_{kind}", UIFactory.Bg);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 104;
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.spacing = 10;

                var label = UIFactory.CreateText(row, "L", GachaPanel.KindLabel(kind), 26,
                    TextAnchor.MiddleCenter, UIFactory.TextDim);
                label.rectTransform.sizeDelta = new Vector2(86, 96);

                var equipped = _session.Units.AllOwned()
                    .Where(u => u.equipped && _session.Units.Defs[u.unitId].kind == kind)
                    .ToList();

                for (int i = 0; i < limit && i < 6; i++)
                {
                    var slot = UIFactory.CreatePanel(row, $"S{i}", new Color(0, 0, 0, 0.45f));
                    UIFactory.Roundify(slot.GetComponent<Image>(), shadow: false);
                    slot.sizeDelta = new Vector2(96, 96);

                    if (i < equipped.Count)
                    {
                        var def = _session.Units.Defs[equipped[i].unitId];
                        var outline = slot.gameObject.AddComponent<Outline>();
                        outline.effectColor = UIFactory.GradeColor(def.grade);
                        outline.effectDistance = new Vector2(2, 2);
                        var icon = UIFactory.LoadSprite($"art/units/{def.artId ?? def.id}.png");
                        if (icon != null)
                        {
                            var iconGo = new GameObject("I", typeof(RectTransform), typeof(Image));
                            iconGo.transform.SetParent(slot, false);
                            var iconImage = iconGo.GetComponent<Image>();
                            iconImage.sprite = icon;
                            iconImage.preserveAspect = true;
                            UIFactory.Fill((RectTransform)iconGo.transform, 6);
                        }
                    }
                    else
                    {
                        var plus = UIFactory.CreateText(slot, "P", "+", 40, TextAnchor.MiddleCenter,
                            new Color(1, 1, 1, 0.15f));
                        UIFactory.Fill(plus.rectTransform);
                    }
                }
            }
        }
    }
}
