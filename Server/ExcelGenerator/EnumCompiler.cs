using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ExcelGenerator;

/// <summary>
/// 생성한 enum 소스 문자열을 Roslyn으로 즉석 컴파일해 인메모리 어셈블리로 로드한다.
/// 이후 리플렉션으로 특정 enum·멤버의 존재 여부를 조회할 수 있다.
/// </summary>
public sealed class EnumCompiler
{
    public Assembly Compile(string source, string assemblyName = "GeneratedEnums")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // enum만 담긴 코드라 참조는 코어 라이브러리(System.Runtime/Private.CoreLib)면 충분하다.
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            
            throw new InvalidOperationException("생성한 enum 소스 컴파일 실패:\n" + string.Join("\n", errors));
        }

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }

    /// <summary>지정한 enum 타입에 해당 멤버 이름이 존재하는지 조회한다.</summary>
    public bool HasMember(Assembly assembly, string enumName, string memberName)
    {
        var type = assembly.GetTypes().FirstOrDefault(t => t.IsEnum && t.Name == enumName);
        if (type is null)
            return false;

        return Enum.GetNames(type).Contains(memberName);
    }
    
    
}
