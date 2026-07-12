using System.Linq;
using IdleCore;
using IdleCore.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>정보 탭 — 세부 스탯(소헌키의 '세부 정보'), 장착 현황, 도감 마일스톤.</summary>
    public sealed class StatusPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private Text _statsText, _equipText, _collectionText;

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
            var list = UIFactory.CreateScrollList(Rect);

            Header(list, "세부 정보");
            _statsText = Body(list, 620);

            Header(list, "장착 현황");
            _equipText = Body(list, 440);

            Header(list, "도감 보너스");
            _collectionText = Body(list, 400);
        }

        private static void Header(RectTransform list, string title)
        {
            var text = UIFactory.CreateText(list, $"H_{title}", $"── {title} ──", 30,
                TextAnchor.MiddleLeft, UIFactory.Gold);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
        }

        private static Text Body(RectTransform list, float height)
        {
            var text = UIFactory.CreateText(list, "Body", "", 28, TextAnchor.UpperLeft);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            text.lineSpacing = 1.35f;
            return text;
        }

        public void Refresh()
        {
            if (_session == null || _statsText == null) return;
            var s = _session.Stats.Snapshot();

            double finalMul = s.Get(StatType.FinalDamage);
            if (finalMul <= 0) finalMul = 1;
            double goldMul = s.Get(StatType.GoldGain); if (goldMul <= 0) goldMul = 1;
            double soulMul = s.Get(StatType.SoulGain); if (soulMul <= 0) soulMul = 1;
            double offRate = s.Get(StatType.OfflineRate); if (offRate <= 0) offRate = 1;

            _statsText.text =
                $"⚔ 전투력        {UIFactory.FormatNumber(s.CombatPower())}\n" +
                $"────────────────\n" +
                $"체력            {UIFactory.FormatNumber(s.Get(StatType.Health))}\n" +
                $"유효 체력       {UIFactory.FormatNumber(s.EffectiveHp())}\n" +
                $"공격력          {UIFactory.FormatNumber(s.Get(StatType.Attack))}\n" +
                $"공격 속도       {s.Get(StatType.AttackSpeed):0.00}/초\n" +
                $"치명타 확률     {s.Get(StatType.CritChance) * 100:0.#}%\n" +
                $"치명타 피해     ×{s.Get(StatType.CritMultiplier):0.00}\n" +
                $"최종 데미지     ×{finalMul:0.00}\n" +
                $"────────────────\n" +
                $"DPS             {UIFactory.FormatNumber(s.Dps())}\n" +
                $"────────────────\n" +
                $"골드 획득       ×{goldMul:0.00}\n" +
                $"영혼 획득       ×{soulMul:0.00}\n" +
                $"방치 보상       ×{offRate:0.00}\n" +
                $"방치 상한       {_session.Config.offline.baseCapHours + s.Get(StatType.OfflineCapHours):0.#}시간";

            var equipLines = _session.Config.equipSlots.Keys.Select(kind =>
            {
                var equipped = _session.Units.AllOwned()
                    .Where(u => u.equipped && _session.Units.Defs[u.unitId].kind == kind)
                    .Select(u => _session.Units.Defs[u.unitId].name);
                string names = string.Join(", ", equipped);
                return $"{GachaPanel.KindLabel(kind)} ({_session.Units.EquippedCount(kind)}/{_session.Units.SlotLimit(kind)}): " +
                       (names.Length == 0 ? "—" : names);
            });
            _equipText.text = string.Join("\n", equipLines);

            int owned = _session.Units.UniqueOwnedCount;
            var milestoneLines = _session.Units.Milestones.Select(m =>
            {
                string mark = owned >= m.count ? "✓" : "  ";
                var effectDesc = string.Join(", ", m.effects.Select(DescribeEffect));
                return $"{mark} {m.count}종 등록 — {effectDesc}";
            });
            _collectionText.text = $"등록 {owned}/{_session.Units.Defs.Count}종\n" + string.Join("\n", milestoneLines);
        }

        private static string DescribeEffect(StatEffect effect)
        {
            string stat = effect.stat switch
            {
                StatType.Attack => "공격력",
                StatType.FinalDamage => "최종 데미지",
                StatType.GoldGain => "골드 획득",
                _ => effect.stat.ToString(),
            };
            double v = effect.value.Evaluate(1);
            return effect.mode == EffectMode.Mul ? $"{stat} +{v * 100:0.#}%" : $"{stat} +{v:0.#}";
        }
    }
}
