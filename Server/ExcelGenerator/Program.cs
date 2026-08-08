using System.Runtime.CompilerServices;

namespace ExcelGenerator;

public class Program
{
    // 저장소 루트 기준 경로. 폴더를 옮기면 여기만 고치면 된다.
    // 프로젝트 폴더 상대("../GameData")로 두면 이 프로젝트를 옮기는 순간
    // 컴파일은 그대로 통과하면서 엉뚱한 위치에 파일을 쓰는 사고가 난다.
    private const string ExcelDirRel   = "GameDesign/Excel";     // 사람이 편집하는 원본
    private const string DataLogDirRel = "GameDesign/DataLog";   // 대조용 JSON 사이드카
    private const string GameDataRel   = "Server/GameData";      // 생성된 정의(.cs)
    private const string BytesDirRel   = "Server/Shared/Data";   // MemoryPack 바이너리

    /// <summary>저장소 루트 표식. 소스 위치에서 위로 올라가며 이 중 하나를 찾는다.</summary>
    private static readonly string[] RepoRootMarkers = { ".git", "GameDesign" };

    public static int Main(string[] args)
    {
        try
        {
            // 루트는 인자로 받는 것이 우선이고(파이프라인이 넘긴다), 없으면 소스 위치에서 거슬러 올라가 찾는다.
            // 실행 경로(bin/Debug/...)를 쓰지 않으므로 어디서 실행하든 결과가 같다.
            var repoRoot = args.Length > 0 && args[0].Length > 0
                ? Path.GetFullPath(args[0])
                : FindRepoRoot();

            Console.WriteLine($"[경로] 저장소 루트 = {repoRoot}");

            // GetFullPath로 구분자를 플랫폼 표기로 통일한다(로그에 '/'와 '\'가 섞이지 않도록).
            var excelDir    = Path.GetFullPath(Path.Combine(repoRoot, ExcelDirRel));
            var logDir      = Path.GetFullPath(Path.Combine(repoRoot, DataLogDirRel));
            var gameDataDir = Path.GetFullPath(Path.Combine(repoRoot, GameDataRel));
            var dataDir     = Path.GetFullPath(Path.Combine(repoRoot, BytesDirRel));

            // Packer는 이 툴 전용 중간 산출물이라 프로젝트 안에 둔다 — 프로젝트를 옮기면 함께 따라간다.
            var packerDir = ResolvePath("Output/Code/Packer");

            var enumFilePath   = Path.Combine(excelDir, "Enum.xlsx");
            var enumOutputPath = Path.Combine(gameDataDir, "Enum.cs");

            AssertExists(excelDir, "엑셀 원본 폴더");

            // 1) Enum 생성 (Enum.xlsx → GameData/Enum.cs)
            EnumGenerator.GenerateEnumSource(enumFilePath);
            EnumGenerator.MakeEnumCode(enumOutputPath);

            Console.WriteLine($"ItemType.Farming 존재? {EnumGenerator.HasMember("ItemType", "Farming")}");
            Console.WriteLine($"GlobalRarity.Mythic 존재? {EnumGenerator.HasMember("GlobalRarity", "Mythic")}");

            // 2) 데이터 테이블 파싱 → 참조 무결성 검사 → Row/Packer 코드 생성
            //    참조 검사는 코드 생성 전에 둔다. 끊어진 TID로 만들어진 .bytes가 서버에 배포되면
            //    실제 드랍이 일어나는 순간에야 KeyNotFoundException으로 드러나기 때문이다.
            ExcelGenerator.LoadExcel(excelDir);
            ReferenceValidator.Validate(ExcelGenerator.Tables);
            ExcelGenerator.GenerateCode(gameDataDir, packerDir);

            // 3) 생성 코드를 런타임 컴파일해 .bytes 생성 (구 ExcelDataPacker 역할 흡수) + JSON 사이드카(DataLog)
            ExcelGenerator.GenerateData(gameDataDir, packerDir, dataDir, logDir);

            Console.WriteLine("[완료] 코드(GameData) + 바이너리(Shared/Data) + 로그(DataLog) 생성 완료");
            return 0;
        }
        catch (Exception ex)
        {
            // 데이터 오류·파일 잠금 등은 여기서 원인 체인을 찍고 비0으로 종료한다(파이프라인이 멈추도록).
            Console.Error.WriteLine($"[오류] {ex.Message}");
            for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
                Console.Error.WriteLine($"       └ {inner.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 소스 파일 위치에서 위로 올라가며 저장소 루트를 찾는다.
    /// bin/obj 어디서 실행하든 컴파일 시점의 소스 위치를 기준으로 삼으므로 실행 경로에 흔들리지 않는다.
    /// </summary>
    private static string FindRepoRoot([CallerFilePath] string sourceFilePath = "")
    {
        var start = Path.GetDirectoryName(sourceFilePath);
        if (string.IsNullOrEmpty(start))
            throw new DirectoryNotFoundException("소스 경로를 알 수 없습니다. 저장소 루트를 첫 인자로 넘기세요.");

        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            foreach (var marker in RepoRootMarkers)
            {
                var probe = Path.Combine(dir.FullName, marker);
                if (Directory.Exists(probe) || File.Exists(probe))   // .git은 워크트리에서 파일일 수 있다
                    return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"'{start}' 위쪽에서 저장소 루트({string.Join("·", RepoRootMarkers)})를 찾지 못했습니다. " +
            "저장소 루트를 첫 인자로 넘기세요.");
    }

    /// <summary>필수 입력 폴더가 없으면 즉시 멈춘다. 없는 채로 진행하면 "테이블 0개"로 조용히 성공한다.</summary>
    private static void AssertExists(string dir, string what)
    {
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"{what}가 없습니다: {dir}");
    }

    /// <summary>
    /// 이 소스 파일이 있는 디렉터리(=프로젝트 루트) 기준 상대 경로를 절대 경로로 변환한다.
    /// 프로젝트와 함께 움직여야 하는 산출물에만 쓴다. 저장소 공용 경로에는 쓰지 않는다.
    /// </summary>
    public static string ResolvePath(string relativePath, [CallerFilePath] string sourceFilePath = "")
    {
        var projectRoot = Path.GetDirectoryName(sourceFilePath)!;
        return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
    }
}
