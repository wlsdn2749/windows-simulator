using ClosedXML.Excel;

namespace ExcelGenerator;

public static class ExcelGenerator
{
    private static List<TableData> _tables = new();

    /// <summary>파싱된 테이블들. GenerateData가 .bytes 패킹 시 각 테이블의 Rows를 사용한다.</summary>
    public static IReadOnlyList<TableData> Tables => _tables;

    public static void LoadExcel(string excelDir)
    {
        _tables.Clear();

        var excels = Directory.EnumerateFiles(excelDir, "*.xlsx")
            .Where(path => !Path.GetFileName(path).StartsWith("~$"))
            .Where(path => !string.Equals(Path.GetFileName(path), "Enum.xlsx", StringComparison.OrdinalIgnoreCase));

        foreach (var excel in excels)
        {
            using var workbook = OpenWorkbook(excel);

            foreach (var worksheet in workbook.Worksheets)
            {
                _tables.Add(ParseSheet(worksheet));
            }
        }
    }

    /// <summary>
    /// 워크북을 연다. 파일이 잠겨 있으면(대개 Excel에서 그 파일을 열어 둔 상태) 원인을 명확히 밝히고 즉시 실패한다.
    /// 예외를 삼키고 지나가면 뒤에서 "소스가 null" 같은 엉뚱한 오류로 번지므로 여기서 fail-fast 한다.
    /// </summary>
    public static XLWorkbook OpenWorkbook(string path)
    {
        try
        {
            return new XLWorkbook(path);
        }
        catch (IOException ex)
        {
            throw new IOException(
                $"엑셀 파일을 열 수 없습니다: '{path}'. " +
                $"Excel에서 이 파일이 열려 있으면 닫고 다시 실행하세요. (원인: {ex.Message})", ex);
        }
    }

    // MemoryPack은 소스 제너레이터 기반이라 생성한 Row 클래스가 컴파일된 뒤에야 직렬화할 수 있다.
    // 예전엔 이 때문에 별도 ExcelDataPacker 프로젝트로 빌드했지만, 지금은 GenerateData가 생성 코드를
    // 런타임에 인메모리 컴파일(TableCompiler)해 이 프로세스에서 .bytes까지 만든다.

    /// <summary>파싱된 테이블들로부터 공유 정의(Row/GameTable/TableSet)는 gameDataDir에,
    /// 툴 전용 Packer 코드는 packerDir에 생성한다.</summary>
    public static void GenerateCode(string gameDataDir, string packerDir)
    {
        TableCodeGenerator.Generate(_tables, gameDataDir, packerDir, ColumnInfo.Platform.ServerClient);
    }

    /// <summary>
    /// GenerateCode가 만든 코드를 런타임에 인메모리 컴파일(MemoryPack 제너레이터 구동)하고,
    /// TableRegistry를 리플렉션으로 호출해 각 테이블의 .bytes를 dataDir에 생성한다.
    /// logDir에는 사람이 읽는 JSON 사이드카(enum=이름, 한글 그대로)를 함께 써서 엑셀 대조/리뷰를 돕는다.
    /// (별도 ExcelDataPacker 없이 이 프로세스에서 코드+바이너리를 모두 만든다.)
    /// </summary>
    public static void GenerateData(string gameDataDir, string packerDir, string dataDir, string logDir)
    {
        var assembly = TableCompiler.Compile(CollectSources(gameDataDir, packerDir));

        // TableRegistry.Tables는 static readonly '필드'(프로퍼티 아님)라 GetField로 접근한다.
        var registryType = assembly.GetType("GameData.TableRegistry")
            ?? throw new InvalidOperationException("생성 어셈블리에서 GameData.TableRegistry를 찾지 못했습니다.");
        var tables = (System.Collections.IDictionary)registryType.GetField("Tables")!.GetValue(null)!;

        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(logDir);

        foreach (var table in _tables)
        {
            var entry = tables[table.Name]
                ?? throw new InvalidOperationException($"'{table.Name}' 패커 엔트리가 없습니다. 코드 생성이 선행됐는지 확인하세요.");
            var entryType = entry.GetType();
            var pack    = (Delegate)entryType.GetProperty("Pack")!.GetValue(entry)!;
            var verify  = (Delegate)entryType.GetProperty("Verify")!.GetValue(entry)!;
            var preview = (Delegate)entryType.GetProperty("Preview")!.GetValue(entry)!;
            var dump    = (Delegate)entryType.GetProperty("Dump")!.GetValue(entry)!;

            byte[] bytes;
            try
            {
                bytes = (byte[])pack.DynamicInvoke(table.Rows)!;
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // Pack 내부의 데이터 오류(테이블/컬럼/행 메시지)를 그대로 드러낸다(fail-fast).
                throw ex.InnerException;
            }

            var outputPath = Path.Combine(dataDir, $"{table.Name}.bytes");
            File.WriteAllBytes(outputPath, bytes);

            // 기록 직후 역직렬화 라운드트립으로 자가검증한다.
            var count = (int)verify.DynamicInvoke(bytes)!;

            // 사람이 읽는 JSON 사이드카(전체 행) → 엑셀 대조/git diff 리뷰용.
            var logPath = Path.Combine(logDir, $"{table.Name}.json");
            File.WriteAllText(logPath, (string)dump.DynamicInvoke(bytes)!,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Console.WriteLine($"[데이터 생성] {table.Name}: {count}행, {bytes.Length:N0} bytes → {outputPath}");
            Console.WriteLine($"    로그: {logPath}");
            Console.WriteLine($"    첫 행: {(string)preview.DynamicInvoke(bytes)!}");
        }

        // 시트를 지워도 옛 .bytes/.json은 그대로 남는다. 미러링은 "소스에 있는 파일"을 옮길 뿐이라
        // 그 고아가 서버 실행 폴더(Content 복사)와 Unity StreamingAssets까지 계속 따라간다.
        // Row/Packer(.cs)는 TableCodeGenerator가 매번 폴더를 비워 해결하므로 여기선 데이터만 정리한다.
        var alive = _tables.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        RemoveOrphans(dataDir, "*.bytes", alive);
        RemoveOrphans(logDir,  "*.json",  alive);
    }

    /// <summary>현재 테이블 목록에 없는 생성물을 지운다(파일명 = 테이블명 규칙).</summary>
    private static void RemoveOrphans(string dir, string pattern, HashSet<string> aliveTableNames)
    {
        // 열거 도중 삭제하지 않도록 먼저 스냅샷을 뜬다.
        foreach (var path in Directory.EnumerateFiles(dir, pattern).ToList())
        {
            if (aliveTableNames.Contains(Path.GetFileNameWithoutExtension(path)))
                continue;

            File.Delete(path);
            Console.WriteLine($"[정리] 사라진 테이블의 생성물 삭제: {path}");
        }
    }

    /// <summary>런타임 컴파일 대상 소스 목록: 공유 정의(GameData) + 툴 전용 Packer.</summary>
    private static IReadOnlyList<string> CollectSources(string gameDataDir, string packerDir)
    {
        var sources = new List<string>();

        void AddIfExists(string path)
        {
            if (File.Exists(path)) sources.Add(path);
        }

        AddIfExists(Path.Combine(gameDataDir, "Enum.cs"));
        AddIfExists(Path.Combine(gameDataDir, "GameTable.cs"));
        AddIfExists(Path.Combine(gameDataDir, "TableSet.cs"));

        var tablesDir = Path.Combine(gameDataDir, "Tables");
        if (Directory.Exists(tablesDir))
            sources.AddRange(Directory.GetFiles(tablesDir, "*.cs"));
        if (Directory.Exists(packerDir))
            sources.AddRange(Directory.GetFiles(packerDir, "*.cs"));

        return sources;
    }


    public record ColumnInfo(
        string                  Name,
        ColumnInfo.RecordType   Type,
        string                  RawType,
        ColumnInfo.Platform     CS,
        int?                    Min,           //
        int?                    Max,
        string?                 DefaultValue,
        string?                 Ref            // "ItemTable.ItemTID" 형식. 값이 대상 테이블에 실재하는지 검사한다(null이면 검사 없음)
        )
    {
        public enum RecordType : byte
        {
            Ignore      = 0b00000000,
            Int         = 0b00000001,
            Float       = 0b00000010,
            String      = 0b00000100,
            ID          = 0b00000101,
            EnumType    = 0b00000110,
            Long        = 0b00000111,
            Bool        = 0b00001000,
            ArrayString = 0b00001001,
            ArrayNumber = 0b00001010,
            ArrayFloat  = 0b00001011,
            ArrayEnum   = 0b00001100,
            Time        = 0b00001101,
        }

        public enum Platform : byte
        {
            Client          = 0b00000001,
            Server          = 0b00000010,
            ServerClient    = 0b00000011, 
        }
    }
    
    private static ColumnInfo.RecordType MapRecordType(string rawType) => rawType switch
    {
        "int"                           => ColumnInfo.RecordType.Int,
        "long"                          => ColumnInfo.RecordType.Long,
        "float"                         => ColumnInfo.RecordType.Float,
        "string"                        => ColumnInfo.RecordType.String,
        "bool"                          => ColumnInfo.RecordType.Bool,
        "ID"                            => ColumnInfo.RecordType.ID,
        _ when rawType.EndsWith("[]")   => MapArrayType(rawType),           // int[]/long[]/float[]/string[]/eXxx[]
        _ when rawType.StartsWith("e")  => ColumnInfo.RecordType.EnumType,  // eItemType, eItemRarity
        _                               => ColumnInfo.RecordType.Ignore,
    };

    /// <summary>배열 표기 "element[]"의 원소 타입으로 Array 계열 RecordType을 고른다.</summary>
    private static ColumnInfo.RecordType MapArrayType(string rawType)
    {
        var element = rawType[..^2];   // "[]" 제거
        return element switch
        {
            "int" or "long"                => ColumnInfo.RecordType.ArrayNumber,   // 정수 배열(int/long은 원본 타입으로 구분)
            "float"                        => ColumnInfo.RecordType.ArrayFloat,
            "string"                       => ColumnInfo.RecordType.ArrayString,
            _ when element.StartsWith("e") => ColumnInfo.RecordType.ArrayEnum,      // eItemType[] 등
            _                              => ColumnInfo.RecordType.Ignore,
        };
    }
    

    public record TableData(string Name, IReadOnlyList<ColumnInfo> columnInfos, IReadOnlyList<string[]> Rows);

    public static TableData ParseSheet(IXLWorksheet ws)
    {
        var lastRow = ws.LastRowUsed()!.RowNumber();

        // 1) A열 스캔 → 마커→행 맵
        var markerRows = new Dictionary<string, int>();
        for (var row = 1; row <= lastRow; row++)
        {
            var marker = ws.Cell(row, 1).GetString().Trim();
            if (!string.IsNullOrEmpty(marker))
                markerRows[marker] = row;
        }

        // 2) 필드명 행 / 데이터 시작 행
        var fieldNameRow  = markerRows.Values.Max() + 1;
        var dataStartRow  = fieldNameRow + 1;

        int    Row(string m)          => markerRows.GetValueOrDefault(m, 0);
        string Cell(int r, int c)     => r > 0 ? ws.Cell(r, c).GetString().Trim() : "";

        // 3) 필드 열(B~) 순회 → 스키마 조립
        //    빈 열을 스킵해도 셀 읽기가 어긋나지 않도록 실제 컬럼 번호를 함께 기록한다.
        var columnInfos  = new List<ColumnInfo>();
        var colNumbers   = new List<int>();
        var lastCol  = ws.Row(fieldNameRow).LastCellUsed()!.Address.ColumnNumber;
        for (var col = 2; col <= lastCol; col++)
        {
            var name = Cell(fieldNameRow, col);
            if (string.IsNullOrEmpty(name)) continue;   // 빈 열 스킵

            var rawType = Cell(Row("Type"), col);
            colNumbers.Add(col);
            columnInfos.Add(new ColumnInfo(
                Name:           name,
                Type:           MapRecordType(rawType),         // "int"→Int, "eItemType"→EnumType ...
                RawType:        rawType,
                CS:             ParsePlatform(Cell(Row("C&S"), col)),   // "a"/"c"/"s" → Platform
                Min:            ParseIntOrNull(Cell(Row("Min"), col)),
                Max:            ParseIntOrNull(Cell(Row("Max"), col)),
                DefaultValue:   Cell(Row("Default(Null)"), col) is { Length: > 0 } d ? d : null,
                Ref:            Cell(Row("Ref"), col) is { Length: > 0 } r ? r : null));
        }
        
        // 4) 데이터 행 읽기 (스키마 순서대로 셀 수집)
        var rows = new List<string[]>();
        for (var row = dataStartRow; row <= lastRow; row++)
        {
            if (string.IsNullOrEmpty(ws.Cell(row, colNumbers[0]).GetString().Trim()))
                continue;   // 첫 필드(TID) 비면 빈 행

            var cells = new string[columnInfos.Count];
            for (var i = 0; i < columnInfos.Count; i++)
                cells[i] = ws.Cell(row, colNumbers[i]).GetString().Trim();
            rows.Add(cells);
        }

        return new TableData(ws.Name, columnInfos, rows);
    }
    
    static ColumnInfo.Platform ParsePlatform(string s) => s.ToLower() switch
    {
        "c" => ColumnInfo.Platform.Client,
        "s" => ColumnInfo.Platform.Server,
        _   => ColumnInfo.Platform.ServerClient,   // "a"/공백 = 양쪽
    };

    static int? ParseIntOrNull(string s) => int.TryParse(s, out var n) ? n : null;
}