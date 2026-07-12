using System.Linq;
using IdleCore;
using IdleCore.Gacha;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 소환 탭 — 순수 뽑기: 배너 선택 → 소환 레벨/확률 공시 → 소환 버튼(1/10/광고) → 결과.
    /// 장착·승급·도감 관리는 편성 탭(EquipPanel)에서.
    /// </summary>
    public sealed class GachaPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private Text _pityText, _resultText;
        private Button _pull1Button, _pull10Button, _adPullButton;
        private readonly System.Collections.Generic.List<Button> _bannerButtons
            = new System.Collections.Generic.List<Button>();
        private string _bannerId;

        public static GachaPanel Create(Transform root, GameSession session)
        {
            var rect = UIFactory.CreatePanel(root, "GachaPanel", UIFactory.Bg);
            UIFactory.Stretch(rect, UIFactory.MainContentTop, UIFactory.MainContentBottom, UIFactory.ScreenGutter);
            var panel = rect.gameObject.AddComponent<GachaPanel>();
            panel.Rect = rect;
            panel._session = session;
            panel.Build();
            return panel;
        }

        private void Build()
        {
            _bannerId = _session.Gacha.Banners.Keys.First();
            var list = UIFactory.CreateScrollList(Rect, spacing: 14);

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
            _pityText.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;

            _pull1Button = UIFactory.CreateButton(list, "Pull1", "", () => Pull(1));
            _pull1Button.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

            _pull10Button = UIFactory.CreateButton(list, "Pull10", "", () => Pull(10), UIFactory.Gold);
            _pull10Button.GetComponentInChildren<Text>().color = Color.black;
            _pull10Button.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

            _adPullButton = UIFactory.CreateButton(list, "AdPull", "", () =>
            {
                if (!_session.Ads.CanUse("free_summon")) { _resultText.text = "오늘의 무료 소환을 다 썼습니다"; return; }
                _session.Ads.Use("free_summon", ok =>
                {
                    if (!ok) return;
                    if (_session.Gacha.TryPullFree(_bannerId, 1, out var result))
                    {
                        var def = _session.Units.Defs[result.UnitIds[0]];
                        _resultText.text = $" 무료 소환: [{GradeLabel(def.grade)}] {def.name}";
                    }
                    Refresh();
                });
            }, new Color(0.55f, 0.3f, 0.75f), 27);
            _adPullButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 86;

            _resultText = UIFactory.CreateText(list, "Result", "", 27, TextAnchor.UpperCenter);
            _resultText.gameObject.AddComponent<LayoutElement>().preferredHeight = 320;

            var hint = UIFactory.CreateText(list, "Hint",
                "장착·승급·도감은 [편성] 탭에서 관리합니다", 23, TextAnchor.MiddleCenter, UIFactory.TextDim);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
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
                .Take(6)
                .Select(g =>
                {
                    var def = _session.Units.Defs[g.Key];
                    string elem = string.IsNullOrEmpty(def.element) ? "" : Elements.Icon(def.element);
                    return $"[{GradeLabel(def.grade)}] {elem}{def.name} ×{g.Count()}";
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
                    ? $" 무료 소환 1회 (프리패스, 남은 {adLeft}회)"
                    : $" 광고 보고 무료 소환 (남은 {adLeft}회)";
            _adPullButton.interactable = adLeft > 0;
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
            "costume" => "코스튬",
            _ => kind,
        };
    }
}
