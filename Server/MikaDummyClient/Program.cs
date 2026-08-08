using System.Threading.Tasks;

namespace MikaDummyClient
{
    class Program
    {
        private static async Task Main(string[] args)
        {
            await NetworkManager.Instance.Initialize();

            // 보낼 패킷을 번호로 선택하고 필드를 입력받아 송신하는 메뉴 루프
            new PacketMenu().Run();
        }
    }
}
