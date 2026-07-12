using System.Collections.Generic;
using IdleCore;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 성장 탭 — 카드형: 축 이름 + Lv 칩 + 현재 효과, 우측 큰 강화 버튼 (재화/비용 2줄).
    /// </summary>
    public sealed class GrowthPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private readonly List<(string axisId, Text level, Text effect, Button button, Text buttonLabel, Text lockText)> _rows
            = new List<(string, Text, Text, Button, Text, Text)>();

        public static GrowthPanel Create(Transform root, GameSession session)
        {
            var rect = UIFactory.CreatePanel(root, "GrowthPanel", UIFactory.Bg);
            UIFactory.Stretch(rect, UIFactory.MainContentTop, UIFactory.MainContentBottom, UIFactory.ScreenGutter);
            var panel = rect.gameObject.AddComponent<GrowthPanel>();
            panel.Rect = rect;
            panel._session = session;
            panel.Build();
            return panel;
        }

        /// <summary>일괄 강화 배수 (x1/x10/x50/MAX=-1) — 성장·장비 강화 공용 설정.</summary>
        public static int BulkMultiplier = 1;
        private static readonly int[] BulkSteps = { 1, 10, 50, -1 };
        private Button _bulkButton;

        private void Build()
        {
            var list = UIFactory.CreateScrollList(Rect, spacing: 18);

            // 강화 배수 토글 (x1 → x10 → x50 → MAX)
            _bulkButton = UIFactory.CreateButton(list, "Bulk", "", () =>
            {
                int index = System.Array.IndexOf(BulkSteps, BulkMultiplier);
                BulkMultiplier = BulkSteps[(index + 1) % BulkSteps.Length];
                Refresh();
            }, UIFactory.PanelRaised, 25);
            _bulkButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 68;

            foreach (var kv in _session.Stats.Axes)
            {
                string axisId = kv.Key;
                var axis = kv.Value;

                var card = UIFactory.CreatePanel(list, $"Axis_{axisId}", UIFactory.Panel);
                UIFactory.Roundify(card.GetComponent<Image>());
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 168;

                var accent = UIFactory.CreatePanel(card, "Accent", UIFactory.Accent);
                accent.anchorMin = new Vector2(0, 0.18f);
                accent.anchorMax = new Vector2(0, 0.82f);
                accent.pivot = new Vector2(0, 0.5f);
                accent.anchoredPosition = new Vector2(0, 0);
                accent.sizeDelta = new Vector2(6, 0);

                // 이름 + Lv 칩
                var name = UIFactory.CreateText(card, "Name", axis.name, 30, TextAnchor.UpperLeft);
                name.fontStyle = FontStyle.Bold;
                UIFactory.TopBand(name.rectTransform, 22, 42, 32);
                name.rectTransform.offsetMax = new Vector2(-330, name.rectTransform.offsetMax.y);
                var level = UIFactory.CreateText(card, "Lv", "", 24, TextAnchor.UpperRight, UIFactory.Gold);
                UIFactory.TopBand(level.rectTransform, 24, 38, 32);
                level.rectTransform.offsetMin = new Vector2(430, level.rectTransform.offsetMin.y);
                level.rectTransform.offsetMax = new Vector2(-330, level.rectTransform.offsetMax.y);

                // 현재 효과 (흐리게)
                var effect = UIFactory.CreateText(card, "Effect", "", 23, TextAnchor.LowerLeft, UIFactory.TextDim);
                UIFactory.BottomBand(effect.rectTransform, 22, 46, 32);
                effect.rectTransform.offsetMax = new Vector2(-330, effect.rectTransform.offsetMax.y);

                // 강화 버튼 (배수 적용: x1/x10/x50/MAX)
                var button = UIFactory.CreateButton(card, "Up", "", () =>
                    PowerToast.Wrap(_session, Rect.parent, () =>
                    {
                        int count = BulkMultiplier < 0 ? 10000 : BulkMultiplier;
                        _session.Stats.LevelUpMany(axisId, _session.Wallet, count);
                        Refresh();
                    }), UIFactory.Accent, 25);
                var buttonRect = (RectTransform)button.transform;
                buttonRect.anchorMin = new Vector2(1, 0.5f);
                buttonRect.anchorMax = new Vector2(1, 0.5f);
                buttonRect.pivot = new Vector2(1, 0.5f);
                buttonRect.anchoredPosition = new Vector2(-22, 0);
                buttonRect.sizeDelta = new Vector2(285, 122);

                // 잠금 안내 (해금 전용, 카드 전체 덮는 텍스트)
                var lockText = UIFactory.CreateText(card, "Lock", "", 26, TextAnchor.MiddleRight, UIFactory.TextDim);
                UIFactory.Fill(lockText.rectTransform, 24);

                _rows.Add((axisId, level, effect, button, button.GetComponentInChildren<Text>(), lockText));
            }
        }

        public void Refresh()
        {
            if (_session == null) return;
            if (_bulkButton != null)
                _bulkButton.GetComponentInChildren<Text>().text =
                    $"강화 배수: {(BulkMultiplier < 0 ? "MAX" : $"x{BulkMultiplier}")}  (탭하여 변경)";
            foreach (var (axisId, level, effect, button, buttonLabel, lockText) in _rows)
            {
                var axis = _session.Stats.Axes[axisId];
                int lv = _session.Stats.GetLevel(axisId);
                bool unlocked = _session.Progression.HighestClearedIndex >= axis.unlockStageIndex;

                if (!unlocked)
                {
                    var stage = new IdleCore.Progression.StageId(axis.unlockStageIndex);
                    level.text = "";
                    effect.text = "";
                    lockText.text = $" {stage.Display(_session.Config.stage)} 클리어 시 해금";
                    button.gameObject.SetActive(false);
                    continue;
                }

                lockText.text = "";
                button.gameObject.SetActive(true);
                level.text = $"Lv.{lv}";
                effect.text = axis.effects.Count > 0 && lv > 0
                    ? "현재 " + UnitDetailView.Describe(axis.effects[0], lv)
                    : "강화하여 효과 획득";

                long cost = _session.Stats.NextCost(axisId);
                bool affordable = _session.Wallet.CanAfford(axis.costCurrency, cost)
                                  && _session.Stats.CanLevelUp(axisId);
                button.interactable = affordable;
                string mult = BulkMultiplier < 0 ? "MAX" : $"x{BulkMultiplier}";
                buttonLabel.text = $"강화 {mult}\n{CurrencyLabel(axis.costCurrency)} {UIFactory.FormatNumber(cost)}~";
            }
        }

        public static string CurrencyLabel(string currency) => currency switch
        {
            "gold" => "골드",
            "soul" => "영혼",
            "gem_soft" => "영옥",
            "gem_hard" => "수정",
            "awaken_stone" => "각성석",
            "sweep_ticket" => "소탕권",
            "mileage" => "마일리지",
            _ => currency,
        };
    }
}
