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

- Trạng thái: hoàn thành.
- Bằng chứng:
  - `ci.yml`: matrix build 5 image.
  - `release.yml`: tag `v*` → gate `make check` → build/push GHCR → SSH deploy → smoke công khai.
  - YAML parse bằng `js-yaml` — pass.
  - Cache build dùng `type=gha` (tránh lỗi GHCR `write_package` khi export registry cache).
  - Deploy job tự `docker login ghcr.io` bằng `GITHUB_TOKEN` tạm thời qua SSH; xoá `DOCKER_CONFIG=/tmp/longevia-docker` sau deploy.

## Phase 3 — VPS bootstrap

- Trạng thái: hoàn thành.
- Bằng chứng:
  - DNS A `api.longevia.hycat.online → 160.22.107.140`.
  - `deployments/` + `resources/knowledge/che-do-an-truong-tho` pre-seed tại `/opt/longevia-production`.
  - Cert Let's Encrypt DNS-01 (Vercel), hạn `2027-02-11`, `privkey` mode 600.
  - Port `8084`/`8446` reachable từ ngoài; ufw/nftables inactive.
  - `.env.production` 53 key, mode `600 deploy:deploy`; không còn key GHCR.

## Phase 4 — First deploy

- Trạng thái: thành công với tag `v0.1.2`.
- Commit/tag: `ac712b7` / `v0.1.2`
- Image digests (`.env.production.deploy`):
  - identity `sha256:cdb8f39c87d116708d7433ea2f27a7ca4c298b2acf886ee5f6bd9da387184184`
  - diet `sha256:af315d8b73ea97c3398da6b65e955740f3e833622374cc7ac5cd925a89d6893f`
  - progress `sha256:124b9e7aedbc51ceb3fd0c5db9010b5737130419403f9c546cdae87b172272ef`
  - recommendation `sha256:43346dd24742d3e115923f13fa2f8cd5dae1f41b1492755546b4b27472fad8a4`
  - worker `sha256:eb2e13bab438f219f25c03006dbe602f91a67ae09107c35ae0dd4d355ba75566`
- Smoke:
  - `https://api.longevia.hycat.online:8446/health` → 200 `ok`
  - `/api/identity/ready`, `/api/diet/ready`, `/api/progress/ready` → 200
  - CORS preflight → 204 + `Access-Control-Allow-Origin: https://longevia.hycat.online`
  - 5 app container + nginx + postgres/rabbitmq/redis đều healthy
  - `kb-bootstrap`: 13 chapters, 188 chunks, 67 page flags
- Rollback: `.env.production.deploy.prev` giữ digest của lần deploy `v0.1.1`.
- Ghi chú lỗi đã xử lý:
  - `v0.1.0`: GHCR cache export `permission_denied: write_package` → đổi sang `type=gha`.
  - `v0.1.1`: nginx healthcheck `301` sang port 8446 → thêm `location /healthz` trả 200 trên HTTP nội bộ.
  - Flake MassTransit shutdown trong `Progress.IntegrationTests` → re-run CI pass.
- GHCR package hiện vẫn **private** (anonymous manifest trả 401); deploy dùng token tạm, không lưu trên VPS. Để đúng phương án A, đổi 5 package sang Public trong GitHub Packages.

## Deploy v0.1.3 — GHCR read PAT

- Commit/tag: `7af1c43` / `v0.1.3`
- GHCR pull dùng PAT riêng:
  - Environment secret `GHCR_READ_TOKEN` (classic, `read:packages`)
  - Environment variable `GHCR_USERNAME`
- Workflow login tạm trên VPS bằng `DOCKER_CONFIG=/tmp/longevia-docker`; cleanup `if: always()`.
- Kết quả: gate + 5 image + deploy **success**; 5 app container healthy; `/health` 200; CORS 204.
- `.env.production` vẫn 53 key, **0 key GHCR**; `/tmp/longevia-docker` không còn sau deploy.
- Rollback: `.env.production.deploy.prev` giữ digest của `v0.1.2`.

## Deploy v0.1.4 — SMTP binding fix

- Commit/tag: `ad69e8c` / `v0.1.4`
- Nguyên nhân Brevo từ chối: `SMTP__SENDER_EMAIL`/`SMTP__USE_SSL` không bind vào
  `SenderEmail`/`UseSsl` vì binder chuẩn không bỏ dấu `_`; Worker dùng default
  `no-reply@longevia.local`.
- Fix: thêm `[ConfigurationKeyName("SENDER_EMAIL")]` và
  `[ConfigurationKeyName("USE_SSL")]` trong `SmtpOptions`; thêm unit test bind.
- Kết quả release: gate + 5 image + deploy **success**; Worker healthy.
- Resend verification: `POST /api/identity/auth/resend-verification` → 202;
  Worker log `Verification email dispatched`.
- Cần xác nhận trên Brevo log sender là `no-reply@mail.hycat.online` và Gmail
  nhận được (kiểm tra cả Spam).



