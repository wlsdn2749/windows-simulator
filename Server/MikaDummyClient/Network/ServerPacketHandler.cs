using System;
using MikaNetwork;
using MikaProtocol;

namespace MikaDummyClient
{
    public static class ServerPacketHandler
    {
        [PacketHandler]
        public static void Handle_S_EchoResponse(ISession session, S_EchoResponse res)
        {
            Console.WriteLine($"[Client] Recv Echo: {res.Message}");
        }

        [PacketHandler]
        public static void Handle_S_PongResponse(ISession session, S_PongResponse res)
        {
            Console.WriteLine("[Client] Recv Pong");
        }

        [PacketHandler]
        public static void Handle_S_LoginResponse(ISession session, S_LoginResponse res)
        {
            Console.WriteLine($"[Client] Recv Login: Result={res.Result}, SessionId={res.SessionId}");
        }

        [PacketHandler]
        public static void Handle_S_UpdateItemResponse(ISession session, S_UpdateItemResponse res)
        {
            Console.WriteLine($"[Client] Recv UpdateItem: Count={res.ItemChangeInfos?.Count}");
            foreach (var item in res.ItemChangeInfos!)
                Console.WriteLine($"  - Kind={item.Kind.ToString()}, ItemId={item.ItemId}, Count={item.Count}");
        }

        [PacketHandler]
        public static void Handle_S_InventoryResponse(ISession session, S_InventoryResponse res)
        {
            Console.WriteLine($"[Client] Recv Inventory: Count={res.Items?.Count}");
            foreach (var item in res.Items!)
                Console.WriteLine($"  - ItemId={item.ItemId}, Count={item.Count}");
        }

        [PacketHandler]
        public static void Handle_S_GachaDrawResponse(ISession session, S_GachaDrawResponse res)
        {
            if (res.Result != EResultCode.Ok)
            {
                Console.WriteLine($"[Client] Recv Gacha: 실패 Result={res.Result}");
                return;
            }

            Console.WriteLine($"[Client] Recv Gacha: Count={res.Rewards?.Count}");
            foreach (var reward in res.Rewards!)
                Console.WriteLine($"  - Rarity={reward.Rarity.ToString()}, ItemId={reward.ItemId}, Count={reward.Count}");

            Console.WriteLine($"[Client] Recv Gacha 인벤토리 변경: Count={res.ItemChangeInfos?.Count}");
            foreach (var change in res.ItemChangeInfos!)
                Console.WriteLine($"  - Kind={change.Kind.ToString()}, ItemId={change.ItemId}, Count={change.Count} (누적 총량)");
        }

        [PacketHandler]
        public static void Handle_S_WorkStationSlotsResponse(ISession session, S_WorkStationSlotsResponse res)
        {
            Console.WriteLine($"[Client] Recv 작업슬롯: Count={res.Slots?.Count}");
            foreach (var slot in res.Slots!)
                Console.WriteLine($"  - Slot={slot.SlotIndex}, Industry={slot.Industry}, " +
                                  $"Character={slot.CharacterId}, LastTick={slot.LastTickAtUnix}");
        }

        [PacketHandler]
        public static void Handle_S_WorkStationAssignResponse(ISession session, S_WorkStationAssignResponse res)
        {
            if (res.Result != EResultCode.Ok)
            {
                Console.WriteLine($"[Client] Recv 슬롯 배치: 실패 Result={res.Result}");
                return;
            }

            Console.WriteLine($"[Client] Recv 슬롯 배치: Slot={res.Slot?.SlotIndex}, " +
                              $"Industry={res.Slot?.Industry}, Character={res.Slot?.CharacterId}");
        }

        // 서버가 요청 없이 밀어 주는 채취 결과. 클라이언트는 받기만 한다(서버 권위).
        [PacketHandler]
        public static void Handle_S_GatherResultResponse(ISession session, S_GatherResultResponse res)
        {
            Console.WriteLine($"[Client] Recv 채취: Slot={res.SlotIndex}, 판정={res.JudgeCount}회");
            foreach (var change in res.ItemChanges!)
                Console.WriteLine($"  - ItemId={change.ItemId}, Count={change.Count}, Kind={change.Kind}");
        }

        [PacketHandler]
        public static void Handle_S_CharacterListResponse(ISession session, S_CharacterListResponse res)
        {
            Console.WriteLine($"[Client] Recv 캐릭터: Count={res.Characters?.Count}");
            foreach (var character in res.Characters!)
                Console.WriteLine($"  - Id={character.CharacterId}, Tid={character.CharacterTid}, " +
                                  $"Lv={character.Level}, Exp={character.Exp}");
        }

        // 로그인 스냅샷과 변경 푸시가 같은 패킷으로 온다.
        // Amount가 증감이 아니라 확정 잔액이라 두 경우 모두 덮어쓰기로 처리하면 된다.
        [PacketHandler]
        public static void Handle_S_CurrencyResponse(ISession session, S_CurrencyResponse res)
        {
            Console.WriteLine($"[Client] Recv 재화: Count={res.Currencies?.Count}");
            foreach (var currency in res.Currencies!)
                Console.WriteLine($"  - Type={currency.CurrencyType}, Amount={currency.Amount}");
        }
    }
}

