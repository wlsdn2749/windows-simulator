using System;
using MemoryPack;
using MikaNetwork;

/// <summary>
/// Core(MikaSession/MikaClient)는 byte[]만 다루는 순수 전송 계층이라 IPacket을 모른다.
/// 객체 → [id][size][body] 직렬화/프레이밍은 약속(Protocol) 계층의 책임이므로
/// 여기서 확장 메서드로 얹는다. (Protocol → Core 단방향 의존)
/// </summary>

namespace MikaProtocol
{
    public static class MikaSessionPacketExtensions
    {
        /// <summary>
        /// 송신 직후에 불린다. 인자는 (세션, 패킷 id, 프레임 전체 크기).
        ///
        /// <para>
        /// Protocol 계층은 로거를 모른다 — 어디에 어떻게 찍을지는 호스트가 정한다.
        /// 서버는 콘솔 로그로, Unity는 Debug.Log로 각각 채우면 된다. 채우지 않으면 아무 일도 없다.
        /// </para>
        /// </summary>
        public static Action<ISession, ushort, int>? Sent;

        public static void SendPacket<T>(this ISession session, T packet) where T : IPacket
        {
            ushort id = MikaGenerated.GeneratedPacketIds.Get<T>();
            byte[] body = MemoryPackSerializer.Serialize(packet);            // body 직렬화
            byte[] framed = MikaPacketBuilder.MakePacket(id, body);          // [id][size][body]
            session.Send(framed);                                            // Core의 byte[] Send 재사용

            Sent?.Invoke(session, id, framed.Length);
        }
    }
}
