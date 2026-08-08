using GameData;
using MikaProtocol;

namespace WSGameServer.Tests.ProtocolTests;

/// <summary>
/// 프로토콜 <see cref="EGlobalRarity"/>와 엑셀 생성 <see cref="GlobalRarity"/>의
/// <b>이름·값 1:1</b>을 지킨다. GachaService.RarityOf가 byte 캐스팅으로 값을 그대로
/// 옮기므로, 엑셀에서 등급을 추가·재배열하면 컴파일은 통과한 채
/// 클라이언트 연출 등급만 조용히 어긋난다 — 이 테스트가 그 드리프트를 잡는다.
/// </summary>
public class PacketEnumTest
{
    [Fact]
    public void 프로토콜_등급은_GameData_등급과_이름_값이_1대1이다()
    {
        var protocol = Enum.GetValues<EGlobalRarity>()
            .Select(v => $"{v}={(byte)v}");

        // Max는 개수 셈용 센티널 — 실제 등급이 아니라 와이어로 나가지 않는다.
        var gameData = Enum.GetValues<GlobalRarity>()
            .Where(v => v != GlobalRarity.Max)
            .Select(v => $"{v}={(byte)v}");

        protocol.ShouldBe(gameData);
    }
}
