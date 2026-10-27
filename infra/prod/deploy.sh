#!/usr/bin/env bash
# 운영 서버에서 게임 서버 + DB 를 올리는 배포: 비밀값 1회 생성 → 이미지 빌드 → /health 확인
# 사용(서버 root): git -C /opt/ashveil/src pull --ff-only && /opt/ashveil/src/infra/prod/deploy.sh
# pull 을 스크립트 밖에서 하는 까닭: 도는 중인 스크립트 파일이 바뀌면 bash 가 바뀐 자리부터 읽는다.
set -euo pipefail
SRC="$(cd "$(dirname "$0")/../.." && pwd)"
ENV_FILE="${ASHVEIL_ENV:-/opt/ashveil/.env}"

# 비밀값은 서버에서 처음 한 번만 만든다. 이미 있으면 그대로 둔다.
if [ ! -f "$ENV_FILE" ]; then
  # .env 없이 DB 볼륨만 남았으면 새 비밀번호가 볼륨 안 role 과 어긋나 서버가 DB 에 못 붙는다
  if docker volume inspect ashveil-db-data >/dev/null 2>&1; then
    echo "❌ $ENV_FILE 없이 DB 볼륨 ashveil-db-data 만 있다 — 옛 .env 를 되살리거나 볼륨을 지운 뒤 다시"; exit 1
  fi
  (umask 077; cat > "$ENV_FILE" <<EOF
JWT_KEY=$(openssl rand -hex 32)
ADMIN_KEY=$(openssl rand -hex 24)
DB_PASSWORD=$(openssl rand -hex 24)
EOF
  )
  echo "  $ENV_FILE 를 만들었다 (JWT_KEY · ADMIN_KEY · DB_PASSWORD)"
fi

docker compose -f "$SRC/infra/prod/docker-compose.yml" --env-file "$ENV_FILE" up -d --build

for _ in $(seq 1 30); do
  if body=$(curl -fs http://127.0.0.1:18081/health); then
    echo "✅ ashveil-server 기동 $body"; exit 0
  fi
  sleep 2
done
docker logs --tail 50 ashveil-server
echo "❌ 60초 안에 http://127.0.0.1:18081/health 가 200 이 아니다"; exit 1
