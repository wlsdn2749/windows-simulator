---
date: 2026-08-02
title: 요청 1:1 응답 패킷에 EResultCode 결과 코드 도입
tags: [server, protocol, error-handling]
---

# 요청 1:1 응답 패킷에 EResultCode 결과 코드 도입

## 목적 / 배경

- "클라 요청에는 성공하든 실패하든 반드시 응답을 남긴다"를 규칙으로 만들기 위해
  응답 구조를 통일했다. 기존에는 `bool Success`가 일부 응답에만 있었고,
  로그인 전 요청(user null)은 응답 없이 무시됐다.
- 실패 이유를 클라가 구분할 수 있도록 bool 대신 결과 코드 enum을 쓴다.

## 변경 내용

- `Server/MikaProtocol/PacketEnum.cs` — `EResultCode : ushort` 추가.
  값 대역: 1~99 공통 / 100~ 가챠 / 200~ 작업슬롯.
- `Server/MikaProtocol/MikaPacket.cs` — 요청 1:1 응답 4종
  (`S_LoginResponse`·`S_UpdateItemResponse`·`S_GachaDrawResponse`·
  `S_WorkStationAssignResponse`)의 `bool Success`를 `EResultCode Result`로 교체
  (첫 프로퍼티). 푸시 패킷(스냅샷·채취 등)에는 넣지 않았다.
- `GachaService.Draw` — 검증을 둘로 나눠 `InvalidDrawCount` / `InvalidGachaId` 구분.
- `User.AssignWorkStation` — `InvalidSlotIndex` / `CharacterNotOwned` / `NoAptitude`
  구분(기존엔 로그로만 구분하던 것을 코드로도 내려 준다).
- `ClientPacketHandler` — AddItem·GachaDraw·WorkStationAssign의 user null 분기가
  이제 `NotLoggedIn`으로 응답한다 (기존엔 무응답).
- 수신부 갱신: `MikaDummyClient/ServerPacketHandler`,
  Unity `Scripts_Server/.../ServerPacketHandler`(로그),
  **`Scripts_Client/Managers/SessionManager`** — 클라 담당 폴더지만 `Success` 필드가
  사라져 컴파일이 깨지므로 `res.Result == EResultCode.Ok` 기계적 치환만 했다.

## 주요 결정 / 근거

- **응답마다 `Result` 프로퍼티 중복 선언** 방식 채택(사용자 선택).
  공용 에러 패킷 분리안은 클라 처리가 성공/실패 두 핸들러로 갈라지고
  요청-응답 매칭이 필요해져 기각. 기본 클래스 상속안은 MemoryPack 와이어 포맷이
  상속 멤버 순서에 묶여 기각. `IResponsePacket` 인터페이스는 보류(추후 도입 가능).
- **규약**: ① 응답의 첫 프로퍼티는 `Result` ② `Result != Ok`면 payload 전부 null,
  클라는 읽지 않는다 ③ 핸들러는 어떤 분기든 반드시 응답을 보낸다.

## 후속 작업 / 주의사항

- MemoryPack은 필드 순서 기반 — 서버·클라 빌드를 같이 갱신해야 한다.
  미러는 빌드 시 자동 동기화됐고 같은 커밋에 담았다.
- `SessionManager`의 실패 UI(에러 토스트 등)에서 `Result` 코드별 메시지 분기는
  클라 담당 몫. 공통 처리 지점이 필요해지면 `IResponsePacket` 인터페이스 도입 검토.
- `C_LoginRequest`는 세션 미연결 시(`User.Login`의 `Destroy` 분기) 여전히 무응답 —
  보낼 세션이 없어서다. DB 조회 실패 경로(`LoginRepository`)의 응답 여부는
  이번에 건드리지 않았다.
