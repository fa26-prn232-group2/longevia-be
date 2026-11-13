# DEPLOY REPORT — longevia

Ghi bằng chứng sau mỗi phase/deploy. Không ghi secret.

## Phase 0 — Intake

- Ngày: 2026-11-13
- Kết quả: `DECISIONS.md` + `deployments/profile.env` đã ghi; domain/ports/path/TLS/secret model chốt.
- Bằng chứng: audit read-only `vps-hycat`; DNS `api.longevia.hycat.online → 160.22.107.140`.

## Phase 1 — Local baseline

- Trạng thái: hoàn thành phần code/compose; chờ CI chạy integration tests.
- Bằng chứng:
  - `dotnet build longevia-be.slnx -c Release` — 0 warning, 0 error.
  - Unit tests: Identity 80, Diet 36, Progress 120, Recommendation 384, Worker 17 — pass.
  - `docker compose --env-file .env.production --env-file <test> -f deployments/docker-compose.production.yml config --quiet` — pass.
  - `sh -n` toàn bộ script deploy/cert/backup — pass.
  - Docker daemon local không chạy nên integration tests (Testcontainers) để CI chạy.

## Phase 2 — CI/CD

- Trạng thái: hoàn thành workflow; chờ push để CI xác nhận.
- Bằng chứng:
  - `ci.yml`: matrix build 5 image.
  - `release.yml`: tag `v*` → gate `make check` → build/push GHCR → SSH deploy → smoke công khai.
  - YAML parse bằng `js-yaml` — pass.

## Phase 3 — VPS bootstrap

- Trạng thái: chờ handoff (Vercel token, firewall, cert).
- Bằng chứng: _(điền sau khi chạy)_

## Phase 4 — First deploy

- Trạng thái: chưa chạy.
- Commit/tag:
- Image digests:
- Smoke:
- Rollback digest:
- Ghi chú:
