# 엑셀 → 코드(.cs) + MemoryPack 바이너리(.bytes) → 미러링 파이프라인
#
# 위치: GameDesign/ — 입력(엑셀)이 여기 있고 서버·클라 어느 쪽 담당도 아닌 공용 파이프라인이라
#       중립 폴더에 둔다. 기획자가 엑셀을 고친 자리에서 바로 실행할 수 있다.
#
# 입력: GameDesign/Excel/*.xlsx  — 저장소 루트의 공용 기획 데이터(서버/클라 어느 쪽 폴더에도 속하지 않음)
#
# 1단계: ExcelGenerator — Enum/Row/GameTable/TableSet/Packer 코드 생성 + 그 코드를 런타임 컴파일해
#                          .bytes까지 한 번에 생성 (Shared/Data/*.bytes). 구 ExcelDataPacker 흡수.
#                          대조용 JSON 사이드카는 GameDesign/DataLog 에 기록.
# 2단계: C# 9 규약 검사  — 미러 대상 생성물이 Unity(C# 9) 문법 범위를 벗어나지 않는지 확인.
#                          GameData.csproj는 [MemoryPackable] 때문에 LangVersion 11을 쓸 수밖에 없어
#                          서버 빌드가 이 위반을 잡지 못한다. Unity를 열지 않고 막는 유일한 지점이다.
# 3단계: 미러링         — 정의(.cs) → Unity, 데이터(.bytes) → Unity StreamingAssets
#                          (서버는 WSGameServer.csproj의 Content로 .bytes를 bin에 자동 복사)

$ErrorActionPreference = "Stop"

# 모든 경로는 이 스크립트 위치(GameDesign/)에서 유도한다.
# 머신·체크아웃 위치가 달라도 그대로 동작하도록 절대경로를 박아 두지 않는다.
$RepoRoot   = Split-Path -Parent $PSScriptRoot          # 저장소 루트
$ServerRoot = Join-Path $RepoRoot "Server"              # .NET 솔루션 루트
$UnityRoot  = Join-Path $RepoRoot "Assets"              # Unity 프로젝트 루트(미러링 대상)

# 콘솔에서 직접 실행(더블클릭 등)했을 때만 멈춘다. NonInteractive(CI 등)면 프롬프트가 불가하므로 조용히 넘어간다.
# 더블클릭 실행 시 창이 바로 닫혀 결과(성공/실패)를 못 보는 걸 막는다.
function Wait-BeforeClose {
    if ([Environment]::UserInteractive) {
        try { Read-Host "엔터를 누르면 창을 닫습니다" | Out-Null } catch { }
    }
}

# 실패 시 원인을 출력하고 창을 유지한 뒤 종료한다.
function Stop-OnFailure {
    param([string]$Message, [int]$Code = 1)
    Write-Host "[실패] $Message" -ForegroundColor Red
    Wait-BeforeClose
    exit $Code
}

# 미러 대상 생성물이 Unity(C# 9) 문법 범위를 지키는지 검사한다.
#
# 왜 여기서 검사하는가:
#   GameData.csproj는 Row의 [MemoryPackable] 때문에 LangVersion 11이 강제된다
#   (MemoryPack 생성기가 static abstract·scoped를 방출 → C# 9면 CS8703/CS8987).
#   그래서 서버 빌드는 C# 9 위반을 통과시키고, Unity에서만 뒤늦게 깨진다.
#   위반된 코드를 Unity로 내보내기 전에 여기서 끊는다.
function Assert-CSharp9 {
    param([string]$Dir)

    # Unity(C# 9)에서 컴파일되지 않는 신문법. 생성기가 실수로 방출하면 걸린다.
    $forbidden = @(
        @{ Pattern = '^\s*namespace\s+[\w\.]+\s*;';                    Desc = 'file-scoped namespace (C# 10)' }
        @{ Pattern = '^\s*global\s+using\s';                           Desc = 'global using (C# 10)' }
        @{ Pattern = '\brecord\s+struct\b';                            Desc = 'record struct (C# 10)' }
        @{ Pattern = '\b(public|internal|protected|private)\s+required\b'; Desc = 'required 멤버 (C# 11)' }
        @{ Pattern = '=\s*\[';                                         Desc = '컬렉션 표현식 (C# 12)' }
    )

    # obj/·bin/은 제외한다. EmitCompilerGeneratedFiles=true 때문에 MemoryPack 생성기 산출물(.g.cs)이
    # obj/ 아래에 쌓이는데, 이들은 file-scoped namespace를 쓰지만 Unity로 미러링되지 않는다
    # (Unity는 자체 MemoryPack 패키지로 포매터를 다시 생성한다). 포함하면 오탐으로 파이프라인이 멈춘다.
    $violations = @()
    $targets = Get-ChildItem -LiteralPath $Dir -Filter *.cs -File -Recurse |
               Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }

    foreach ($file in $targets) {
        $lines = Get-Content -LiteralPath $file.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            foreach ($rule in $forbidden) {
                if ($lines[$i] -match $rule.Pattern) {
                    $violations += "  $($file.Name)($($i + 1)): $($rule.Desc)"
                    $violations += "      $($lines[$i].Trim())"
                }
            }
        }
    }

    if ($violations.Count -gt 0) {
        Write-Host "  Unity(C# 9)에서 컴파일되지 않는 생성물이 있습니다:" -ForegroundColor Red
        $violations | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        Stop-OnFailure "ExcelGenerator의 생성 문법을 C# 9 범위로 고치세요. (Server/ExcelGenerator/CodeGenUtil.cs 참조)"
    }

    Write-Host "  ok : C# 9 범위 준수" -ForegroundColor Green
}

try {
    Write-Host "[1/3] ExcelGenerator: 코드 + 바이너리 생성" -ForegroundColor Cyan
    # 저장소 루트를 명시적으로 넘긴다. 인자가 없으면 툴이 소스 위치에서 스스로 찾지만,
    # 파이프라인이 이미 아는 값을 넘겨 두면 탐색 결과가 어긋날 여지가 없다.
    dotnet run --project (Join-Path $ServerRoot "ExcelGenerator") -- $RepoRoot
    if ($LASTEXITCODE -ne 0) { Stop-OnFailure "코드/바이너리 생성 단계에서 중단합니다." $LASTEXITCODE }

    Write-Host "[2/3] C# 9 규약 검사: 미러 대상 생성물" -ForegroundColor Cyan
    Assert-CSharp9 (Join-Path $ServerRoot "GameData")

    Write-Host "[3/3] 미러링: 정의(.cs) → Unity, 데이터(.bytes) → Unity StreamingAssets" -ForegroundColor Cyan

    # 3-1) GameData 정의(.cs) → Assets/Scripts_Server/GameData (기존 프로토콜 미러 스크립트 재사용)
    #      스크립트가 Server/에 있으므로 소스·대상 루트를 명시적으로 넘겨 기본값에 의존하지 않는다.
    & (Join-Path $ServerRoot "sync-protocol-to-unity.ps1") `
        -SourceRoot $ServerRoot `
        -DestRoot (Join-Path $UnityRoot "Scripts_Server") `
        -FolderMap @{ "GameData" = "GameData" }
    if ($LASTEXITCODE -ne 0) { Stop-OnFailure "정의(.cs) 미러링 단계에서 중단합니다." $LASTEXITCODE }

    # 3-2) .bytes → Assets/StreamingAssets/Data (없어진 파일은 함께 정리)
    $dataSrc   = Join-Path $ServerRoot "Shared\Data"
    $unityData = Join-Path $UnityRoot   "StreamingAssets\Data"
    New-Item -ItemType Directory -Force -Path $unityData | Out-Null

    $srcBytes = Get-ChildItem -LiteralPath $dataSrc -Filter *.bytes -File -ErrorAction SilentlyContinue
    $expected = New-Object System.Collections.Generic.HashSet[string]
    foreach ($b in $srcBytes) {
        [void]$expected.Add($b.Name)
        Copy-Item -LiteralPath $b.FullName -Destination (Join-Path $unityData $b.Name) -Force
        Write-Host "  copy : StreamingAssets\Data\$($b.Name)" -ForegroundColor Green
    }
    # 소스에서 사라진 .bytes 는 Unity 쪽에서도 .meta 와 함께 제거
    foreach ($d in (Get-ChildItem -LiteralPath $unityData -Filter *.bytes -File -ErrorAction SilentlyContinue)) {
        if (-not $expected.Contains($d.Name)) {
            Remove-Item -LiteralPath $d.FullName -Force
            $meta = "$($d.FullName).meta"
            if (Test-Path -LiteralPath $meta) { Remove-Item -LiteralPath $meta -Force }
            Write-Host "  del  : StreamingAssets\Data\$($d.Name)" -ForegroundColor DarkYellow
        }
    }

    Write-Host "[완료] GameData(.cs)/Shared·Unity(.bytes) 생성 + 미러링 완료" -ForegroundColor Green
    Wait-BeforeClose
}
catch {
    # $ErrorActionPreference=Stop 로 인한 종료성 예외도 창을 유지한 채 보여준다.
    Stop-OnFailure $_.Exception.Message
}
