# Game Sync

여러 PC의 게임 세이브 디렉토리를 zip으로 동기화하는 프로젝트입니다.

| 폴더 | 설명 | Git |
|------|------|-----|
| [server/](server/) | Node.js 20+ + **SQLite** API | 별도 repository |
| [client/](client/) | .NET 8 WinForms 클라이언트 | 별도 repository |

## 빠른 시작

1. `cd server && npm install && npm start` (SQLite DB 자동 생성, 포트 3000)
2. `cd client && dotnet run` (서버 URL 기본값 `http://localhost:3000`)

외부 MySQL/MariaDB는 사용하지 않습니다. DB 파일은 `server/data/game_sync.sqlite`에 저장됩니다.

### install in docker container
```bash
apt update
apt install -y build-essential python3

# install nvm 
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.1/install.sh | bash

# restart container
nvm install 20
nvm use 20

# install miot
npm install

# install pm2
npm install -g pm2

```

### container start script
> insert below script to /bin/start script
```bash
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"  # This loads nvm
[ -s "$NVM_DIR/bash_completion" ] && \. "$NVM_DIR/bash_completion"  # This loads nvm bash_completion

pm2 resurrect
```