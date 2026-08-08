using System;
using System.Collections.Generic;
using System.Diagnostics;
using MemoryPack;

namespace MikaNetwork
{
    /// <summary>
    /// 수신 디스패처. PacketId → "그 타입으로 역직렬화 후 핸들러 호출"하는 어댑터를 등록해둔다.
    /// 등록 시점에만 구체 타입 T를 알면 되고, 수신부(OnRecvPacket)는 타입을 몰라도 된다.
    /// </summary>
    public class MikaPacketManager
    {
        /// <summary>
        /// 패킷이 핸들러로 넘어가기 <b>직전</b>에 불린다 — 즉 실제로 처리하는 스레드에서 실행된다.
        /// 인자는 (세션, 패킷 id, body 크기, 도착 시각 <see cref="Stopwatch"/> 타임스탬프).
        ///
        /// <para>
        /// 프레임워크는 게임 패킷 목록도 로거도 모른다. 이름을 붙이고 어디에 찍을지는 호스트가 정한다.
        /// 채우지 않으면 <b>아무 비용도 들지 않는다</b>(job을 감싸는 델리게이트조차 만들지 않는다).
        /// </para>
        /// </summary>
        public static Action<ISession, ushort, int, long>? Dispatching;

        /// <summary>
        /// 등록되지 않은 id를 받았을 때. 핸들러 누락·버전 불일치·알 수 없는 패킷이 여기로 온다.
        /// 채우지 않으면 조용히 버린다.
        /// </summary>
        public static Action<ISession, ushort>? UnknownReceived;

        private readonly Dictionary<ushort, Func<ISession, ReadOnlyMemory<byte>, Action>> _handlers = new();

        public void Register<T>(ushort id, Action<ISession, T> handler)
        {
            _handlers[id] = (session, body) =>
            {
                var packet = MemoryPackSerializer.Deserialize<T>(body.Span)!;

                return () => handler(session, packet); // 직렬화만 Network Thread가 하도록 고정
            };
        }

        // OnRecvCallback : 보통 Unity에서 바로 처리하지않고 PacketQueue로 넘어갈일이 있으면 사용
        public void OnRecvPacket(ISession session, ReadOnlyMemory<byte> data, Action<Action>? onRecvCallback = null)
        {
            if (data.Length < MikaPacketBuilder.HeaderSize) return;                       // 최소 헤더 크기

            ushort id   = MikaPacketBuilder.ReadId(data.Span);    // [0..2) = id
            var body = MikaPacketBuilder.ReadBody(data);                 // [4..]  = body (헤더 제외)

            if (!_handlers.TryGetValue(id, out var handler))
            {
                // 등록 안 된 id(핸들러 누락·버전 불일치·알 수 없는 패킷) -> silent drop 대신 호스트에 알린다
                UnknownReceived?.Invoke(session, id);
                return;
            }

            // 여기(네트워크 스레드)가 도착 시각이다. 실제 처리는 로직 스레드로 넘어가므로
            // 둘의 차이가 곧 로직 스레드의 큐 대기시간이 된다.
            var arrivedAt = Stopwatch.GetTimestamp();
            var job = handler(session, body);

            var dispatching = Dispatching;
            if (dispatching != null)
            {
                // 훅을 job 안으로 밀어 넣어야 "처리한 스레드"에서 찍힌다. 여기서 바로 부르면
                // 네트워크 스레드 이름만 남아 정작 알고 싶은 것(로직 스레드가 밀리는지)을 못 본다.
                var inner    = job;
                var bodySize = body.Length;
                job = () =>
                {
                    dispatching(session, id, bodySize, arrivedAt);
                    inner();
                };
            }

            if (onRecvCallback != null)
            {
                onRecvCallback.Invoke(job); // 실행을 다른 스레드로 이양
            }
            else
            {
                job.Invoke(); // NetworkThread에서 직접 실행
            }
        }
    }
}
