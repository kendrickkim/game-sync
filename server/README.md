# Game Sync 서버

게임 세이브 디렉토리 zip과 메타데이터를 **SQLite**에 저장하는 Node.js 20+ Express API입니다.

## 실행

1. `.env.example`을 `.env`로 복사합니다. (선택 — 로컬에서는 기본값으로도 동작합니다.)
2. 설치 후 실행합니다. DB 파일은 `data/game_sync.sqlite`에 자동 생성됩니다.

```bash
npm install
npm start
```

개발 모드(자동 재시작):

```bash
npm run dev
```

## 환경 변수

| 변수 | 기본값 | 설명 |
|------|--------|------|
| `PORT` | `3000` | API 포트 |
| `JWT_SECRET` | (개발용 기본값) | JWT 서명 키 |
| `SQLITE_PATH` | `data/game_sync.sqlite` | SQLite DB 경로 |
| `UPLOAD_DIR` | `uploads` | zip 저장 디렉토리 |

## API 개요

| Method | Path | 인증 | 설명 |
|--------|------|------|------|
| POST | `/auth/register` | 없음 | 회원가입 |
| POST | `/auth/login` | 없음 | 로그인 (JWT 반환) |
| GET/POST | `/games` | 필요 | 게임 목록 / 추가 |
| DELETE | `/games/:id` | 필요 | 게임 삭제 |
| GET/POST | `/computers` | 필요 | 컴퓨터 목록 / 등록 |
| POST | `/computers/heartbeat` | 필요 | 온라인 상태 갱신 |
| GET | `/sync/list` | 필요 | 동기화 기록 목록 (`?gameId=`) |
| POST | `/sync/upload` | 필요 | zip 업로드 (`multipart`: file, gameId, computerName, localPath, contentMtime) |
| GET | `/sync/download/:entryId` | 필요 | zip 다운로드 |
| POST | `/remote-uploads` | 필요 | 같은 계정의 다른 컴퓨터에 업로드 요청 |
| GET | `/remote-uploads/pending` | 필요 | 해당 컴퓨터의 대기 명령 조회 |
| POST | `/remote-uploads/:id/claim` | 필요 | 원격 명령 수락 |
| POST | `/remote-uploads/:id/result` | 필요 | 완료/실패 보고 |

원격 업로드는 인증된 `user_id` 범위로만 동작합니다. 다른 계정의 컴퓨터나 게임을 대상으로 할 수 없으며, 대상 WinForms 클라이언트가 실행 중이어야 폴링·실행이 됩니다.
