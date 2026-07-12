using System.Linq;
using IdleCore;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>미션/출석 오버레이 — HUD의 [미션] 버튼으로 연다. 일일 루틴의 허브.</summary>
    public sealed class MissionView : MonoBehaviour
    {
        private GameSession _session;
        private RectTransform _list;

        public static void Open(Transform root, GameSession session)
        {
            var overlay = UIFactory.CreatePanel(root, "MissionOverlay", new Color(0, 0, 0, 0.8f));
            UIFactory.Fill(overlay);
            var view = overlay.gameObject.AddComponent<MissionView>();
            view._session = session;
            view.Build(overlay);
        }

        private void Build(RectTransform overlay)
        {
            var panel = UIFactory.CreatePanel(overlay, "Panel", UIFactory.Panel);
            panel.anchorMin = new Vector2(0.05f, 0.12f);
            panel.anchorMax = new Vector2(0.95f, 0.9f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            var title = UIFactory.CreateText(panel, "Title", "📋 일일 미션 · 출석", 40);
            UIFactory.TopBand(title.rectTransform, 10, 70, 20);

            var listArea = UIFactory.CreatePanel(panel, "ListArea", UIFactory.Panel);
            UIFactory.Stretch(listArea, 90, 110);
            _list = UIFactory.CreateScrollList(listArea, spacing: 10);

            var close = UIFactory.CreateButton(panel, "Close", "닫기", () => Destroy(overlay.gameObject));
            UIFactory.BottomBand((RectTransform)close.transform, 16, 80, 60);

            Rebuild();
        }

        private void Rebuild()
        {
            foreach (Transform child in _list) Destroy(child.gameObject);

            // ── 출석부 ──
            var att = _session.Attendance;
            var attRow = UIFactory.CreateButton(_list, "Attendance",
                att.CanClaimToday()
                    ? $"🗓 출석부 {att.CurrentDay}일차 — 받기!"
                    : $"🗓 출석부 {(att.CurrentDay > 1 ? att.CurrentDay - 1 : att.DayCount)}일차 수령 완료 (내일 또 만나요)",
                () => { if (_session.Attendance.TryClaimToday()) Rebuild(); },
                att.CanClaimToday() ? UIFactory.Accent : UIFactory.Bg, 28);
            attRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;

            // ── 일일 미션 ──
            foreach (var def in _session.Missions.Defs.Values)
            {
                var state = _session.Missions.State(def.id);
                bool claimable = _session.Missions.CanClaim(def.id);
                string status = state.claimed ? "✓ 완료"
                    : claimable ? "받기!"
                    : $"{state.progress}/{def.target}";
                string rewards = string.Join(" ", def.rewards.Select(r =>
                    $"{GrowthPanel.CurrencyLabel(r.currency)} {UIFactory.FormatNumber(r.amount)}"));

                var row = UIFactory.CreateButton(_list, $"M_{def.id}",
                    $"{def.name}  [{status}]\n{rewards}",
                    () => { if (_session.Missions.TryClaim(def.id)) Rebuild(); },
                    claimable ? UIFactory.Accent : UIFactory.Bg, 26);
                row.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                row.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(20, 0);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 96;
            }
        }
    }
}
