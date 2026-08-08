using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MikaNetwork
{
    public class MikaClientSession : ISession
    {
        private readonly Socket                     _socket;
        private readonly MikaSendQueue              _sendQueue;
        private readonly MikaRecvBuffer             _recvBuffer;
        private readonly CancellationTokenSource    _cts;
        
        private const int SendQueueCapacity = 1024;
        private const int MaxRecvBufferSize = ushort.MaxValue;
        private const int RecvChunkSize     = 4096;
        
        public long SessionId { get; }
        public EndPoint? RemoteEndPoint => _socket.RemoteEndPoint;
        public bool IsConnected { get; private set; } = false;
        
        public event Func<ISession, ReadOnlyMemory<byte>, ValueTask>? Received;
        public event Action<ISession>? Connected;
        public event Action<ISession>? Disconnected;

        public MikaClientSession(Socket socket)
        {
            _socket = socket;
            _sendQueue = new MikaSendQueue();
            _recvBuffer = new MikaRecvBuffer(MaxRecvBufferSize);
            _cts = new CancellationTokenSource();
        }
        
        public void Send(ReadOnlyMemory<byte> data)
        {
            if(!_sendQueue.TryWrite(data))
                Disconnect();
        }

        public void Disconnect()
        {
            if (!IsConnected)
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
            while (IsConnected)
            {
                // 여기서 recvBuffer에 직접쓸 공간 가져오기 새 할당 XX
                var buffer =_recvBuffer.GetWritableMemory(RecvChunkSize);
                int received = await _socket.ReceiveAsync(buffer, SocketFlags.None);
                
                if (received == 0)
                {
                    break; 
                }
                
                _recvBuffer.AdvanceWrite(received);
                while (_recvBuffer.ReadableBytes >= MikaPacketBuilder.HeaderSize) // PacketHeader
                {
                    var size = MikaPacketBuilder.ReadSize(_recvBuffer.GetReadableSpan());
                    
                    // 읽은 사이즈가 Header보다 작거나, 패킷 사이즈보다 클 경우 차단
                    if (size < MikaPacketBuilder.HeaderSize || size > MikaPacketBuilder.MaxPacketSize)
                    {
                        Disconnect();
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
            
            Disconnect();
        }

        private async Task SendLoop()
        {
            try
            {
                while (IsConnected)
                {
                    await _sendQueue.WaitToReadAsync(_cts.Token);
                    while (_sendQueue.TryRead(out var data))
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
}