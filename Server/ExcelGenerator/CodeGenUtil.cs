using System.Text;

namespace ExcelGenerator;

/// <summary>
/// 생성 코드의 파일 형식(using + 네임스페이스)을 만드는 공용 헬퍼.
///
/// 생성물은 Unity로 미러링되고 Unity는 기본 C# 9이므로,
/// file-scoped namespace(C# 10)를 쓸 수 없다 → 반드시 블록 네임스페이스로 감싼다.
/// 생성기마다 직접 "namespace X;"를 쓰면 이 제약을 놓치기 쉬우므로 여기 한 곳으로 모았다.
///
/// ⚠️ 이 파일 자체(ExcelGenerator 프로젝트)는 미러 대상이 아니므로 최신 문법을 써도 된다.
///    제약은 "생성되어 나가는 문자열"에만 적용된다.
/// </summary>
internal static class CodeGenUtil
{
    private const string Indent = "    ";

    /// <summary>
    /// using 목록과 본문을 받아 블록 네임스페이스로 감싼 파일 소스를 만든다.
    /// 본문은 한 단계 들여쓰기가 적용된다(호출부에서 미리 들여쓸 필요 없음).
    /// </summary>
    internal static string BuildFile(string ns, string[] usings, string body)
    {
        var sb = new StringBuilder();

        if (usings.Length > 0)
        {
            foreach (var u in usings)
                sb.AppendLine($"using {u};");
            sb.AppendLine();
        }

        sb.AppendLine($"namespace {ns}");
        sb.AppendLine("{");
        AppendIndented(sb, body);
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>본문 각 줄을 한 단계 들여쓴다. 빈 줄에는 공백을 남기지 않는다(trailing whitespace 방지).</summary>
    private static void AppendIndented(StringBuilder sb, string body)
    {
        // 개행을 \n으로 통일한 뒤 끝쪽 빈 줄을 떼어낸다(닫는 중괄호 앞에 빈 줄이 남지 않도록).
        var lines = body.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        foreach (var line in lines)
            sb.AppendLine(line.Length == 0 ? string.Empty : Indent + line);
    }
}
