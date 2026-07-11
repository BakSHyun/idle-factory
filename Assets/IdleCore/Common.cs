using System;

namespace IdleCore
{
    /// <summary>모든 시간 판정(오프라인 보상, 출석, 이벤트 기간)은 이 인터페이스를 경유한다.</summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>테스트/시뮬레이터용 수동 시계.</summary>
    public sealed class ManualClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public void Advance(TimeSpan span) => UtcNow += span;
    }

    /// <summary>가챠·드랍은 시드 주입 가능한 RNG만 사용한다 (결정론 보장).</summary>
    public interface IRng
    {
        /// <returns>[0, 1) 구간의 난수</returns>
        double NextDouble();
        int NextInt(int minInclusive, int maxExclusive);
    }

    public sealed class SeededRng : IRng
    {
        private readonly Random _random;
        public SeededRng(int seed) => _random = new Random(seed);
        public double NextDouble() => _random.NextDouble();
        public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }
}
