using System.Collections;
using IdleCore;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투력 변화 토스트 — 장비/차사/스킬 교체·강화 시 "1.45K → 1.62K (▲ +11.7%)" 팝업.
    /// 사용: PowerToast.Wrap(session, root, () => { ...행동... });
    /// </summary>
    public static class PowerToast
    {
        /// <summary>행동 전후 전투력을 비교해 변화가 있으면 토스트 표시.</summary>
        public static void Wrap(GameSession session, Transform root, System.Action action)
        {
            double before = session.Stats.Snapshot().CombatPower();
            action();
            double after = session.Stats.Snapshot().CombatPower();
            if (System.Math.Abs(after - before) < 0.5) return;
            Show(root, before, after);
        }

        public static void Show(Transform root, double before, double after)
        {
            bool up = after >= before;
            double percent = before > 0 ? (after - before) / before * 100 : 0;

            var panel = UIFactory.CreatePanel(root, "PowerToast",
                up ? new Color(0.12f, 0.25f, 0.16f, 0.97f) : new Color(0.3f, 0.12f, 0.14f, 0.97f));
            UIFactory.Roundify(panel.GetComponent<Image>());
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.72f);
            panel.sizeDelta = new Vector2(660, 96);

            var text = UIFactory.CreateText(panel, "T",
                $" {UIFactory.FormatNumber(before)} → {UIFactory.FormatNumber(after)}  " +
                (up ? $"<color=#7dffa0>▲ +{percent:0.#}%</color>" : $"<color=#ff8090>▼ {percent:0.#}%</color>"),
                32, TextAnchor.MiddleCenter);
            UIFactory.Fill(text.rectTransform);

            var runner = panel.gameObject.AddComponent<ToastRunner>();
            runner.StartCoroutine(runner.Run(panel));
            AudioManager.Play(up ? "levelup" : "click", 0.4f);
        }

        private sealed class ToastRunner : MonoBehaviour
        {
            public IEnumerator Run(RectTransform panel)
            {
                var start = panel.anchoredPosition;
                var group = panel.gameObject.AddComponent<CanvasGroup>();
                for (float t = 0; t < 1f; t += Time.deltaTime / 1.6f)
                {
                    panel.anchoredPosition = start + new Vector2(0, t * 46f);
                    group.alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
                    yield return null;
                }
                Destroy(panel.gameObject);
            }
        }
    }
}
