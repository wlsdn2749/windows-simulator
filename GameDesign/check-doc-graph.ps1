<#
.SYNOPSIS
    기획 문서 참조 그래프를 검사한다.

.DESCRIPTION
    GameDesign/기획 아래 기획 문서들의 상호 링크를 읽어 그래프를 만들고,
    각 문서 헤더의 "바뀌면 갱신" 블록이 실제 역참조와 맞는지 대조한다.

    검사 항목:
      1. 깨진 링크          — 링크 대상 .md 파일이 없다
      2. 헤더 블록 누락      — 기획 문서인데 "바뀌면 갱신" 블록이 없다
      3. 블록 ↔ 그래프 불일치 — 실제 역참조와 블록에 적힌 목록이 다르다
      4. 갱신일 역전        — 내가 갱신됐는데 나를 참조하는 문서가 더 오래됐다 (전파 누락 의심)

    4번이 이 스크립트의 핵심이다. "하위 문서만 고치고 상위를 안 고쳤다"가
    이 저장소에서 실제로 반복된 사고 유형이라(2026-08-02 확인), 날짜 역전으로 잡아낸다.

.PARAMETER Graph
    검사 대신 그래프를 덤프한다. 문서관계도.md의 표를 갱신할 때 쓴다.

.PARAMETER Fix
    각 문서 헤더의 "바뀌면 갱신" 블록을 실제 그래프에 맞춰 다시 쓴다.
    (날짜 역전은 사람이 판단할 문제라 고치지 않는다)

.PARAMETER Changed
    날짜 역전 검사를 **이번에 고친 문서**에서 출발하는 것으로 좁힌다(git 기준).
    저장소 전체는 상시 수십 건이 뜨는데, 커밋 전에 궁금한 것은
    "방금 고친 문서가 전파됐는가" 하나뿐이다. 커밋 전 검사에는 이쪽을 쓴다.

.EXAMPLE
    powershell -File GameDesign/check-doc-graph.ps1 -Changed   # 커밋 전
    powershell -File GameDesign/check-doc-graph.ps1            # 전수 점검
    powershell -File GameDesign/check-doc-graph.ps1 -Graph     # 그래프 덤프
#>
[CmdletBinding()]
param(
    [switch]$Graph,
    [switch]$Fix,
    [switch]$Changed
)

$ErrorActionPreference = 'Stop'

# 경로는 스크립트 위치에서 유도한다 — 어디에 체크아웃하든 동작해야 한다.
$DesignRoot = Join-Path $PSScriptRoot '기획'

# 그래프에서 빼는 것.
#   리서치·방향제안 — 특정 시점의 조사·제안을 남긴 아카이브다. 갱신 전파 대상이 아니다.
#   문서관계도      — 그래프를 서술하는 메타 문서라 자기 자신을 노드로 세면 순환한다.
$ExcludeDirs = @('리서치', '방향제안')
$ExcludeDocs = @('문서관계도')

# 헤더 블록의 표식. 이 줄이 있어야 파싱 대상이 된다.
# 대상이 많은 문서(게임기획코어는 16개)는 여러 줄로 접히므로,
# 표식 줄 다음의 "들여쓴 인용 줄"(`>` + 공백 3칸 이상)까지 한 블록으로 본다.
$BlockMarker  = '바뀌면 갱신'
$BlockPattern = '(?m)^>[^\r\n]*바뀌면 갱신[^\r\n]*(?:\r?\n>\s{3,}[^\r\n]*)*'

# 한 줄에 넣을 링크 수. 넘으면 다음 줄로 접는다.
$LinksPerLine = 6


# ── 유틸 ──────────────────────────────────────────────────────────────────────

# 문서의 표시 이름. `<시스템>/README.md`는 폴더명이 곧 이름이다.
function Get-DocName {
    param([string]$FullPath)

    $rel = $FullPath.Substring($DesignRoot.Length).TrimStart('\', '/') -replace '\\', '/'
    if ($rel -like '*/README.md') {
        return (Split-Path (Split-Path $rel -Parent) -Leaf)
    }
    return [IO.Path]::GetFileNameWithoutExtension($rel)
}

# 링크 대상을 절대 경로로 정규화한다. %20 같은 URL 인코딩을 되돌린다.
function Resolve-LinkTarget {
    param([string]$FromFile, [string]$Target)

    $clean = ($Target -split '#')[0].Trim()
    if (-not $clean) { return $null }

    $decoded = [Uri]::UnescapeDataString($clean)
    $baseDir = Split-Path $FromFile -Parent
    $joined  = Join-Path $baseDir $decoded

    # 존재하지 않는 경로도 정규화해야 하므로 Resolve-Path가 아니라 문자열로 접는다.
    return [IO.Path]::GetFullPath($joined)
}

function Get-DocRelativeLink {
    param([string]$FromFile, [string]$ToFile)

    $fromDir = (Split-Path $FromFile -Parent).TrimEnd('\', '/')
    $to      = $ToFile

    # `.Split('\','/')`는 PowerShell에서 Split(string, count) 오버로드로 잡힌다. -split을 쓴다.
    $fromParts = @($fromDir -split '[\\/]' | Where-Object { $_ })
    $toParts   = @($to      -split '[\\/]' | Where-Object { $_ })

    $common = 0
    while ($common -lt $fromParts.Count -and $common -lt $toParts.Count -and
           $fromParts[$common] -eq $toParts[$common]) {
        $common++
    }

    $up   = @('..') * ($fromParts.Count - $common)
    $down = $toParts[$common..($toParts.Count - 1)]
    $rel  = (@($up) + @($down)) -join '/'

    # 공백은 마크다운 링크에서 깨지므로 인코딩한다 ("진행 및 성장").
    return ($rel -replace ' ', '%20')
}


# ── 문서 수집 ─────────────────────────────────────────────────────────────────

if (-not (Test-Path $DesignRoot)) {
    Write-Error "기획 문서 폴더가 없습니다: $DesignRoot"
}

$docs = @{}   # 절대경로 -> 문서 정보

Get-ChildItem -Path $DesignRoot -Filter *.md -Recurse | ForEach-Object {
    $rel = $_.FullName.Substring($DesignRoot.Length).TrimStart('\', '/') -replace '\\', '/'

    $topDir = ($rel -split '/')[0]
    if ($ExcludeDirs -contains $topDir) { return }

    $name = Get-DocName $_.FullName
    if ($ExcludeDocs -contains $name) { return }

    $text = Get-Content -Path $_.FullName -Raw -Encoding UTF8

    # "최종 업데이트"를 먼저 찾고, 없을 때만 "작성일"로 떨어진다.
    # 기획평가처럼 `> 작성일: A · 최종 업데이트: B` 한 줄에 둘 다 있는 문서가 있어
    # 하나의 정규식으로 훑으면 앞에 적힌 작성일이 잡힌다(= 문서가 영원히 낡아 보인다).
    $updated = $null
    $m = [regex]::Match($text, '(?m)^>.*최종 업데이트\s*:\s*(\d{4}-\d{2}-\d{2})')
    if (-not $m.Success) {
        $m = [regex]::Match($text, '(?m)^>\s*작성일\s*:\s*(\d{4}-\d{2}-\d{2})')
    }
    if ($m.Success) { $updated = $m.Groups[1].Value }

    $docs[$_.FullName] = [pscustomobject]@{
        Path     = $_.FullName
        Rel      = $rel
        Name     = $name
        Text     = $text
        Updated  = $updated
        Links    = @()   # 이 문서가 참조하는 문서 (절대경로)
        Declared = @()   # 헤더 블록에 적힌 갱신 대상 (절대경로)
        HasBlock = $false
        Broken   = @()
    }
}

if ($docs.Count -eq 0) {
    Write-Error "기획 문서를 찾지 못했습니다: $DesignRoot"
}


# ── 링크 파싱 ─────────────────────────────────────────────────────────────────

$linkPattern = '\[[^\]]*\]\(([^)]+?\.md(?:#[^)]*)?)\)'

foreach ($doc in $docs.Values) {
    $links  = New-Object System.Collections.Generic.HashSet[string]
    $broken = New-Object System.Collections.Generic.HashSet[string]

    # ⚠️ "바뀌면 갱신" 블록은 그래프에서 뺀다.
    # 블록은 역참조를 적은 메타 정보라, 이것을 정참조로 세면 A→B가 생길 때마다
    # B→A가 따라 생겨 모든 간선이 양방향으로 번진다(그래프가 자기 자신을 오염시킨다).
    $body = [regex]::Replace($doc.Text, $BlockPattern, '')

    foreach ($match in [regex]::Matches($body, $linkPattern)) {
        $target = Resolve-LinkTarget -FromFile $doc.Path -Target $match.Groups[1].Value
        if (-not $target) { continue }

        if (-not (Test-Path $target)) {
            [void]$broken.Add($match.Groups[1].Value)
            continue
        }

        # 기획 폴더 밖(일감·CLAUDE.md 등)은 그래프 노드가 아니다.
        if ($docs.ContainsKey($target) -and $target -ne $doc.Path) {
            [void]$links.Add($target)
        }
    }

    $doc.Links  = @($links)
    $doc.Broken = @($broken)
}

# 헤더 블록 파싱 — 블록 안의 링크만 뽑는다(장 번호 같은 설명은 자유 서술로 둔다).
foreach ($doc in $docs.Values) {
    $blockMatch = [regex]::Match($doc.Text, $BlockPattern)
    if (-not $blockMatch.Success) { continue }

    $doc.HasBlock = $true

    $declared = New-Object System.Collections.Generic.HashSet[string]
    foreach ($match in [regex]::Matches($blockMatch.Value, $linkPattern)) {
        $target = Resolve-LinkTarget -FromFile $doc.Path -Target $match.Groups[1].Value
        if ($target -and $docs.ContainsKey($target)) { [void]$declared.Add($target) }
    }
    $doc.Declared = @($declared)
}


# ── 역참조 계산 ───────────────────────────────────────────────────────────────
# "A가 바뀌면 갱신할 곳" = A를 참조하는 문서들. 참조가 곧 의존이다.

$incoming = @{}
foreach ($path in $docs.Keys) { $incoming[$path] = New-Object System.Collections.Generic.HashSet[string] }

foreach ($doc in $docs.Values) {
    foreach ($target in $doc.Links) { [void]$incoming[$target].Add($doc.Path) }
}

$sorted = $docs.Values | Sort-Object Rel


# ── -Graph : 그래프 덤프 ──────────────────────────────────────────────────────

if ($Graph) {
    Write-Host ''
    Write-Host '── 정참조 (이 문서가 참조하는 문서) ──' -ForegroundColor Cyan
    foreach ($doc in $sorted) {
        $names = ($doc.Links | ForEach-Object { $docs[$_].Name } | Sort-Object) -join ' · '
        Write-Host ("{0,-14} -> {1}" -f $doc.Name, $names)
    }

    Write-Host ''
    Write-Host '── 역참조 (이 문서가 바뀌면 갱신할 곳) ──' -ForegroundColor Cyan
    foreach ($doc in $sorted) {
        $names = ($incoming[$doc.Path] | ForEach-Object { $docs[$_].Name } | Sort-Object) -join ' · '
        Write-Host ("{0,-14} -> {1}" -f $doc.Name, $names)
    }

    Write-Host ''
    Write-Host '── 갱신일 ──' -ForegroundColor Cyan
    foreach ($doc in $sorted) {
        $u = $doc.Updated
        if (-not $u) { $u = '(없음)' }
        Write-Host ("{0,-14} {1}" -f $doc.Name, $u)
    }
    Write-Host ''
    exit 0
}


# ── -Fix : 헤더 블록 재작성 ───────────────────────────────────────────────────

if ($Fix) {
    $fixed = 0
    foreach ($doc in $sorted) {
        $targets = @($incoming[$doc.Path]) | ForEach-Object { $docs[$_] } | Sort-Object Name
        if ($targets.Count -eq 0) { continue }

        $parts = @(foreach ($t in $targets) {
            "[``$($t.Name)``]($(Get-DocRelativeLink -FromFile $doc.Path -ToFile $t.Path))"
        })

        # 대상이 많으면 줄을 접는다. 이어지는 줄은 들여쓴 인용 줄이라 블록의 일부로 파싱된다.
        $lines = @()
        for ($i = 0; $i -lt $parts.Count; $i += $LinksPerLine) {
            $end   = [Math]::Min($i + $LinksPerLine, $parts.Count) - 1
            $chunk = ($parts[$i..$end] -join ' · ')
            if ($i -eq 0) { $lines += "> **바뀌면 갱신:** $chunk" }
            else          { $lines += ">   $chunk" }
        }
        $block = $lines -join "`n"

        if ($doc.HasBlock) {
            $new = [regex]::Replace($doc.Text, $BlockPattern, $block.Replace('$', '$$'), 1)
        }
        else {
            # "최종 업데이트"(없으면 "작성일") 줄 바로 뒤에 끼운다 — 헤더에서 가장 자연스러운 자리다.
            $m = [regex]::Match($doc.Text, '(?m)^>\s*(?:최종 업데이트|작성일)[^\r\n]*\r?\n')
            if (-not $m.Success) {
                Write-Warning "[$($doc.Name)] 헤더에 '최종 업데이트' 줄이 없어 건너뜁니다"
                continue
            }
            $new = $doc.Text.Insert($m.Index + $m.Length, "$block`n")
        }

        if ($new -ne $doc.Text) {
            # Set-Content -Encoding UTF8은 PowerShell 5.1에서 BOM을 붙인다.
            # 기획 문서는 BOM 없는 UTF-8이므로 .NET으로 직접 쓴다.
            [IO.File]::WriteAllText($doc.Path, $new, (New-Object Text.UTF8Encoding($false)))
            Write-Host "갱신: $($doc.Rel)" -ForegroundColor Green
            $fixed++
        }
    }
    Write-Host ""
    Write-Host "$fixed 개 문서의 '바뀌면 갱신' 블록을 그래프에 맞췄습니다."
    exit 0
}


# ── 검사 ──────────────────────────────────────────────────────────────────────

$errors   = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

# -Changed : git이 "바뀌었다"고 보는 문서만 날짜 역전의 출발점으로 삼는다.
$changedPaths = $null
if ($Changed) {
    $changedPaths = New-Object System.Collections.Generic.HashSet[string]
    # quotepath=false — 한글 경로를 \352\270 같은 8진 이스케이프로 뱉지 않게 한다.
    # safecrlf=false  — 줄바꿈 경고가 목록에 섞이지 않게 한다.
    $gitArgs  = @('-c', 'core.quotepath=false', '-c', 'core.safecrlf=false', '-C', $PSScriptRoot)
    $repoRoot = (git @gitArgs rev-parse --show-toplevel)

    if (-not $repoRoot) {
        Write-Warning 'git 저장소가 아니라 -Changed를 무시하고 전수 검사합니다'
        $changedPaths = $null
    }
    else {
        $names = @(git @gitArgs diff --name-only HEAD --) +
                 @(git @gitArgs ls-files --others --exclude-standard --)
        foreach ($n in ($names | Where-Object { $_ })) {
            try   { [void]$changedPaths.Add([IO.Path]::GetFullPath((Join-Path $repoRoot $n))) }
            catch { Write-Verbose "경로를 해석하지 못해 건너뜁니다: $n" }
        }
        Write-Host "-Changed: 변경된 파일 $($changedPaths.Count)개 기준" -ForegroundColor DarkGray
    }
}

foreach ($doc in $sorted) {

    # 1. 깨진 링크 — 폴더 이름을 바꾸면 조용히 늘어난다.
    foreach ($b in $doc.Broken) {
        $errors.Add("[$($doc.Name)] 깨진 링크: $b")
    }

    # 2·3. 헤더 블록 ↔ 실제 역참조
    $expected = @($incoming[$doc.Path])
    if ($expected.Count -gt 0) {
        if (-not $doc.HasBlock) {
            $errors.Add("[$($doc.Name)] '바뀌면 갱신' 블록이 없습니다 (-Fix로 생성)")
        }
        else {
            $missing = $expected | Where-Object { $doc.Declared -notcontains $_ }
            $extra   = $doc.Declared | Where-Object { $expected -notcontains $_ }

            foreach ($m in $missing) {
                $errors.Add("[$($doc.Name)] 블록에 빠짐: $($docs[$m].Name) — 이 문서를 참조하는데 갱신 대상에 없습니다")
            }
            foreach ($e in $extra) {
                $warnings.Add("[$($doc.Name)] 블록에 남음: $($docs[$e].Name) — 더 이상 이 문서를 참조하지 않습니다")
            }
        }
    }

    # 4. 갱신일 역전 — 전파 누락의 실질 신호다.
    $skipDateCheck = $changedPaths -and (-not $changedPaths.Contains($doc.Path))
    if ($doc.Updated -and -not $skipDateCheck) {
        foreach ($path in $expected) {
            $other = $docs[$path]
            if (-not $other.Updated) { continue }
            if ([datetime]$doc.Updated -gt [datetime]$other.Updated) {
                $warnings.Add(
                    "[$($doc.Name)] $($doc.Updated) > [$($other.Name)] $($other.Updated) — " +
                    "$($other.Name)에 전파되지 않았을 수 있습니다")
            }
        }
    }
}


# ── 결과 ──────────────────────────────────────────────────────────────────────

Write-Host ''
Write-Host "기획 문서 $($docs.Count)개 · 참조 $(($docs.Values | ForEach-Object { $_.Links.Count } | Measure-Object -Sum).Sum)개" -ForegroundColor Cyan
Write-Host ''

if ($warnings.Count -gt 0) {
    Write-Host "경고 $($warnings.Count)건 — 사람이 판단한다" -ForegroundColor Yellow
    foreach ($w in $warnings) { Write-Host "  ! $w" -ForegroundColor Yellow }
    Write-Host ''
}

if ($errors.Count -gt 0) {
    Write-Host "오류 $($errors.Count)건" -ForegroundColor Red
    foreach ($e in $errors) { Write-Host "  x $e" -ForegroundColor Red }
    Write-Host ''
    exit 1
}

Write-Host '그래프 정합성 OK' -ForegroundColor Green
Write-Host ''
exit 0
