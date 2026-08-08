---
date: 2026-07-29
title: MIKA001 경고가 Unity 콘솔에 뜨지 않던 문제 수정
tags: [server, sourcegen, unity, diagnostics]
---

# MIKA001 경고가 Unity 콘솔에 뜨지 않던 문제 수정

## 목적 / 배경

사용자 보고: **"패킷 핸들러가 없으면 경고가 나게 해 뒀는데,
`dotnet build`에서는 뜨고 Unity 실행 시에는 안 뜬다."**

## 원인 — 진단에 위치가 없었다

`ReportMissingHandlers`가 `Location.None`으로 진단을 냈다. 그러면 컴파일러 출력이 이렇게 나온다.

```
CSC : warning MIKA001: 수신 패킷 'S_GatherResultResponse'에 [PacketHandler]가 없습니다.
```

**`dotnet build`는 이 형태를 그대로 출력하지만, Unity는 콘솔에 올리지 않는다.**
Unity는 컴파일러 메시지를 `파일(줄,열): warning ...` 형식으로 파싱해 콘솔 항목을 만드는데,
`CSC :`로 시작하는 위치 없는 줄은 그 패턴에 걸리지 않아 조용히 사라진다.

분석기 DLL·`RoslynAnalyzer` 라벨·Unity 쪽 핸들러 존재는 전부 정상이었다.
**진단 자체는 Unity에서도 발생하고 있었고, 표시만 되지 않았다.**

## 변경 내용

- `PacketHandlerGenerator.Diagnostics.cs`
  - `GetAllPacketTypeNames`가 이름만이 아니라 **선언 위치까지** 모으도록 `PacketRef` 도입.
    소스에 있는 선언(`Location.IsInSource`)만 위치로 채운다.
  - 진단 위치를 ① 패킷 선언 → ② 기존 핸들러 → ③ `Location.None` 순으로 고른다.
- `PacketHandlerGenerator.Handlers.cs`
  - `HandlerInfo`에 `DeclLocation` 추가. 패킷이 참조 어셈블리에 있을 때 쓰는 대체 위치다.
- `MikaSourceGen.csproj`
  - **post-build로 `Assets/Plugins/Analyzers/`에 DLL 자동 복사** (`SyncAnalyzerToUnity`).

## 주요 결정 / 근거

- **대체 위치로 "기존 핸들러의 선언 위치"를 골랐다.**
  Unity는 패킷 정의가 소스(`Assets/Scripts_Server/Protocol`)에 있어 ①로 해결되지만,
  서버 빌드는 패킷이 `MikaProtocol.dll`(참조 어셈블리)이라 위치를 잡을 수 없다.
  핸들러 파일은 **"여기에 핸들러를 추가하라"** 는 뜻이기도 해서 안내 위치로도 알맞다.
- **DLL 복사를 자동화했다.** 지금까지 수동이라 Unity 쪽 DLL이 한 달(6/29 → 7/29) 낡아 있었다.
  이번 증상의 원인은 아니었지만(그 사이 제너레이터 소스는 바뀌지 않았다),
  **"서버에선 경고가 뜨는데 Unity에선 안 뜬다"는 똑같은 증상을 만드는 두 번째 경로**라 함께 막았다.
- `ContinueOnError="true"` — Unity 에디터가 DLL을 잠그고 있으면 복사가 실패하는데,
  그 이유로 서버 빌드까지 깨뜨릴 필요는 없다.

## 검증

핸들러를 일시적으로 주석 처리하고 빌드해 출력 형식을 비교했다.

| | 출력 |
| --- | --- |
| 수정 전 | `CSC : warning MIKA001: ...` (위치 없음 → Unity에서 사라짐) |
| 수정 후 | `...\ServerPacketHandler.cs(10,28): warning MIKA001: ...` |

- 분석기 빌드 오류 0, 솔루션 빌드 오류 0.
- post-build 복사 확인 — Unity DLL과 빌드 산출물의 md5 일치.

## 후속 작업 / 주의사항

- ⚠️ **Unity 콘솔에서 실제로 뜨는지는 확인하지 못했다**(에디터를 띄울 수 없음).
  Unity를 열어 리컴파일하면 **핸들러가 없는 `S_` 패킷 5건**이 뜨는 것이 정상이다:
  `S_PongResponse` · `S_UpdateItemResponse` · `S_WorkStationAssignResponse` ·
  `S_WorkStationSlotsResponse` · `S_GatherResultResponse`
  (Unity 쪽 핸들러는 Login·Inventory·GachaDraw·Echo 4개뿐이다)
- 그래도 안 뜬다면 점검 순서:
  1. `Assets/Plugins/Analyzers/MikaSourceGen.dll.meta`의 `labels: - RoslynAnalyzer`
  2. Unity 콘솔 우상단 **경고 필터**가 켜져 있는지
  3. Unity 콘솔에 분석기 **로드 실패 메시지**가 없는지(Roslyn 버전 불일치)
  4. `Reimport` 또는 `Library/ScriptAssemblies` 삭제 후 전체 리컴파일
- **경고를 오류로 올리고 싶다면** `DiagnosticSeverity.Error`로 바꾸면 되지만,
  Unity에서 컴파일이 막혀 작업 흐름이 끊길 수 있다. 현재는 Warning 유지.
