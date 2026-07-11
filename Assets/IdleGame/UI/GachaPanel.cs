using System.Linq;
using IdleCore;
using IdleCore.Gacha;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>소환 탭 — 배너, 천장 카운터, x1/x10 소환, 결과, 보유 유닛 요약.</summary>
    public sealed class GachaPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private Text _pityText, _resultText, _ownedText;
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
            var banner = _session.Gacha.Banners.Values.First();
            _bannerId = banner.id;

            var list = UIFactory.CreateScrollList(Rect, spacing: 18);

            var title = UIFactory.CreateText(list, "Title", banner.name, 44);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;

            _pityText = UIFactory.CreateText(list, "Pity", "", 28, TextAnchor.MiddleCenter, UIFactory.TextDim);
            _pityText.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;

            var pull1 = UIFactory.CreateButton(list, "Pull1",
                $"1회 소환  (영옥 {banner.costPerPull})", () => Pull(1));
            pull1.gameObject.AddComponent<LayoutElement>().preferredHeight = 110;

            var pull10 = UIFactory.CreateButton(list, "Pull10",
                $"10연 소환  (영옥 {banner.CostFor(10)})", () => Pull(10), UIFactory.Gold);
            pull10.GetComponentInChildren<Text>().color = Color.black;
            pull10.gameObject.AddComponent<LayoutElement>().preferredHeight = 110;

            _resultText = UIFactory.CreateText(list, "Result", "", 28, TextAnchor.UpperCenter);
            _resultText.gameObject.AddComponent<LayoutElement>().preferredHeight = 320;

            _ownedText = UIFactory.CreateText(list, "Owned", "", 26, TextAnchor.UpperLeft, UIFactory.TextDim);
            _ownedText.gameObject.AddComponent<LayoutElement>().preferredHeight = 380;
        }

        private void Pull(int count)
        {
            if (!_session.Gacha.TryPull(_bannerId, count, out var result))
            {
                _resultText.text = "영옥이 부족합니다";
                return;
            }
            // 신규 유닛 자동 장착 (프로토: 장착 UI 생략)
            foreach (var unit in _session.Units.AllOwned())
                if (!unit.equipped) _session.Units.SetEquipped(unit.unitId, true);

            var lines = result.UnitIds
                .GroupBy(id => id)
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
            if (_session == null) return;
            var banner = _session.Gacha.Banners[_bannerId];
            int pity = _session.Gacha.PityCounter(_bannerId);
            _pityText.text = $"천장까지 {banner.pityThreshold - pity}회  (고대 확정)";

            var owned = _session.Units.AllOwned()
                .OrderByDescending(u => _session.Units.Defs[u.unitId].grade)
                .Select(u =>
                {
                    var def = _session.Units.Defs[u.unitId];
                    return $"[{GradeLabel(def.grade)}] {def.name}  {u.limitBreak}돌파 (사본 {u.copies})";
                })
                .ToList();
            _ownedText.text = owned.Count == 0
                ? "보유 유닛 없음 — 소환으로 차사를 모집하세요"
                : "── 명부 (보유 유닛) ──\n" + string.Join("\n", owned);
        }

        private static string GradeLabel(UnitGrade grade) => grade switch
        {
            UnitGrade.Ancient => "고대",
            UnitGrade.Mythic => "신화",
            UnitGrade.Epic => "전설",
            _ => "특급",
        };
    }
}
