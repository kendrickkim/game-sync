# Game Sync 클라이언트

Game Sync 서버와 연동해 게임 세이브 디렉토리를 동기화하는 .NET 8 WinForms 앱입니다.

## 요구 사항

- .NET 8 SDK 또는 Desktop Runtime
- 실행 중인 Game Sync 서버 (`../server`)

## 빌드 · 실행

```bash
dotnet build
dotnet run
```

## 배포용 빌드

.NET 설치 없이 실행 가능한 단일 exe를 만듭니다.

```bash
dotnet publish GameSync.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -o publish/GameSync-win-x64
```

아이콘은 어셈블리에 임베드되어 있어 단일 파일 배포에서도 그대로 표시됩니다.

## 기능

- 계정 로그인 / 회원가입
- 게임별 로컬 디렉토리 매핑 (설정: `%AppData%/GameSync/config.json`)
- 게임별로 백업에서 제외할 파일·폴더 지정 (디렉토리 선택 후 제외 설정 대화상자)
- 수동 업로드 시 기록 생성 → 기록을 선택해 다운로드
- 같은 계정으로 로그인된 다른 PC에 원격 업로드 요청
- 컴퓨터명은 `Environment.MachineName` 사용
- 중복 실행 시 기존 창에 포커스를 주고, 트레이에 있으면 창을 다시 연다. 창을 닫으면 트레이로 최소화 (트레이 메뉴에서 종료)

클라이언트는 약 10초마다 heartbeat를 보내고 원격 업로드 명령을 폴링합니다.  
원격 업로드를 수행할 PC에는 해당 게임의 로컬 디렉토리가 미리 설정되어 있어야 합니다.
