<#
.SYNOPSIS
    서버 테스트를 커버리지와 함께 돌리고, 파일별 결과를 낮은 순으로 출력한다.

.DESCRIPTION
    coverlet.collector로 수집하고 coverlet.runsettings의 필터를 적용한다.
    필터 없이 돌리면 MemoryPack 생성물이 전체 라인의 절반을 넘어 숫자가 쓸모없어진다.

    ⚠️ 실행 중인 WSGameServer 프로세스가 있으면 DLL 잠금으로 빌드가 실패한다(MSB3021).
       Unity 에디터는 분석기 DLL 복사만 막으므로 이 스크립트에는 영향이 없다.

.PARAMETER Html
    ReportGenerator로 HTML 리포트까지 만들고 연다.
    전역 도구가 필요하다: dotnet tool install -g dotnet-reportgenerator-globaltool

.PARAMETER Top
    출력할 파일 수. 기본 25 (커버리지가 낮은 순). 0이면 전부.

.EXAMPLE
    powershell -File Server/run-coverage.ps1
    powershell -File Server/run-coverage.ps1 -Html
    powershell -File Server/run-coverage.ps1 -Top 0
#>
param(
    [switch]$Html,
    [int]$Top = 25
)

$ErrorActionPreference = 'Stop'

# 저장소 루트에서 경로를 유도한다 — 어디서 실행하든 동작해야 한다.
$ServerRoot  = Split-Path -Parent $MyInvocation.MyCommand.Path
$TestProject = Join-Path $ServerRoot 'WSGameServer.Tests\WSGameServer.Tests.csproj'
$Settings    = Join-Path $ServerRoot 'coverlet.runsettings'
$ResultsDir  = Join-Path $ServerRoot 'WSGameServer.Tests\TestResults'

if (-not (Test-Path $TestProject)) { throw "테스트 프로젝트를 찾을 수 없습니다: $TestProject" }

# 실행할 때마다 새 GUID 폴더가 쌓인다. 이번 결과만 남기려면 먼저 비운다.
if (Test-Path $ResultsDir) { Remove-Item $ResultsDir -Recurse -Force }

Write-Host '테스트 실행 중...' -ForegroundColor Cyan
dotnet test $TestProject --nologo --collect:"XPlat Code Coverage" --settings $Settings
if ($LASTEXITCODE -ne 0) { throw '테스트가 실패했습니다. 커버리지를 집계하지 않습니다.' }

$report = Get-ChildItem $ResultsDir -Recurse -Filter 'coverage.cobertura.xml' | Select-Object -First 1
if ($null -eq $report) { throw '커버리지 파일을 찾지 못했습니다.' }

# ── 파일 단위 집계 ────────────────────────────────────────────────
# partial class(User.*.cs)가 여러 <class>로 쪼개져 나오므로 filename으로 합친다.
[xml]$xml = Get-Content $report.FullName
$agg = @{}

foreach ($cls in $xml.SelectNodes('//class')) {
    $file = $cls.filename
    if ([string]::IsNullOrWhiteSpace($file)) { continue }

    $lines = $cls.SelectNodes('.//line')
    if ($lines.Count -eq 0) { continue }

    $hit = ($lines | Where-Object { [int]$_.hits -gt 0 }).Count

    # 저장소 루트 아래 상대경로로 줄여서 읽기 쉽게 둔다.
    $key = ($file -replace '\\', '/') -replace '^.*?/Windows_simulator/', ''

    if (-not $agg.ContainsKey($key)) { $agg[$key] = @{ Hit = 0; Total = 0 } }
    $agg[$key].Hit   += $hit
    $agg[$key].Total += $lines.Count
}

$rows = $agg.GetEnumerator() | ForEach-Object {
    [pscustomobject]@{
        File  = $_.Key
        Hit   = $_.Value.Hit
        Total = $_.Value.Total
        Cov   = if ($_.Value.Total) { [math]::Round($_.Value.Hit / $_.Value.Total * 100, 1) } else { 0 }
    }
} | Sort-Object Cov

$totalHit   = ($rows | Measure-Object Hit   -Sum).Sum
$totalLines = ($rows | Measure-Object Total -Sum).Sum
$totalPct   = if ($totalLines) { [math]::Round($totalHit / $totalLines * 100, 1) } else { 0 }

Write-Host ''
if ($Top -gt 0 -and $rows.Count -gt $Top) {
    Write-Host "커버리지 낮은 순 $Top개 (전체 $($rows.Count)개 — 전부 보려면 -Top 0)" -ForegroundColor Yellow
    $rows | Select-Object -First $Top | Format-Table -AutoSize
} else {
    $rows | Format-Table -AutoSize
}

Write-Host ("전체: {0}/{1} 줄  {2}%" -f $totalHit, $totalLines, $totalPct) -ForegroundColor Green
Write-Host ''

if ($Html) {
    if ($null -eq (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
        Write-Host 'reportgenerator가 없습니다. 먼저 설치하세요:' -ForegroundColor Yellow
        Write-Host '  dotnet tool install -g dotnet-reportgenerator-globaltool'
        return
    }

    $htmlDir = Join-Path $ResultsDir 'report'
    reportgenerator -reports:$report.FullName -targetdir:$htmlDir -reporttypes:Html | Out-Null

    $index = Join-Path $htmlDir 'index.html'
    Write-Host "HTML 리포트: $index" -ForegroundColor Green
    Start-Process $index
}
