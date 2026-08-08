<#
.SYNOPSIS
    MikaProtocol(서버) -> Protocol(Unity) 로 .cs 정의를 단방향 미러링한다.

.DESCRIPTION
    서버가 패킷의 단일 진실(source of truth)이다. 이 스크립트는 서버 쪽 공유
    프로젝트의 .cs 파일만 Unity Assets 아래로 복사한다.

    - 소스 폴더명과 대상 폴더명이 다를 수 있다(예: MikaProtocol -> Protocol).
      $FolderMap 에 "소스 = 대상" 형태로 매핑한다.
    - bin/obj 는 제외한다.
    - .meta 는 복사하지 않는다(Unity 가 경로 기준으로 알아서 생성/유지).
    - 내용이 같은 파일은 건너뛴다(불필요한 Unity 리임포트 방지).
    - 서버에서 삭제된 .cs 는 Unity 쪽에서도 .meta 와 함께 제거한다(진짜 미러).
    - 직렬화 코드(MemoryPack)나 핸들러 디스패처(Roslyn)는 복사 대상이 아니다.
      양쪽이 각자 생성하므로 정의(.cs)만 옮긴다.

.NOTES
    MSBuild post-build 에서 호출하는 용도. 예) MikaProtocol.csproj 에
      <Target Name="SyncToUnity" AfterTargets="Build">
        <Exec Command="powershell -NoProfile -ExecutionPolicy Bypass -File &quot;$(SolutionDir)sync-protocol-to-unity.ps1&quot;" />
      </Target>
#>
[CmdletBinding()]
param(
    # 서버 솔루션 루트 (기본값: 이 스크립트가 있는 폴더)
    [string]$SourceRoot,

    # Unity 프로젝트의 스크립트 루트 (기본값: 저장소 루트 기준 Assets\Scripts_Server)
    [string]$DestRoot,

    # 미러링 매핑 ("소스 폴더" = "대상 폴더", SourceRoot/DestRoot 기준 상대경로).
    # 소스와 대상 폴더명이 달라도 된다. 공유 코어도 동기화하려면
    # 여기에 "MikaNetwork.Core" = "Core" 같은 항목을 추가하면 된다.
    [hashtable]$FolderMap = @{ "MikaProtocol" = "Protocol" }
)

$ErrorActionPreference = "Stop"

# -File 로 호출되면 $PSScriptRoot 가 빌 수 있어 보강한다.
if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $SourceRoot = $PSScriptRoot
    } else {
        $SourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
}

# 대상 루트도 절대경로를 박지 않고 위치에서 유도한다(SourceRoot = Server/ → 부모가 저장소 루트).
if ([string]::IsNullOrWhiteSpace($DestRoot)) {
    $DestRoot = Join-Path (Split-Path -Parent $SourceRoot) "Assets\Scripts_Server"
}

function Get-FileHashSafe([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    return (Get-FileHash -LiteralPath $path -Algorithm MD5).Hash
}

$copied = 0
$skipped = 0
$removed = 0

foreach ($srcFolder in $FolderMap.Keys) {
    $dstFolder = $FolderMap[$srcFolder]
    $srcDir = Join-Path $SourceRoot $srcFolder
    $dstDir = Join-Path $DestRoot   $dstFolder

    if (-not (Test-Path -LiteralPath $srcDir)) {
        Write-Warning "소스 폴더 없음, 건너뜀: $srcDir"
        continue
    }

    # 1) 소스의 .cs 를 대상에 복사 (bin/obj 제외, 하위 폴더 구조 유지)
    $srcFiles = Get-ChildItem -LiteralPath $srcDir -Recurse -File -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

    $expected = New-Object System.Collections.Generic.HashSet[string]

    foreach ($f in $srcFiles) {
        $rel = $f.FullName.Substring($srcDir.Length).TrimStart('\','/')
        $dst = Join-Path $dstDir $rel
        [void]$expected.Add($dst)

        $dstParent = Split-Path -Parent $dst
        if (-not (Test-Path -LiteralPath $dstParent)) {
            New-Item -ItemType Directory -Path $dstParent -Force | Out-Null
        }

        if ((Get-FileHashSafe $f.FullName) -eq (Get-FileHashSafe $dst)) {
            $skipped++
            continue
        }

        Copy-Item -LiteralPath $f.FullName -Destination $dst -Force
        Write-Host "  copy : $dstFolder\$rel" -ForegroundColor Green
        $copied++
    }

    # 2) 소스에서 사라진 .cs 는 대상에서도 제거 (.meta 동반 삭제)
    if (Test-Path -LiteralPath $dstDir) {
        $dstFiles = Get-ChildItem -LiteralPath $dstDir -Recurse -File -Filter *.cs |
            Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

        foreach ($d in $dstFiles) {
            if (-not $expected.Contains($d.FullName)) {
                Remove-Item -LiteralPath $d.FullName -Force
                $rel = $d.FullName.Substring($dstDir.Length).TrimStart('\','/')
                Write-Host "  del  : $dstFolder\$rel" -ForegroundColor DarkYellow
                $removed++

                $meta = "$($d.FullName).meta"
                if (Test-Path -LiteralPath $meta) { Remove-Item -LiteralPath $meta -Force }
            }
        }
    }
}

Write-Host ("[sync-protocol] done. copied={0} removed={1} unchanged={2}" -f $copied, $removed, $skipped) -ForegroundColor Cyan
