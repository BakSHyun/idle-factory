using System;
using System.Collections.Generic;

namespace IdleCore.Economy
{
    public enum PassKeyType
    {
        /// <summary>스테이지 인덱스 연동 (상시 진행도 패스)</summary>
        StageIndex,
        /// <summary>포인트 적립 연동 (시즌/이벤트 패스 — 미션이 포인트를 준다)</summary>
        Points,
    }

    public sealed class PassRewardStep
    {
        /// <summary>이 키 값 도달 시 수령 가능 (스테이지 인덱스 또는 포인트)</summary>
        public long atKey;
        public List<CurrencyGrant> freeRewards = new List<CurrencyGrant>();
        public List<CurrencyGrant> paidRewards = new List<CurrencyGrant>();
    }

    /// <summary>
    /// 패스 정의 — 진행도 패스(상시), 시즌 패스(월간 로테이션), 이벤트 패스가 전부 이 하나로 표현된다.
    /// 차이는 keyType과 (시즌/이벤트의 경우) 유효 기간뿐.
    /// </summary>
    public sealed class PassDef
    {
        public string id;
        public string name;
        public PassKeyType keyType = PassKeyType.StageIndex;
        /// <summary>유료 트랙 해금 가격 (하드커런시, 가격 사다리 준수)</summary>
        public long unlockPrice = 990;
        public List<PassRewardStep> steps = new List<PassRewardStep>();
        /// <summary>시즌/이벤트 패스의 기간 (null이면 상시)</summary>
        public DateTime? startUtc;
        public DateTime? endUtc;
    }

    public sealed class PassState
    {
        public string defId;
        public bool paidUnlocked;
        public long points;                 // Points 타입일 때 적립치
        public int freeClaimedSteps;        // 앞에서부터 수령한 단계 수
        public int paidClaimedSteps;
    }

    /// <summary>패스 1개의 상태 머신. 여러 패스가 동시 가동된다(소울헌터: 8종+).</summary>
    public sealed class PassSystem
    {
        private readonly PassDef _def;
        private readonly Wallet _wallet;
        private readonly IClock _clock;

        public PassState State { get; }

        public PassSystem(PassDef def, Wallet wallet, IClock clock, PassState state = null)
        {
            _def = def;
            _wallet = wallet;
            _clock = clock;
            State = state ?? new PassState { defId = def.id };
        }

        public PassDef Def => _def;

        public bool IsWithinPeriod()
        {
            var now = _clock.UtcNow;
            if (_def.startUtc.HasValue && now < _def.startUtc.Value) return false;
            if (_def.endUtc.HasValue && now >= _def.endUtc.Value) return false;
            return true;
        }

        public bool TryUnlockPaid()
        {
            if (State.paidUnlocked || !IsWithinPeriod()) return false;
            if (!_wallet.TrySpend(CurrencyIds.GemHard, _def.unlockPrice)) return false;
            State.paidUnlocked = true;
            return true;
        }

        public void AddPoints(long points)
        {
            if (_def.keyType != PassKeyType.Points) throw new InvalidOperationException("not a points pass");
            State.points += points;
        }

        private long CurrentKey(int currentStageIndex) =>
            _def.keyType == PassKeyType.StageIndex ? currentStageIndex : State.points;

        /// <summary>수령 가능한 보상을 전부 수령하고 지급 내역을 반환한다.</summary>
        public List<CurrencyGrant> ClaimAll(int currentStageIndex)
        {
            var granted = new List<CurrencyGrant>();
            if (!IsWithinPeriod()) return granted;
            long key = CurrentKey(currentStageIndex);

            for (int i = State.freeClaimedSteps; i < _def.steps.Count && _def.steps[i].atKey <= key; i++)
            {
                foreach (var g in _def.steps[i].freeRewards) { _wallet.Earn(g.currency, g.amount); granted.Add(g); }
                State.freeClaimedSteps = i + 1;
            }
            if (State.paidUnlocked)
            {
                for (int i = State.paidClaimedSteps; i < _def.steps.Count && _def.steps[i].atKey <= key; i++)
                {
                    foreach (var g in _def.steps[i].paidRewards) { _wallet.Earn(g.currency, g.amount); granted.Add(g); }
                    State.paidClaimedSteps = i + 1;
                }
            }
            return granted;
        }
    }
}
