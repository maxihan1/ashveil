// 확률 판정에 쓰는 난수 공급자 추상화 (테스트에서 고정값 주입)
namespace Ashveil.Shared
{
    public interface IRandomSource
    {
        /// <summary>[0, 1) 범위의 실수.</summary>
        double NextDouble();

        /// <summary>[minInclusive, maxExclusive) 범위의 정수.</summary>
        int Next(int minInclusive, int maxExclusive);
    }

    /// <summary>System.Random 을 감싼 기본 구현. 스레드 안전하지 않으므로 호출 단위로 만들거나 잠근다.</summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly System.Random _random;

        public SystemRandomSource() : this(new System.Random()) { }

        public SystemRandomSource(System.Random random) { _random = random; }

        public double NextDouble() => _random.NextDouble();

        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }
}
