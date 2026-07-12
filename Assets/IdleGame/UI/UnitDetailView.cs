using System.Collections.Generic;
using System.Linq;
using IdleCore;
using IdleCore.Gacha;
using IdleCore.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 유닛/장비 상세 팝업 (소헌키의 장비 상세) — 장착 효과/보유 효과 분리 표시,
    /// 장착·강화·승급 버튼. 미보유 유닛도 열람 가능 (수집욕).
    /// </summary>
    public sealed class UnitDetailView : MonoBehaviour
    {
        private GameSession _session;
        private string _unitId;
        private System.Action _onChanged;
        private Transform _root;
        private Text _body;
        private Button _equipButton, _levelButton, _composeButton;

        public static void Open(Transform root, GameSession session, string unitId, System.Action onChanged)
        {
            var overlay = UIFactory.CreatePanel(root, "UnitDetail", new Color(0, 0, 0, 0.8f));
            UIFactory.Fill(overlay);
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(() => Destroy(overlay.gameObject));
            var view = overlay.gameObject.AddComponent<UnitDetailView>();
            view._session = session;
            view._unitId = unitId;
            view._onChanged = onChanged;
            view._root = root;
            view.Build(overlay);
        }

        private void Build(RectTransform overlay)
        {
            var def = _session.Units.Defs[_unitId];
            var gradeColor = UIFactory.GradeColor(def.grade);

            var panel = UIFactory.CreatePanel(overlay, "Panel", UIFactory.Panel);
            UIFactory.Roundify(panel.GetComponent<Image>());
            panel.anchorMin = new Vector2(0.06f, 0.16f);
            panel.anchorMax = new Vector2(0.94f, 0.88f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Button>(); // 패널 클릭이 오버레이 닫기로 전파되지 않게 차단
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = gradeColor;
            outline.effectDistance = new Vector2(3, 3);

            // 아이콘
            var icon = UIFactory.LoadSprite($"art/units/{def.artId ?? def.id}.png");
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(panel, false);
                var iconImage = iconGo.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                var unit0 = _session.Units.Get(_unitId);
                iconImage.color = unit0 != null ? Color.white : new Color(0.08f, 0.07f, 0.11f);
                var iconRect = (RectTransform)iconGo.transform;
                UIFactory.TopBand(iconRect, 20, 210);
            }

            string elementTag = string.IsNullOrEmpty(def.element)
                ? "" : $" {IdleCore.Elements.Icon(def.element)}{IdleCore.Elements.Label(def.element)}";
            var title = UIFactory.CreateText(panel, "Title",
                $"[{GachaPanel.GradeLabel(def.grade)}] {def.name}{elementTag}", 38,
                TextAnchor.MiddleCenter, gradeColor);
            UIFactory.TopBand(title.rectTransform, 236, 54, 20);

            var bodyArea = UIFactory.CreatePanel(panel, "BodyArea", UIFactory.Panel);
            UIFactory.Stretch(bodyArea, 300, 120);
            var list = UIFactory.CreateScrollList(bodyArea, spacing: 6);
            _body = UIFactory.CreateText(list, "Body", "", 27, TextAnchor.UpperLeft);
            _body.lineSpacing = 1.3f;
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 하단 버튼
            var buttonBar = UIFactory.CreatePanel(panel, "Buttons", UIFactory.Panel);
            UIFactory.BottomBand(buttonBar, 16, 90, 16);
            var layout = buttonBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.spacing = 10;

            _equipButton = UIFactory.CreateButton(buttonBar, "Equip", "", () =>
                PowerToast.Wrap(_session, _root, () =>
                {
                    var unit = _session.Units.Get(_unitId);
                    if (unit == null) return;
                    if (unit.equipped) _session.Units.Unequip(_unitId);
                    else if (!_session.Units.TryEquip(_unitId))
                        _body.text = "슬롯이 가득 찼습니다 — 다른 속성을 쓰면 슬롯 +1 (최대 +3)\n\n" + _body.text;
                    _onChanged?.Invoke();
                    Refresh();
                }), UIFactory.Accent, 27);

            _levelButton = UIFactory.CreateButton(buttonBar, "Level", "", () =>
                PowerToast.Wrap(_session, _root, () =>
                {
                    int count = GrowthPanel.BulkMultiplier < 0 ? 10000 : GrowthPanel.BulkMultiplier;
                    if (_session.Units.TryLevelUpMany(_unitId, _session.Wallet, count) > 0)
                    {
                        _onChanged?.Invoke();
                        Refresh();
                    }
                }), new Color(0.2f, 0.3f, 0.5f), 27);

            _composeButton = UIFactory.CreateButton(buttonBar, "Compose", "", () =>
                PowerToast.Wrap(_session, _root, () =>
                {
                    if (_session.Units.TryComposeUnit(_unitId, out var newId))
                    {
                        _onChanged?.Invoke();
                        var newDef = _session.Units.Defs[newId];
                        _body.text = $"승급 성공! [{GachaPanel.GradeLabel(newDef.grade)}] {newDef.name} 획득\n\n" + _body.text;
                        Refresh();
                    }
                }), new Color(0.32f, 0.24f, 0.15f), 27);

            UIFactory.CreateButton(buttonBar, "Close", "닫기", () => Destroy(overlay.gameObject), UIFactory.Panel, 27);

            Refresh();
        }

        private void Refresh()
        {
            var def = _session.Units.Defs[_unitId];
            var unit = _session.Units.Get(_unitId);
            var sb = new System.Text.StringBuilder();

            if (unit == null)
            {
                sb.AppendLine("〈 미보유 — 소환으로 획득하세요 〉\n");
            }
            else
            {
                sb.Append($"보유 사본 {unit.copies} · {unit.limitBreak}돌파");
                if (def.maxLevel > 1) sb.Append($" · Lv.{unit.level}/{def.maxLevel}");
                if (unit.equipped) sb.Append(" · ★장착 중");
                sb.AppendLine("\n");
            }

            sb.AppendLine("── 장착 효과 ──");
            foreach (var e in def.baseEffects) sb.AppendLine("· " + Describe(e, 1));
            int level = unit?.level ?? 1;
            foreach (var e in def.levelEffects) sb.AppendLine($"· {Describe(e, level)} (Lv.{level})");
            foreach (var lb in def.limitBreakEffects)
            {
                bool active = unit != null && unit.limitBreak >= lb.atLimitBreak;
                sb.AppendLine($"{(active ? "✓" : "🔒")} {lb.atLimitBreak}돌파: {Describe(lb.effect, 1)}");
            }

            sb.AppendLine("\n── 보유 효과 (도감, 미장착에도 적용) ──");
            foreach (var e in def.collectionEffects) sb.AppendLine("· " + Describe(e, 1));
            foreach (var e in def.limitBreakScalingEffects)
            {
                int lb = unit?.limitBreak ?? 0;
                sb.AppendLine($"· 한계돌파 시 {Describe(e, System.Math.Max(1, lb))}" +
                              (lb > 0 ? $" (현재 {lb}돌)" : " (돌파당 증가)"));
            }
            if (def.skillCooldown > 0)
            {
                double burst = def.skillBurstSeconds * (1 + 0.08 * (unit?.limitBreak ?? 0));
                sb.AppendLine($"\n── 스킬 ──\n쿨타임 {def.skillCooldown:0.#}초 · 버스트 {burst:0.#}초어치 피해" +
                              (unit != null ? $" (돌파 보정 +{unit.limitBreak * 8}%)" : ""));
            }

            if (def.upgradeToId != null && _session.Units.Defs.TryGetValue(def.upgradeToId, out var up))
            {
                int surplus = unit != null ? _session.Units.SurplusCopies(_unitId) : 0;
                sb.AppendLine($"\n── 승급 ──\n{def.name} 잉여 {surplus}/{_session.Units.ComposeCost} → {up.name}");
            }
            _body.text = sb.ToString();

            bool owned = unit != null;
            _equipButton.gameObject.SetActive(owned);
            if (owned) _equipButton.GetComponentInChildren<Text>().text = unit.equipped ? "해제" : "장착";

            bool levelable = owned && def.maxLevel > 1;
            _levelButton.gameObject.SetActive(levelable);
            if (levelable)
            {
                long cost = _session.Units.LevelUpCost(_unitId);
                string mult = GrowthPanel.BulkMultiplier < 0 ? "MAX" : $"x{GrowthPanel.BulkMultiplier}";
                _levelButton.GetComponentInChildren<Text>().text =
                    cost < 0 ? "강화 MAX" : $"강화 {mult}\n{UIFactory.FormatNumber(cost)}~";
                _levelButton.interactable = cost >= 0 && _session.Wallet.CanAfford(def.levelCostCurrency, cost);
            }

            bool composable = owned && _session.Units.CanCompose(_unitId);
            _composeButton.gameObject.SetActive(def.upgradeToId != null && owned);
            if (_composeButton.gameObject.activeSelf)
            {
                _composeButton.GetComponentInChildren<Text>().text =
                    $"⚗ 승급 {_session.Units.SurplusCopies(_unitId)}/{_session.Units.ComposeCost}";
                _composeButton.interactable = composable;
            }
        }

        public static string Describe(StatEffect effect, int level)
        {
            string stat = effect.stat switch
            {
                StatType.Attack => "공격력",
                StatType.AttackSpeed => "공격 속도",
                StatType.CritChance => "치명타 확률",
                StatType.CritMultiplier => "치명타 피해",
                StatType.FinalDamage => "최종 데미지",
                StatType.Health => "체력",
                StatType.Defense => "방어도",
                StatType.GoldGain => "골드 획득",
                StatType.SoulGain => "영혼 획득",
                StatType.OfflineRate => "방치 보상",
                StatType.OfflineCapHours => "방치 상한",
                StatType.FireDamage => "🔥 불 속성 공격력",
                StatType.LightningDamage => "⚡ 번개 속성 공격력",
                StatType.DarkDamage => "🌑 어둠 속성 공격력",
                StatType.AllElementDamage => "모든 속성 피해",
                StatType.DefensePierce => "방어 무시",
                StatType.SoftGemGain => "영옥 획득량",
                _ => effect.stat.ToString(),
            };
            bool percentStat = effect.stat is StatType.CritChance
                or StatType.FireDamage or StatType.LightningDamage or StatType.DarkDamage
                or StatType.SoftGemGain;
            double v = effect.value.Evaluate(level);
            return effect.mode == EffectMode.Mul || percentStat
                ? $"{stat} +{v * 100:0.##}%"
                : $"{stat} +{UIFactory.FormatNumber(v)}";
        }
    }
}
