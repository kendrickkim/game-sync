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

## 기능

- 계정 로그인 / 회원가입
- 게임별 로컬 디렉토리 매핑 (설정: `%AppData%/GameSync/config.json`)
- 수동 업로드 시 기록 생성 → 기록을 선택해 다운로드
- 같은 계정으로 로그인된 다른 PC에 원격 업로드 요청
- 컴퓨터명은 `Environment.MachineName` 사용
- 중복 실행 방지, 창 닫기 시 트레이로 최소화 (트레이 메뉴에서 종료)

클라이언트는 약 10초마다 heartbeat를 보내고 원격 업로드 명령을 폴링합니다.  
원격 업로드를 수행할 PC에는 해당 게임의 로컬 디렉토리가 미리 설정되어 있어야 합니다.
