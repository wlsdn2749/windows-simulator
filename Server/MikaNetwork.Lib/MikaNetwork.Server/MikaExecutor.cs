using System;
using System.Threading.Channels;
using MikaUtils;

namespace MikaNetwork.Server
{
    public interface ILogicExecutor
    {
        public void Start();
        public void Stop();
        public void Post(Action job);
    }

    /// <summary>
    /// DB 작업 전용 실행기. sessionId로 파티션을 고정해
    /// "같은 유저는 직렬, 다른 유저는 병렬"로 처리한다.
    /// 게임 상태 변경은 하지 않는다 — 결과는 LogicExecutor로 되돌린다.
    /// </summary>
    public sealed class DBExecutor : Singleton<DBExecutor>
    {
        private Channel<Func<Task>>[]? _dbChannels;
        private int _channelCount;

        public void Start(int channelCount = 0)
        {
            _channelCount = channelCount;
            
            _dbChannels = new Channel<Func<Task>>[_channelCount];

            for (int i = 0; i < _channelCount; i++)
            {
                _dbChannels[i] = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false });

                _ = RunAsync(_dbChannels[i].Reader);
            }
        }

        public void Post(long id, Func<Task> dbJob)
        {
            int idx = (int)((ulong)id % (ulong)_channelCount);

            _dbChannels?[idx].Writer.TryWrite(dbJob);
        }

        private async Task RunAsync(ChannelReader<Func<Task>> reader)
        {
            await foreach (var job in reader.ReadAllAsync())
            {
                try
                {
                    await job();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[DBExecutor] job 예외: {e}");
                }
            }
        }

        public void Stop()
        {
            if (_dbChannels == null) return;
            foreach (var channel in _dbChannels)
            {
                channel.Writer.TryComplete();
            }
        }
    }

    // SingleThread LogicExecutor 
    public sealed class LogicExecutor : ILogicExecutor
    {
        private readonly Channel<Action> _queue = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }); // MultiProducer-SingleConsumer
        private readonly Thread _thread;

        public LogicExecutor()
        {
            _thread = new Thread(Run) { IsBackground = true, Name = "LogicThread" };
        }


        public void Start()
        {
            _thread.Start();
        }

        public void Post(Action job) => _queue.Writer.TryWrite(job);

        private void Run()
        {
            foreach(var job in _queue.Reader.ReadAllAsync().ToBlockingEnumerable())
            {
                try
                {
                    // 잡마다 스레드 번호를 찍던 줄은 제거했다. 이제 패킷 로그가 시각·스레드·패킷명을
                    // 함께 남기므로(MikaPacketManager.Dispatching 훅), 여기서는 아무것도 찍지 않는다.
                    job.Invoke();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[LogicExecutor] job 예외: {e}");
                }
            }
        }

        public void Stop()
        {
            _queue.Writer.TryComplete();
            _thread.Join();
        }
        
    }
}