# Game Sync

여러 PC의 게임 세이브 디렉토리를 zip으로 압축해 서버에 올리고, 필요할 때 내려받아 동기화하는 도구입니다.

컴퓨터마다 세이브 경로가 달라도 계정·게임 단위로 매핑해 관리합니다.

## 구성

| 폴더 | GitHub | 설명 |
|------|--------|------|
| [server/](server/) | https://github.com/kendrickkim/game-sync-server | Node.js 20 + Express + SQLite API |
| [client/](client/) | https://github.com/kendrickkim/game-sync-client | .NET 8 WinForms 클라이언트 |

## 주요 기능

- **계정 로그인 / 회원가입** — JWT 인증
- **게임별 로컬 경로 매핑** — PC마다 다른 세이브 폴더 지정 가능
- **백업 제외** — 게임별로 백업에서 빼둘 파일·폴더 지정
- **수동 업로드 / 다운로드** — 업로드마다 기록이 남고, 기록을 골라 다운로드
- **원격 업로드 요청** — 같은 계정으로 로그인된 다른 PC에 업로드 요청
- **트레이 상주** — 창을 닫아도 트레이로 최소화, 중복 실행 시 기존 창 복원

## 빠른 시작

### 1. 서버

```bash
cd server
cp .env.example .env   # 선택 (기본값으로도 동작)
npm install
npm start              # 기본 포트 3000
```

SQLite DB는 `server/data/game_sync.sqlite`에 자동 생성됩니다.

### 2. 클라이언트

```bash
cd client
dotnet run
```

로그인 화면에서 서버 URL 기본값은 `http://localhost:3000`입니다.

## 사용 흐름

1. 클라이언트에서 회원가입·로그인
2. 게임을 추가하고, 이 PC의 세이브 디렉토리를 지정·저장
3. **업로드**로 현재 세이브를 서버에 기록
4. 다른 PC에서 같은 게임에 로컬 경로를 맞춘 뒤, 업로드 기록을 선택해 **다운로드**
5. 원격 PC가 실행 중이면 **원격 업로드 요청**으로 그쪽 세이브를 끌어올 수 있음

## 요구 사항

| 구분 | 요구 사항 |
|------|-----------|
| 서버 | Node.js 20 이상 |
| 클라이언트 | .NET 8 SDK (또는 Desktop Runtime) / Windows |

## 상세 문서

- [서버 README](server/README.md) — 환경 변수, API 목록
- [클라이언트 README](client/README.md) — 빌드·기능·설정 위치
