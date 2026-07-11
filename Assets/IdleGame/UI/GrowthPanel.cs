using System.Collections.Generic;
using IdleCore;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>성장 탭 — 성장 축 목록. config.json에 축을 추가하면 여기 자동으로 줄이 생긴다.</summary>
    public sealed class GrowthPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private readonly List<(string axisId, Text label, Button button, Text buttonLabel)> _rows
            = new List<(string, Text, Button, Text)>();

        public static GrowthPanel Create(Transform root, GameSession session)
        {
            var rect = UIFactory.CreatePanel(root, "GrowthPanel", UIFactory.Bg);
            UIFactory.Stretch(rect, 590, 150); // 전투 뷰 아래 ~ 탭 바(140) 위
            var panel = rect.gameObject.AddComponent<GrowthPanel>();
            panel.Rect = rect;
            panel._session = session;
            panel.Build();
            return panel;
        }

        private void Build()
        {
            var list = UIFactory.CreateScrollList(Rect);

            foreach (var kv in _session.Stats.Axes)
            {
                string axisId = kv.Key;
                var axis = kv.Value;

                var row = UIFactory.CreatePanel(list, $"Axis_{axisId}", UIFactory.Panel);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 130;

                var label = UIFactory.CreateText(row, "Label", axis.name, 30, TextAnchor.MiddleLeft);
                UIFactory.Fill(label.rectTransform, 20);
                label.rectTransform.offsetMin = new Vector2(40, 10); // 왼쪽 클리핑 방지 여백

                var button = UIFactory.CreateButton(row, "LevelUp", "강화", () =>
                {
                    if (!_session.Stats.CanLevelUp(axisId)) return;
                    long cost = _session.Stats.NextCost(axisId);
                    if (!_session.Wallet.TrySpend(axis.costCurrency, cost)) return;
                    _session.Stats.LevelUp(axisId);
                    Refresh();
                }, UIFactory.Accent, 28);
                var buttonRect = (RectTransform)button.transform;
                buttonRect.anchorMin = new Vector2(1, 0.5f);
                buttonRect.anchorMax = new Vector2(1, 0.5f);
                buttonRect.pivot = new Vector2(1, 0.5f);
                buttonRect.anchoredPosition = new Vector2(-20, 0);
                buttonRect.sizeDelta = new Vector2(280, 96);

                _rows.Add((axisId, label, button, button.GetComponentInChildren<Text>()));
            }
        }

        public void Refresh()
        {
            if (_session == null) return;
            foreach (var (axisId, label, button, buttonLabel) in _rows)
            {
                var axis = _session.Stats.Axes[axisId];
                int level = _session.Stats.GetLevel(axisId);
                bool unlocked = _session.Progression.HighestClearedIndex >= axis.unlockStageIndex;

                if (!unlocked)
                {
                    var stage = new IdleCore.Progression.StageId(axis.unlockStageIndex);
                    label.text = $"{axis.name}  —  🔒 {stage.Display(_session.Config.stage)} 해금";
                    button.interactable = false;
                    buttonLabel.text = "잠김";
                    continue;
                }

                long cost = _session.Stats.NextCost(axisId);
                label.text = $"{axis.name}  Lv.{level}";
                bool affordable = _session.Wallet.CanAfford(axis.costCurrency, cost)
                                  && _session.Stats.CanLevelUp(axisId);
                button.interactable = affordable;
                buttonLabel.text = $"{CurrencyLabel(axis.costCurrency)} {UIFactory.FormatNumber(cost)}";
            }
        }

        private static string CurrencyLabel(string currency) => currency switch
        {
            "gold" => "골드",
            "soul" => "영혼",
            "gem_soft" => "영옥",
            "gem_hard" => "수정",
            _ => currency,
        };
    }
}
