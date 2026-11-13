# DEPLOY REPORT — longevia

Ghi bằng chứng sau mỗi phase/deploy. Không ghi secret.

## Phase 0 — Intake

- Ngày: 2026-11-13
- Kết quả: `DECISIONS.md` + `deployments/profile.env` đã ghi; domain/ports/path/TLS/secret model chốt.
- Bằng chứng: audit read-only `vps-hycat`; DNS `api.longevia.hycat.online → 160.22.107.140`.

## Phase 1 — Local baseline

- Trạng thái: đang thực hiện (compose production, nginx TLS, health/ready, migrate-only).
- Bằng chứng: `dotnet build -c Release` pass; unit tests pass; `docker compose config` production pass.

## Phase 2 — CI/CD

- Trạng thái: đang thực hiện (`ci.yml` build matrix, `release.yml` tag deploy).
- Bằng chứng: YAML parse OK; chờ CI xanh trên PR.

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
