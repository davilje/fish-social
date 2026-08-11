# ARC-06 / BE-OPT-D QUAL-06: Fish Social server — multi-stage build
# Node major: 20 (node:20-alpine). Keep aligned with local/CI Node 20+.
# Known debt: runner executes TypeScript via tsx (no server tsc emit) — see CHANGELOG.
FROM node:20-alpine AS builder

WORKDIR /app

RUN apk add --no-cache python3 make g++

COPY package.json package-lock.json ./
COPY shared/package.json shared/
COPY server/package.json server/

RUN npm ci --workspace=shared --workspace=server

COPY shared/ shared/
COPY server/ server/

RUN npm run build --workspace=shared

FROM node:20-alpine AS runner

WORKDIR /app

RUN apk add --no-cache python3 make g++ wget

COPY package.json package-lock.json ./
COPY shared/package.json shared/
COPY server/package.json server/

RUN npm ci --workspace=shared --workspace=server --omit=dev \
  && npm install --workspace=server tsx@4.19.2 --no-save \
  && npm cache clean --force

COPY --from=builder /app/shared/dist ./shared/dist
COPY server/src ./server/src
COPY image ./image

RUN mkdir -p /data /app/server/logs

WORKDIR /app/server

ENV NODE_ENV=production
ENV PORT=3001
ENV DB_PATH=/data/fish-social.db
ENV LOG_DIR=

EXPOSE 3001

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 \
  CMD wget -qO- http://127.0.0.1:3001/health || exit 1

CMD ["npx", "tsx", "src/index.ts"]
# QUAL-06: prefer compiling server to dist in a future PR; tsx kept for parity with local dev.
