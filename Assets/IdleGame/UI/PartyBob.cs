using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>파티 미니 유닛의 생동감 — 랜덤 위상 숨쉬기 + 가끔 폴짝.</summary>
    public sealed class PartyBob : MonoBehaviour
    {
        private RectTransform _rect;
        private Vector2 _base;
        private float _phase, _speed, _hopTimer;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _base = _rect.anchoredPosition;
            _phase = Random.Range(0f, Mathf.PI * 2);
            _speed = Random.Range(1.8f, 2.8f);
            _hopTimer = Random.Range(2f, 6f);
        }

        private float _lungeTime = 1f;

        /// <summary>스킬 시전 등 — 앞으로 돌진했다 복귀.</summary>
        public void Lunge() => _lungeTime = 0f;

        private void Update()
        {
            float y = Mathf.Sin(Time.time * _speed + _phase) * 5f;
            _hopTimer -= Time.deltaTime;
            if (_hopTimer <= 0f) _hopTimer = Random.Range(3f, 8f);
            if (_hopTimer > 0f && _hopTimer < 0.3f)
                y += Mathf.Sin((0.3f - _hopTimer) / 0.3f * Mathf.PI) * 16f; // 폴짝

            float x = 0f;
            if (_lungeTime < 1f)
            {
                _lungeTime += Time.deltaTime / 0.3f;
                x = Mathf.Sin(Mathf.Clamp01(_lungeTime) * Mathf.PI) * 60f;
            }
            _rect.anchoredPosition = _base + new Vector2(x, y);
        }
    }
}
