using MikaNetwork.Server;
using GameData;

namespace WSGameServer;

class Program
{
    private static async Task Main(string[] args)
    {
        var gameServer = new GameServer();
        gameServer.Initialize();
        await gameServer.Run();
        
        WarnIfTuned();

        ServerLog.Info("서버", "10050 포트에서 대기 중...");
        ServerLog.Info("서버", "종료하려면 엔터를 누르세요.");
        Console.ReadLine();
    }

    /// <summary>
    /// 확인용 설정이 켜진 채로 돌고 있으면 시작할 때 경고한다.
    /// 전역 배수를 올려 둔 걸 잊고 배포하면 재화 산출량이 통째로 어긋난다.
    /// </summary>
    private static void WarnIfTuned()
    {
        const double baseCycle = WorkStationSlot.BaseCycleSeconds;

        if (Math.Abs(GatherSpeedMultiplier - 1.0) < 0.0001)
        {
            ServerLog.Info("설정", $"채취 전역 배수 1.0배 — 기준 주기 {baseCycle:F1}초");
            return;
        }

        ServerLog.Warn("설정",
            $"채취 전역 배수 {GatherSpeedMultiplier:F1}배 — " +
            $"기준 주기 {baseCycle:F1}초 → {baseCycle / GatherSpeedMultiplier:F1}초 (확인용 설정)");
    }
}

