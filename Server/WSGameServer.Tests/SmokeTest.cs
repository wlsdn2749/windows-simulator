namespace WSGameServer;

/// <summary>
/// 테스트 인프라 자체가 살아 있는지 확인하는 스모크 테스트.
/// 서버 로직을 검증하지 않는다 — xUnit 발견 · Shouldly 단언 · Moq 목 생성이
/// 모두 동작하는지만 본다. 실제 테스트를 여기에 덧붙이지 말고 새 파일로 나눈다.
/// </summary>
public class SmokeTest
{
    /// <summary>Moq 동작 확인용 더미 인터페이스. 서버 코드와 무관하다.</summary>
    public interface IDummyClock
    {
        DateTimeOffset UtcNow { get; }
    }

    [Fact]
    public void xUnit이_테스트를_발견하고_실행한다()
    {
        var sum = 1 + 1;

        sum.ShouldBe(2);
    }

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(2, 3, 5)]
    [InlineData(-1, 1, 0)]
    public void Theory_인자가_주입된다(int left, int right, int expected)
    {
        (left + right).ShouldBe(expected);
    }

    [Fact]
    public void Shouldly_단언이_동작한다()
    {
        var items = new[] { "붕어", "잉어", "향어" };

        items.ShouldNotBeEmpty();
        items.ShouldContain("잉어");
        items.Length.ShouldBe(3);

        // 예외 단언도 확인해 둔다. 서버 로직 테스트에서 자주 쓴다.
        Should.Throw<InvalidOperationException>(() => throw new InvalidOperationException("의도된 예외"));
    }

    [Fact]
    public void Moq가_인터페이스를_대체한다()
    {
        // 시각처럼 테스트가 제어해야 하는 의존성을 목으로 고정하는 패턴.
        // 채취 정산(경과시간 기반)을 테스트할 때 이 형태를 그대로 쓴다.
        var fixedNow = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

        var clock = new Mock<IDummyClock>();
        clock.Setup(c => c.UtcNow).Returns(fixedNow);

        clock.Object.UtcNow.ShouldBe(fixedNow);
        clock.Verify(c => c.UtcNow, Times.Once);
    }
}
