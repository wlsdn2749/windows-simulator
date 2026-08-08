using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ExcelGenerator;

/// <summary>
/// 생성한 테이블 코드(Enum/Row/Packer/TableRegistry/PackerUtil 등)를 Roslyn으로 즉석 컴파일한다.
/// EnumCompiler와 달리 (1) 여러 소스를 함께 묶고, (2) MemoryPack 소스 제너레이터를 함께 구동해
/// [MemoryPackable] Row의 직렬화 코드까지 생성한 뒤 인메모리 어셈블리로 로드한다.
/// 이 어셈블리의 TableRegistry를 리플렉션으로 호출하면 .bytes를 직렬화할 수 있다.
/// (별도 ExcelDataPacker 없이 ExcelGenerator 한 프로세스에서 코드+바이너리를 모두 만들기 위함)
/// </summary>
public static class TableCompiler
{
    /// <summary>출력 폴더에 함께 배치되는 MemoryPack 소스 제너레이터 DLL 이름(ExcelGenerator.csproj가 복사).</summary>
    private const string GeneratorDllName = "MemoryPack.Generator.dll";

    /// <summary>
    /// 주어진 .cs 소스 파일들을 MemoryPack 제너레이터와 함께 컴파일해 인메모리 어셈블리로 반환한다.
    /// </summary>
    /// <summary>
    /// SDK의 ImplicitUsings(enable)가 제공하는 표준 전역 using. 생성 코드가 System/List/Dictionary/Linq 등을
    /// 명시 using 없이 쓰므로, 인메모리 컴파일에도 동일하게 주입해야 한다.
    /// </summary>
    private const string ImplicitGlobalUsings = """
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
""";

    public static Assembly Compile(IEnumerable<string> sourcePaths, string assemblyName = "GeneratedTables")
    {
        // MemoryPack 제너레이터가 static abstract 인터페이스(net7+) 분기를 emit하므로,
        // SDK 빌드와 동일하게 NETx_0_OR_GREATER 전처리 심볼을 정의해야 net8 MemoryPack.Core와 시그니처가 맞는다.
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest).WithPreprocessorSymbols(
            "NET", "NETCOREAPP",
            "NET5_0_OR_GREATER", "NET6_0_OR_GREATER", "NET7_0_OR_GREATER",
            "NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER");

        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(ImplicitGlobalUsings, parseOptions, "GlobalUsings.g.cs"),
        };
        trees.AddRange(sourcePaths.Select(path =>
            CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path)));

        var compilation = CSharpCompilation.Create(
            assemblyName,
            trees,
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        // MemoryPack 제너레이터를 구동해 [MemoryPackable] 직렬화 코드를 컴파일에 주입한다.
        // 제너레이터가 emit하는 트리도 같은 parseOptions(전처리 심볼 포함)로 파싱되도록 전달한다.
        var driver = CSharpGeneratorDriver.Create(
            LoadMemoryPackGenerators(),
            parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var genDiagnostics);

        var genErrors = genDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (genErrors.Length > 0)
            throw new InvalidOperationException(
                "MemoryPack 제너레이터 구동 실패:\n" + string.Join("\n", genErrors.Select(d => d.ToString())));

        using var ms = new MemoryStream();
        var result = updated.Emit(ms);
        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new InvalidOperationException("생성한 테이블 코드 컴파일 실패:\n" + string.Join("\n", errors));
        }

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }

    /// <summary>
    /// 현재 런타임에 로드/배치된 모든 어셈블리(TPA)를 참조로 삼는다.
    /// System.Runtime/Collections/Linq/Text.Json/netstandard 및 MemoryPack.Core가 모두 포함된다.
    /// </summary>
    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "";
        return tpa
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();
    }

    /// <summary>출력 폴더의 MemoryPack.Generator.dll을 로드해 소스 제너레이터 인스턴스를 만든다.</summary>
    private static ISourceGenerator[] LoadMemoryPackGenerators()
    {
        var dllPath = Path.Combine(AppContext.BaseDirectory, GeneratorDllName);
        if (!File.Exists(dllPath))
            throw new FileNotFoundException(
                $"MemoryPack 제너레이터 DLL을 찾을 수 없습니다: {dllPath} (ExcelGenerator.csproj의 복사 설정 확인)");

        var asm = Assembly.LoadFrom(dllPath);

        // IIncrementalGenerator/ISourceGenerator 구현 타입을 모두 찾아 소스 제너레이터로 감싼다.
        var generators = asm.GetTypes()
            .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null)
            .Where(t => typeof(IIncrementalGenerator).IsAssignableFrom(t) || typeof(ISourceGenerator).IsAssignableFrom(t))
            .Select(ToSourceGenerator)
            .ToArray();

        if (generators.Length == 0)
            throw new InvalidOperationException("MemoryPack.Generator.dll에서 소스 제너레이터를 찾지 못했습니다.");

        return generators;
    }

    private static ISourceGenerator ToSourceGenerator(Type t)
    {
        var instance = Activator.CreateInstance(t)!;
        return instance is IIncrementalGenerator inc
            ? inc.AsSourceGenerator()
            : (ISourceGenerator)instance;
    }
}
