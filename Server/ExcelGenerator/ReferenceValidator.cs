namespace ExcelGenerator;

/// <summary>
/// 시트의 <c>Ref</c> 마커 행을 따라 참조 무결성을 검사한다.
///
/// 드롭 테이블처럼 <c>ItemTID</c>를 참조만 하는 시트는, 값이 오타이거나 아이템이 삭제돼도
/// 타입(int)은 멀쩡해서 .bytes까지 그대로 생성된다. 그러면 오류가 런타임 드랍 시점에야
/// KeyNotFoundException으로 터진다. 여기서 생성 전에 잡아 파이프라인을 멈춘다.
///
/// 표기: <c>대상시트.대상컬럼</c> (예: <c>ItemTable.ItemTID</c>)
///       끝에 <c>?</c>를 붙이면 "참조 없음"(빈 셀·0)을 허용한다. 예: <c>ItemTable.ItemTID?</c>
/// 배열 컬럼(int[] 등)은 ','로 나눠 원소마다 검사한다.
/// </summary>
public static class ReferenceValidator
{
    /// <summary>모든 위반을 모아 한 번에 보고한다. 하나씩 고치고 다시 돌리는 왕복을 없애기 위함이다.</summary>
    public static void Validate(IReadOnlyList<ExcelGenerator.TableData> tables)
    {
        var byName = tables.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var keyCache = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var errors = new List<string>();
        var checkedCount = 0;

        foreach (var table in tables)
        {
            for (var i = 0; i < table.columnInfos.Count; i++)
            {
                var col = table.columnInfos[i];
                if (col.Ref is null) continue;

                var allowEmpty = col.Ref.EndsWith('?');
                var target = allowEmpty ? col.Ref[..^1] : col.Ref;

                var keys = ResolveKeys(target, byName, keyCache, table.Name, col.Name, errors);
                if (keys is null) continue;   // 참조 대상 자체가 잘못됨 — 행 검사는 생략

                for (var row = 0; row < table.Rows.Count; row++)
                {
                    foreach (var value in SplitValues(table.Rows[row][i], col.Type))
                    {
                        if (allowEmpty && (value.Length == 0 || value == "0")) continue;
                        if (value.Length == 0) continue;   // 빈 셀은 Default(Null) 규칙이 담당한다

                        if (!keys.Contains(Normalize(value)))
                            errors.Add($"[{table.Name}.{col.Name}] {row + 1}번째 행: '{value}' 이(가) {target} 에 없습니다.");
                    }
                }

                checkedCount++;
                Console.WriteLine($"[참조 검사] {table.Name}.{col.Name} → {target} ({table.Rows.Count}행)");
            }
        }

        if (errors.Count > 0)
            throw new InvalidDataException(
                $"참조 무결성 검사 실패 ({errors.Count}건)\n       " + string.Join("\n       ", errors));

        if (checkedCount == 0)
            Console.WriteLine("[참조 검사] Ref 마커가 지정된 컬럼이 없습니다.");
    }

    /// <summary>"시트.컬럼" 표기를 실제 값 집합으로 바꾼다. 표기·대상이 잘못됐으면 errors에 적고 null을 돌려준다.</summary>
    private static HashSet<string>? ResolveKeys(
        string target,
        IReadOnlyDictionary<string, ExcelGenerator.TableData> byName,
        Dictionary<string, HashSet<string>> cache,
        string sourceTable,
        string sourceColumn,
        List<string> errors)
    {
        if (cache.TryGetValue(target, out var cached))
            return cached;

        var parts = target.Split('.');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            errors.Add($"[{sourceTable}.{sourceColumn}] Ref 표기 '{target}' 가 잘못됐습니다. '시트명.컬럼명' 형식이어야 합니다.");
            return null;
        }

        if (!byName.TryGetValue(parts[0], out var targetTable))
        {
            errors.Add($"[{sourceTable}.{sourceColumn}] Ref 대상 시트 '{parts[0]}' 를 찾을 수 없습니다.");
            return null;
        }

        var targetIndex = -1;
        for (var i = 0; i < targetTable.columnInfos.Count; i++)
        {
            if (targetTable.columnInfos[i].Name == parts[1])
            {
                targetIndex = i;
                break;
            }
        }
        if (targetIndex < 0)
        {
            errors.Add($"[{sourceTable}.{sourceColumn}] Ref 대상 컬럼 '{parts[1]}' 이(가) {parts[0]} 에 없습니다.");
            return null;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in targetTable.Rows)
            keys.Add(Normalize(row[targetIndex]));

        cache[target] = keys;
        return keys;
    }

    /// <summary>배열 컬럼이면 ','로 나눠 원소를 돌려주고, 아니면 셀 값 하나를 그대로 돌려준다.</summary>
    private static IEnumerable<string> SplitValues(string cell, ExcelGenerator.ColumnInfo.RecordType type)
    {
        var isArray = type is ExcelGenerator.ColumnInfo.RecordType.ArrayNumber
                           or ExcelGenerator.ColumnInfo.RecordType.ArrayString
                           or ExcelGenerator.ColumnInfo.RecordType.ArrayFloat
                           or ExcelGenerator.ColumnInfo.RecordType.ArrayEnum;

        return isArray
            ? cell.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { cell };
    }

    /// <summary>엑셀이 숫자를 "1001.0" 같은 형태로 넘겨도 매칭되도록 정수는 정수 표기로 통일한다.</summary>
    private static string Normalize(string value)
        => decimal.TryParse(value, out var d) && decimal.Truncate(d) == d
            ? decimal.Truncate(d).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : value;
}
