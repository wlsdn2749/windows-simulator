namespace WSGameServer;

/// <summary>
/// 로그 심각도. 값이 클수록 심각하며 <see cref="ServerLog.MinLevel"/>과 크기로 비교한다.
/// <see cref="None"/>은 "아무것도 찍지 않음"을 나타내는 하한 전용 값이라 로그 호출에는 쓰지 않는다.
/// </summary>
public enum LogLevel
{
    /// <summary>진단용 상세 기록(GC 수거 등). 평소에는 꺼 둔다.</summary>
    Trace = 0,

    /// <summary>패킷 송수신처럼 양이 많지만 개발 중에는 봐야 하는 기록.</summary>
    Debug,

    /// <summary>서버 시작·유저 접속처럼 정상 흐름에서 남길 사건.</summary>
    Info,

    /// <summary>동작은 계속되지만 사람이 확인해야 하는 상태(설정 이상·데이터 누락).</summary>
    Warn,

    /// <summary>처리에 실패한 사건. 예외를 함께 남긴다.</summary>
    Error,

    /// <summary>출력 하한 전용 — 모든 로그를 끈다.</summary>
    None,
}

/// <summary>
/// 서버 로그. <b>시각 · 레벨 · 스레드 · 분류</b>를 한 줄에 담는다.
///
/// <para>
/// 프레임워크(<c>MikaNetwork.Lib</c>)가 아니라 서버 쪽에 두는 이유는, Lib이 Unity로 손복사되는
/// 계층이라 <c>Console.WriteLine</c>이 Unity 콘솔에 뜨지 않기 때문이다. 프레임워크·프로토콜 계층은
/// 로그 훅만 뚫어 두고(<c>MikaPacketManager.Dispatching</c> 등) 어디에 어떻게 찍을지는 호스트가 정한다.
/// </para>
///
/// <para>
/// <b>스레드가 핵심 정보다.</b> 게임 로직은 단일 <c>LogicThread</c>에서만 돌아야 하고, DB는 스레드풀에서
/// 돌아야 한다. 로그에 스레드가 찍혀 있으면 그 규칙이 깨진 순간을 바로 알아볼 수 있다.
/// </para>
/// </summary>
public static class ServerLog
{
    /// <summary>출력 하한. 이 레벨 미만은 찍지 않는다. 개발 중이라 패킷까지 보이는 Debug로 둔다.</summary>
    public static LogLevel MinLevel = LogLevel.Debug;

    /// <summary>
    /// 콘솔 색상과 줄 출력을 함께 묶는 락.
    /// 색상은 콘솔 전역 상태라, 락 없이 만지면 다른 스레드가 찍는 줄에 색이 묻는다.
    /// </summary>
    private static readonly object Gate = new();

    /// <summary>
    /// 로그가 한글이라 <b>출력 인코딩을 UTF-8로 고정</b>한다. Windows 콘솔 기본 코드페이지(949)로는
    /// 로그가 통째로 깨져 읽을 수 없다 — 로그를 남기는 쪽이 읽히는 것까지 책임진다.
    /// </summary>
    static ServerLog()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // 출력이 파일·파이프로 리다이렉트돼 있으면 인코딩을 못 바꾼다. 로그는 계속 남겨야 하므로 무시한다.
        }
    }

    public static void Trace(string category, string message) => Write(LogLevel.Trace, category, message);
    public static void Debug(string category, string message) => Write(LogLevel.Debug, category, message);
    public static void Info (string category, string message) => Write(LogLevel.Info,  category, message);
    public static void Warn (string category, string message) => Write(LogLevel.Warn,  category, message);

    /// <summary>실패 기록. 예외를 넘기면 스택까지 다음 줄에 이어 붙인다.</summary>
    public static void Error(string category, string message, Exception? e = null)
        => Write(LogLevel.Error, category, e is null ? message : $"{message}{Environment.NewLine}{e}");

    private static void Write(LogLevel level, string category, string message)
    {
        if (level < MinLevel)
            return;

        // 시각은 로컬시각이다. 게임 로직은 UTC로 계산하지만, 로그는 사람이 손목시계와 대조하는 물건이라
        // UTC로 찍으면 "몇 초 지났나"를 볼 때마다 시차를 빼야 한다.
        // 스레드 칸을 고정폭으로 맞춰 둬야 여러 줄이 이어질 때 분류·메시지가 세로로 정렬된다.
        var line = $"{DateTime.Now:HH:mm:ss.fff} {Tag(level)} [{ThreadLabel(),-12}] [{category}] {message}";

        lock (Gate)
        {
            var previous = Console.ForegroundColor;
            var color = Color(level);

            if (color is not null)
                Console.ForegroundColor = color.Value;

            Console.WriteLine(line);

            if (color is not null)
                Console.ForegroundColor = previous;
        }
    }

    private static string Tag(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Info  => "INF",
        LogLevel.Warn  => "WRN",
        LogLevel.Error => "ERR",
        _              => "???",
    };

    private static ConsoleColor? Color(LogLevel level) => level switch
    {
        LogLevel.Trace => ConsoleColor.DarkGray,
        LogLevel.Debug => ConsoleColor.Gray,
        LogLevel.Warn  => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        _              => null,   // Info는 기본색 그대로 둔다
    };

    /// <summary>
    /// 지금 스레드를 사람이 읽을 수 있는 이름으로.
    ///
    /// <para>
    /// <b>스레드풀은 이름보다 번호를 먼저 본다.</b> .NET은 풀 스레드에 전부 <c>.NET TP Worker</c>라는
    /// 같은 이름을 붙여 두는데, 그대로 찍으면 여러 DB 스레드가 한 스레드처럼 보여 오히려 오해를 부른다.
    /// 이름이 뜻을 갖는 것은 우리가 직접 붙인 스레드(<c>LogicThread</c>)뿐이다.
    /// </para>
    /// </summary>
    private static string ThreadLabel()
    {
        var thread = Thread.CurrentThread;

        if (thread.IsThreadPoolThread)
            return $"Pool#{thread.ManagedThreadId}";

        return string.IsNullOrEmpty(thread.Name)
            ? $"T#{thread.ManagedThreadId}"
            : thread.Name!;
    }
}
