using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 연출 — 수식 기반 전투에 '보는 맛'을 입힌다.
    /// 캐릭터 숨쉬기/공격 돌진, 몬스터 피격 플래시·흔들림·처치 팝, 떠오르는 데미지 숫자.
    /// 실제 계산과 무관한 순수 연출 (DPS/킬 속도를 반영해 체감 동기화).
    /// </summary>
    public sealed class BattleAnimator : MonoBehaviour
    {
        private RectTransform _character;
        private RectTransform _mob;
        private Image _mobImage;
        private RectTransform _battleArea;

        private double _dps;
        private double _killsPerSecond;
        private double _enemyAttackPerHit;
        private Vector2 _charBase, _mobBase;
        private float _attackTimer;
        private float _mobAttackTimer = 1.0f;
        private Image _charImage;

        private Image _mobHpFill;
        private int _hitsOnCurrentMob;

        public static BattleAnimator Attach(RectTransform battleArea, RectTransform character, Image mobImage,
            Image mobHpFill = null)
        {
            var animator = battleArea.gameObject.AddComponent<BattleAnimator>();
            animator._battleArea = battleArea;
            animator._character = character;
            animator._mob = mobImage != null ? (RectTransform)mobImage.transform : null;
            animator._mobImage = mobImage;
            animator._mobHpFill = mobHpFill;
            if (character != null)
            {
                animator._charBase = character.anchoredPosition;
                animator._charImage = character.GetComponent<Image>();
            }
            if (animator._mob != null) animator._mobBase = animator._mob.anchoredPosition;
            return animator;
        }

        /// <summary>파밍 틱마다 호출 — 연출 속도를 실제 전투 수치에 동기화.</summary>
        public void SetRates(double dps, double killsPerSecond, double enemyAttackPerHit = 0)
        {
            _dps = dps;
            _killsPerSecond = killsPerSecond;
            _enemyAttackPerHit = enemyAttackPerHit;
        }

        private void Update()
        {
            if (_character == null) return;

            // 숨쉬기 (idle bob)
            float bob = Mathf.Sin(Time.time * 2.2f) * 6f;
            _character.anchoredPosition = _charBase + new Vector2(0, bob);

            // 공격 주기: 킬 속도 반영 (최소 0.5초, 최대 1.6초 간격)
            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                float interval = _killsPerSecond > 0
                    ? Mathf.Clamp(1f / (float)_killsPerSecond, 0.5f, 1.6f)
                    : 1.6f;
                _attackTimer = interval;
                StartCoroutine(AttackOnce());
            }

            // 적 반격 연출 (enemyAttack이 있을 때만)
            if (_enemyAttackPerHit > 0 && _mobImage != null && _mobImage.enabled)
            {
                _mobAttackTimer -= Time.deltaTime;
                if (_mobAttackTimer <= 0f)
                {
                    _mobAttackTimer = Random.Range(1.3f, 2.1f);
                    StartCoroutine(MobAttackOnce());
                }
            }
        }

        private IEnumerator MobAttackOnce()
        {
            // 몬스터가 캐릭터 쪽으로 돌진
            const float lunge = 55f;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.14f)
            {
                _mob.anchoredPosition = _mobBase + new Vector2(-Mathf.Sin(t * Mathf.PI) * lunge, 0);
                yield return null;
            }
            _mob.anchoredPosition = _mobBase;

            // 캐릭터 피격: 빨간 플래시 + 흔들림 + 빨간 데미지 숫자
            if (_charImage != null)
            {
                _charImage.color = new Color(1f, 0.5f, 0.5f);
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.16f)
                {
                    _character.anchoredPosition = _charBase + new Vector2(Random.Range(-6f, 6f), Random.Range(-4f, 4f));
                    yield return null;
                }
                _charImage.color = Color.white;
                _character.anchoredPosition = _charBase;
            }
            var text = UIFactory.CreateText(_battleArea, "PDmg",
                $"-{UIFactory.FormatNumber(_enemyAttackPerHit)}", 32,
                TextAnchor.MiddleCenter, new Color(1f, 0.35f, 0.4f));
            text.fontStyle = FontStyle.Bold;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.28f, 0.66f);
            rect.anchoredPosition = new Vector2(Random.Range(-24f, 24f), 0);
            StartCoroutine(FloatAndFade(text));
        }

        private IEnumerator AttackOnce()
        {
            // 캐릭터 돌진
            const float lunge = 42f;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.12f)
            {
                _character.anchoredPosition = _charBase + new Vector2(Mathf.Sin(t * Mathf.PI) * lunge, 0);
                yield return null;
            }

            if (_mob != null && _mobImage != null && _mobImage.enabled)
            {
                SpawnDamageText();
                StartCoroutine(MobHit());
            }
        }

        /// <summary>이번 몹을 잡는 데 필요한 타격 수 (연출용) — 킬 속도가 느릴수록 여러 대.</summary>
        private int HitsPerMob()
        {
            if (_killsPerSecond >= 0.4) return 1;
            if (_killsPerSecond <= 0) return 6;
            return Mathf.Clamp(Mathf.RoundToInt(0.8f / (float)_killsPerSecond), 2, 6);
        }

        private IEnumerator MobHit()
        {
            // 적 체력바 감소
            _hitsOnCurrentMob++;
            int hitsNeeded = HitsPerMob();
            RootView.SetHpFill(_mobHpFill, 1f - _hitsOnCurrentMob / (float)hitsNeeded);

            // 피격 플래시 + 흔들림
            _mobImage.color = new Color(1f, 0.45f, 0.5f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.18f)
            {
                _mob.anchoredPosition = _mobBase + new Vector2(Random.Range(-7f, 7f), Random.Range(-5f, 5f));
                yield return null;
            }
            _mobImage.color = Color.white;
            _mob.anchoredPosition = _mobBase;

            // 필요 타격 수를 채우면 처치 팝 (사라졌다 재등장 = 다음 몹)
            if (_hitsOnCurrentMob >= HitsPerMob())
            {
                _hitsOnCurrentMob = 0;
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.12f)
                {
                    float s = Mathf.Lerp(1f, 0.05f, t);
                    _mob.localScale = new Vector3(s, s, 1);
                    yield return null;
                }
                _mob.localScale = Vector3.zero;
                yield return new WaitForSeconds(0.15f);
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.15f)
                {
                    float s = Mathf.LerpUnclamped(0.05f, 1f, 1.70158f * t * t * t - 1.70158f * t * t + t + t * t); // 약한 백이즈
                    _mob.localScale = new Vector3(Mathf.Max(0.05f, s), Mathf.Max(0.05f, s), 1);
                    yield return null;
                }
                _mob.localScale = Vector3.one;
                RootView.SetHpFill(_mobHpFill, 1f); // 다음 몹 등장 — 체력 리셋
            }
        }

        private void SpawnDamageText()
        {
            if (_dps <= 0) return;
            double perHit = _dps * Mathf.Clamp(1f / Mathf.Max(0.5f, (float)_killsPerSecond), 0.5f, 1.6f);
            var text = UIFactory.CreateText(_battleArea, "Dmg", UIFactory.FormatNumber(perHit), 34,
                TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f));
            text.fontStyle = FontStyle.Bold;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.78f, 0.62f);
            rect.anchoredPosition = new Vector2(Random.Range(-30f, 30f), Random.Range(-10f, 10f));
            StartCoroutine(FloatAndFade(text));
        }

        private IEnumerator FloatAndFade(Text text)
        {
            var rect = text.rectTransform;
            var start = rect.anchoredPosition;
            var baseColor = text.color;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.7f)
            {
                rect.anchoredPosition = start + new Vector2(0, t * 70f);
                text.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t * t);
                yield return null;
            }
            Destroy(text.gameObject);
        }
    }
}
