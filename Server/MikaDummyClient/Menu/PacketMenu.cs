using System;
using System.Collections.Generic;
using MikaProtocol;

namespace MikaDummyClient
{
    /// <summary>
    /// 보낼 패킷을 번호로 선택하고 필요한 필드를 입력받아 송신하는 메뉴 루프.
    /// 액션을 리스트에 등록하면 번호가 자동으로 부여된다(레지스트리 방식).
    /// 새 패킷을 시험하려면 <see cref="_actions"/>에 항목 하나만 추가하면 된다.
    /// </summary>
    public sealed class PacketMenu
    {
        private readonly List<ClientAction> _actions;

        public PacketMenu()
        {
            _actions = new List<ClientAction>
            {
                new ClientAction("Echo (채팅)", SendEcho),
                new ClientAction("Ping", SendPing),
                new ClientAction("Login", SendLogin),
                new ClientAction("AddItem", SendAddItem),
                new ClientAction("GachaDraw", SendGachaDraw),
                new ClientAction("WorkStationAssign (슬롯 배치)", SendWorkStationAssign),
            };
        }

        /// <summary>
        /// 메뉴를 반복 표시하며 번호 입력을 처리한다. 0을 입력하면 종료한다.
        /// </summary>
        public void Run()
        {
            while (true)
            {
                PrintMenu();

                Console.Write("Select > ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (!int.TryParse(input.Trim(), out int choice))
                {
                    Console.WriteLine("[Client] 숫자를 입력하세요.\n");
                    continue;
                }

                if (choice == 0)
                    break;

                if (choice < 1 || choice > _actions.Count)
                {
                    Console.WriteLine("[Client] 존재하지 않는 번호입니다.\n");
                    continue;
                }

                _actions[choice - 1].Execute();
                Console.WriteLine();
            }

            Console.WriteLine("[Client] 서버와 연결을 해제하고 종료합니다.");
        }

        private void PrintMenu()
        {
            Console.WriteLine("=== 보낼 패킷 선택 ===");
            for (int i = 0; i < _actions.Count; i++)
                Console.WriteLine($"{i + 1}) {_actions[i].Label}");
            Console.WriteLine("0) 종료");
        }

        // --- 각 패킷 액션 ---

        private void SendEcho()
        {
            Console.Write("보낼 메시지 > ");
            string message = Console.ReadLine() ?? "";
            NetworkManager.Instance.Send(new C_EchoRequest { Message = message });
        }

        private void SendPing()
        {
            NetworkManager.Instance.Send(new C_PingRequest());
        }

        private void SendLogin()
        {
            Console.Write("로그인 Id > ");
            string id = Console.ReadLine() ?? "";
            NetworkManager.Instance.Send(new C_LoginRequest { Id = id });
        }

        private void SendAddItem()
        {
            Console.Write("ItemId > ");
            if (!int.TryParse(Console.ReadLine(), out int itemId))
            {
                Console.WriteLine("[Client] ItemId는 숫자여야 합니다.");
                return;
            }

            Console.Write("Count > ");
            if (!int.TryParse(Console.ReadLine(), out int count))
            {
                Console.WriteLine("[Client] Count는 숫자여야 합니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_AddItemRequest { ItemId = itemId, Count = count });
        }

        private void SendWorkStationAssign()
        {
            Console.Write("SlotIndex (기본 0) > ");
            string? slotInput = Console.ReadLine();
            int slotIndex = string.IsNullOrWhiteSpace(slotInput) ? 0 : int.Parse(slotInput.Trim());

            // GameData.ItemType — 1=농사 2=낚시 3=채굴 4=벌목 5=사냥 (0=해제)
            Console.Write("Industry (2=낚시) > ");
            if (!byte.TryParse(Console.ReadLine(), out byte industry))
            {
                Console.WriteLine("[Client] Industry는 숫자여야 합니다.");
                return;
            }

            Console.Write("CharacterId (0=해제) > ");
            if (!long.TryParse(Console.ReadLine(), out long characterId))
            {
                Console.WriteLine("[Client] CharacterId는 숫자여야 합니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_WorkStationAssignRequest
            {
                SlotIndex = slotIndex, Industry = industry, CharacterId = characterId,
            });
        }

        private void SendGachaDraw()
        {
            Console.Write("GachaId (기본 1) > ");
            string? gachaInput = Console.ReadLine();
            int gachaId = string.IsNullOrWhiteSpace(gachaInput) ? 1 : int.Parse(gachaInput.Trim());

            Console.Write("DrawCount (1 또는 10) > ");
            if (!int.TryParse(Console.ReadLine(), out int drawCount))
            {
                Console.WriteLine("[Client] DrawCount는 숫자여야 합니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_GachaDrawRequest { GachaId = gachaId, DrawCount = drawCount });
        }
    }
}
