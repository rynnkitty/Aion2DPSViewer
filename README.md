# Aion2DPSViewer

**아이온 2 실시간 DPS 오버레이 뷰어** — .NET Framework 4.8 / WinForms

네트워크 패킷을 캡처하여 아이온 2 전투 중 파티원의 DPS를 실시간으로 화면에 오버레이로 표시하는 도구입니다.

---

## 주요 기능

| 기능 | 설명 |
|---|---|
| **실시간 DPS 측정** | 네트워크 패킷 분석을 통한 개인/파티 DPS 실시간 계산 |
| **스킬별 상세 분석** | 스킬별 타수, 치명타율, 후방타율, 강타율, 다단히트율, 회피/막기 통계 |
| **버프 업타임 추적** | 버프·디버프 유지율(%) 실시간 집계 |
| **전투 기록 저장** | 전투 종료 시 자동 저장 및 히스토리 조회 |
| **파티 정보 연동** | 패킷 기반 파티 구성원 자동 감지 및 동기화 |
| **전투력(CP) 조회** | Plaync API를 통한 캐릭터 전투력·전투점수 자동 조회 |
| **오버레이 모드** | 투명 레이어드 윈도우로 게임 위에 겹쳐 표시 |
| **클릭스루** | 미정 |
| **탭 전환** | 조회(히스토리) / DPS(실시간) 탭 즉시 전환 |
| **서버 자동 감지** | 종굴·아이고·전두력 서버별 자동 연결 |
| **전역 단축키** | 토글/새로고침/컴팩트/탭전환 전역 단축키 설정 |
| **자동 업데이트** | GitHub Releases 기반 자동 업데이트 확인 및 적용 |
| **전투 기록 업로드** | 미정 |

---

## 요구 사항

### 실행 환경

- Windows 10 / 11 (64비트)
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) 이상
- [Npcap](https://npcap.com/) — 패킷 캡처 드라이버 (프로그램 최초 실행 시 자동 설치 안내)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) — UI 렌더링

### 빌드 환경

- Visual Studio 2022 이상
- NuGet 패키지 복원 필요

---

## 빌드 방법

```bash
# 1. 저장소 클론
git clone https://github.com/rynnkitty/Aion2DPSViewer.git
cd Aion2DPSViewer

# 2. NuGet 패키지 복원
nuget restore Aion2DPSViewer.sln

# 3. 빌드 (Release)
msbuild Aion2DPSViewer.sln /p:Configuration=Release
```

또는 Visual Studio 2022에서 `Aion2DPSViewer.sln`을 열고 빌드하면 됩니다.

---

## NuGet 의존성

| 패키지 | 버전 | 용도 |
|---|---|---|
| `Microsoft.Web.WebView2` | 1.0.2849.39 | 오버레이 UI 렌더링 |
| `SharpPcap` | 6.3.0 | 네트워크 패킷 캡처 |
| `PacketDotNet` | 1.4.7 | 패킷 파싱 |
| `System.Text.Json` | 8.0.5 | JSON 직렬화 |
| `System.Memory` | 4.5.5 | 고성능 메모리 버퍼 |
| `System.Buffers` | 4.5.1 | ArrayPool |
| `Microsoft.Bcl.AsyncInterfaces` | 8.0.0 | async/await 인터페이스 |
| `Jint` | 4.1.0 | JavaScript 수식 엔진 (전투점수 계산) |
| `Acornima` | 1.1.0 | JS 파서 (Jint 의존) |

---

## 프로젝트 구조

```
Aion2DPSViewer/
├── Program.cs                  # 진입점, 단일 인스턴스·Npcap 초기화
│
├── Forms/                      # UI 레이어
│   ├── OverlayForm.cs          # 메인 오버레이 폼 (투명 레이어드 윈도우)
│   ├── DpsDetailForm.cs        # 스킬 상세 분석 팝업
│   ├── SettingsForm.cs         # 설정 폼
│   ├── ConsentForm.cs          # 개인정보 동의 폼
│   ├── WebBridge.cs            # WebView2 ↔ C# 메시지 브릿지
│   └── SettingsData.cs         # 설정 데이터 모델
│
├── Dps/                        # DPS 측정 핵심 로직
│   ├── DpsMeter.cs             # DPS 계산 엔진 (패킷 → 통계)
│   ├── ActorDps.cs             # 플레이어별 DPS 스냅샷
│   ├── ActorStats.cs           # 누적 전투 통계
│   ├── SkillDps.cs             # 스킬별 DPS 데이터
│   ├── SkillDamage.cs          # 개별 스킬 피해 레코드
│   ├── BuffTracker.cs          # 버프 업타임 추적기
│   ├── CombatRecord.cs         # 전투 기록 스냅샷
│   ├── CombatRecordStore.cs    # 전투 기록 저장소
│   ├── DpsSnapshot.cs          # 실시간 DPS 상태
│   ├── SkillDatabase.cs        # 스킬/몹/버프 메타데이터 DB
│   ├── JobMapping.cs           # 직업 코드 ↔ 이름 매핑
│   └── PacketLogger.cs         # 패킷 로그 기록
│
├── Packet/                     # 네트워크 패킷 처리
│   ├── PacketSniffer.cs        # Npcap 기반 패킷 캡처
│   ├── PacketProcessor.cs      # 패킷 디코딩·분류
│   ├── PacketDispatcher.cs     # 패킷 타입별 디스패치
│   ├── TcpReassembler.cs       # TCP 스트림 재조립
│   ├── StreamProcessor.cs      # 스트림 처리
│   ├── PartyStreamParser.cs    # 파티 정보 파싱
│   ├── ProtocolUtils.cs        # 프로토콜 유틸리티
│   ├── Lz4Decoder.cs           # LZ4 압축 해제
│   ├── ServerMap.cs            # 서버 ID → 이름 매핑
│   └── DungeonMap.cs           # 던전 ID → 이름 매핑
│
├── Core/                       # 인프라·시스템
│   ├── AppSettings.cs          # 설정 영속화 (JSON)
│   ├── Win32Native.cs          # Win32 API 인터롭
│   ├── HotkeyManager.cs        # 전역 단축키 관리
│   ├── TrayManager.cs          # 시스템 트레이
│   ├── ForegroundWatcher.cs    # 아이온 창 포커스 감지
│   ├── EmbeddedWebServer.cs    # 내장 웹 에셋 서버
│   ├── WebViewHelper.cs        # WebView2 초기화
│   ├── FileCache.cs            # 파일 캐시 관리
│   ├── ErrorReporter.cs        # 오류 로깅·보고
│   ├── NpcapInstaller.cs       # Npcap 설치 관리
│   ├── Updater.cs              # 자동 업데이트
│   └── PartyTracker.cs         # 파티 상태 추적
│
├── Api/                        # 외부 API 연동
│   ├── PlayncClient.cs         # Plaync 캐릭터 조회 API
│   ├── CharacterService.cs     # 캐릭터 데이터 서비스
│   ├── CombatUploader.cs       # 전투 기록 업로드
│   └── CharacterData.cs        # 캐릭터 데이터 모델
│
├── Calc/                       # 전투점수·공식 계산
│   ├── CalcEngine.cs           # 전투점수 계산 엔진 (Jint JS 실행)
│   ├── CombatScore.cs          # 종합 전투점수 산정
│   ├── FormulaConfig.cs        # 계산 공식 설정
│   └── Supplement.cs           # 스탯 보완 계산
│
└── (리소스 데이터)
    ├── mobs.json               # 몹/보스 코드 DB
    ├── skills_db.json          # 스킬 메타데이터 DB (이름·아이콘·타입·직업)
    ├── buffs_ko.json           # 버프·디버프 이름 DB
    ├── overlay.html            # 메인 오버레이 UI
    ├── detail.html             # 스킬 상세 분석 UI
    ├── index.html              # 전투 기록 히스토리 UI
    └── settings.html           # 설정 UI
```

---

## 작동 원리

```
Npcap 패킷 캡처
    │
    ▼
PacketSniffer ──── 서버 포트 자동 감지 / TCP 재조립
    │
    ▼
PacketProcessor ─── 게임 프로토콜 디코딩 (LZ4 압축 해제 포함)
    │
    ├── 피해 패킷 ──────► DpsMeter.OnDamage()
    ├── 버프 패킷 ──────► BuffTracker
    ├── 파티 패킷 ──────► PartyStreamParser → OverlayForm
    └── 몹 정보 패킷 ──► MobInfo / BossHp
                              │
                              ▼
                     DpsMeter.Tick() (200ms)
                              │
                              ▼
                     DpsSnapshot 생성
                              │
                              ▼
                     WebBridge.SendToJs("dps-update")
                              │
                              ▼
                     WebView2 HTML UI 렌더링
```

---

## 설정 파일 위치

```
%APPDATA%\A2Viewer\
├── app_settings.json       # 사용자 설정 (단축키, 스케일 등)
├── window_state.json       # 윈도우 위치/크기
└── a2viewer.log            # 오류 로그
```

---

## 지원 직업

| 코드 | 직업 |
|:---:|---|
| 0 | 검성 |
| 1 | 궁성 |
| 2 | 마도성 |
| 3 | 살성 |
| 4 | 수호성 |
| 5 | 정령성 |
| 6 | 치유성 |
| 7 | 호법성 |

---

## 주의 사항

- 본 프로그램은 **자신의 PC 네트워크 트래픽**만 캡처합니다.
- Npcap 드라이버가 필요하며, **관리자 권한**으로 실행해야 합니다.
- 게임사의 이용약관을 확인 후 사용하세요.
- 전투 기록 업로드 기능은 개인정보 수집 동의 후 사용 가능합니다.

---

## 원본 프로젝트

이 프로젝트는 **A2Viewer** (.NET 8 기반)를 **.NET Framework 4.8 WinForms** 로 변환한 버전입니다.

주요 변환 내용:
- `Microsoft.Web.WebView2` 유지 (net45 호환 버전 사용)
- `System.Text.Json` 8.x NuGet 패키지로 대체
- `Application.SetHighDpiMode` / `SetDefaultFont` 제거 (미지원 API)
- 디컴파일러 아티팩트 수정 (`string.op_Equality` → `==`, `TryGetValue ref` → `out`, `WndProc virtual` → `override` 등)
- .NET 5+ 전용 API 폴리필 적용 (`HMACSHA256.HashData`, `Convert.FromHexString` 등)
- `Esprima` → `Acornima` 패키지 교체 (Jint 4.x 의존성)

---

## 라이선스

이 프로젝트는 개인 학습 및 연구 목적으로 변환되었습니다.
