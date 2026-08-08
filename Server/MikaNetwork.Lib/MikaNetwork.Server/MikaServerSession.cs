using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using MikaNetwork;

namespace MikaNetwork.Server;

public sealed class MikaServerSession : ISession, IHeartbeatSession
{
    /// <summary>
    /// _sequenceKey와 CreateSessionId()를 통해 Unique한 SessionId를 발급
    /// </summary>
    private readonly Socket                             _socket;
    private readonly Channel<ReadOnlyMemory<byte>>      _sendQueue;
    private readonly MikaRecvBuffer                     _recvBuffer;
    private readonly CancellationTokenSource            _cts;

    private const int SendQueueCapacity = 1024;
    private const int MaxRecvBufferSize = ushort.MaxValue;
    private const int RecvChunkSize     = 4096;
    
    public EndPoint? RemoteEndPoint => _socket.RemoteEndPoint;
    public long SessionId { get; init; }
    public bool IsConnected { get; private set; } = false;

    // Disconnect가 여러 스레드에서 동시에 들어올 수 있다(수신 루프 종료·송신 실패·유휴 스윕).
    // bool 검사-후-대입으로는 두 스레드가 함께 통과해 Disconnected가 2회 발화한다 —
    // 호스트 쪽에서는 그것이 종료 정산 중복이 된다.
    private int _disconnected;

    /// <summary>
    /// 마지막으로 <b>바이트를 받은</b> 시각(UTC). 유휴 판정의 유일한 근거다.
    /// 접속 시점을 초기값으로 두어, 한 번도 못 받은 세션도 접속 시각부터 재게 한다.
    /// </summary>
    public DateTime LastReceivedAt { get; private set; } = DateTime.UtcNow;


    public event Func<ISession, ReadOnlyMemory<byte>, ValueTask>? Received;
    public event Action<ISession>? Disconnected;
    public event Action<ISession>? Connected;
    
    public MikaServerSession(Socket socket, long sessionId)
    {
        _socket = socket;
        _sendQueue = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(SendQueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });
        _recvBuffer = new MikaRecvBuffer(MaxRecvBufferSize);
        _cts = new CancellationTokenSource();
        SessionId = sessionId;
    }


    public void Send(ReadOnlyMemory<byte> data)
    {
        if(!_sendQueue.Writer.TryWrite(data)) // 송신 Queue에 넣는데 실패하면, 연결 끊음 
            Disconnect(); 
    }


    public void Disconnect()
    {
        // 정확히 한 번만 통과한다. 이 가드가 없으면 Disconnected 이벤트가 두 번 발화해
        // 호스트의 세션 정리(종료 정산 포함)가 중복된다.
        if (Interlocked.Exchange(ref _disconnected, 1) == 1)
            return;

        IsConnected = false;

        try
        {
            _socket?.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        
        Disconnected?.Invoke(this);
        Dispose();
    }
    
    public void Dispose()
    {
        _socket.Dispose();
        _cts.Cancel();
    }
    //
    //

    public async Task StartAsync()
    {
        if (IsConnected)
            return;
        
        IsConnected = true;
        Connected?.Invoke(this);
        
        try
        {
            await Task.WhenAll(ReceiveLoop(), SendLoop());
        }
        finally
        {
            Disconnect();
        }
    }

    public async Task OnReceived(ReadOnlyMemory<byte> data)
    {
        var handler = Received;

        if (handler is null) return;
        
        await handler(this, data);
    }
    

    private async Task ReceiveLoop()
    {
        try
        {
            while (IsConnected)
            {
                // 여기서 recvBuffer에 직접쓸 공간 가져오기 새 할당 XX
                var buffer =_recvBuffer.GetWritableMemory(RecvChunkSize);
                int received = await _socket.ReceiveAsync(buffer); // 연결 끊기면 예외 throw

                if (received == 0)
                {
                    break;
                }

                // 바이트가 왔다 = 연결이 살아 있다. 무엇을 받았는지는 보지 않는다 —
                // 하트비트든 일반 요청이든 유휴 판정에는 똑같은 증거다.
                LastReceivedAt = DateTime.UtcNow;

                _recvBuffer.AdvanceWrite(received);
                while (_recvBuffer.ReadableBytes >= MikaPacketBuilder.HeaderSize) // PacketHeader
                {
                    var size = MikaPacketBuilder.ReadSize(_recvBuffer.GetReadableSpan());

                    // 읽은 사이즈가 Header보다 작거나, 패킷 사이즈보다 클 경우 차단
                    if (size < MikaPacketBuilder.HeaderSize || size > MikaPacketBuilder.MaxPacketSize)
                    {
                        return;
                    }

                    if (size <= _recvBuffer.ReadableBytes)
                    {
                        var data = MikaPacketBuilder.ReadPacket(_recvBuffer.GetReadableSpan(), size);
                        _recvBuffer.AdvanceRead(size);
                        await OnReceived(data);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception)
        {
            // 강제 종료(RST)·소켓 오류 등 → finally에서 Disconnect 처리
        }
        finally
        {
            Disconnect();
        }
    }

    private async Task SendLoop()
    {
        try
        {
            while (await _sendQueue.Reader.WaitToReadAsync(_cts.Token))
            {
                while (_sendQueue.Reader.TryRead(out var data))
                {
                    if (!IsConnected) return;

                    await _socket.SendAsync(data, SocketFlags.None);
                }
            }
        }
        catch (Exception)
        {
            Disconnect();
        }
    }


    
    
}