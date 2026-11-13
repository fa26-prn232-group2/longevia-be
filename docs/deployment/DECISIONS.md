# DECISIONS — longevia (BE)

Nguồn sự thật cho mọi giá trị triển khai. Agent đọc file này trước khi làm bất cứ
gì; không đoán giá trị thiếu. Giá trị máy đọc tương ứng nằm ở
`deployments/profile.env`.

Trạng thái: `Confirmed` (dev đã chốt), `Proposed` (agent đề xuất, chờ chốt),
`Undecided` (chưa có, kèm câu hỏi mở ở §9).

## 1. Dự án

| Mục | Giá trị | Trạng thái |
|---|---|---|
| App slug | `longevia` | Confirmed |
| Repo / owner | `github.com/fa26-prn232-group2/longevia-be` | Confirmed |
| Visibility | **public** | Confirmed |
| Một service / monorepo | monorepo, 5 service | Confirmed |
| Service và stack | `identity`, `diet`, `progress`, `recommendation`, `worker` — .NET 10 / ASP.NET Core | Confirmed |
| Người release | không phân quyền riêng; ai có quyền tag trên repo đều release được | Confirmed |
| Cửa sổ release | không yêu cầu | Confirmed |

Ghi chú: stack được detect từ `src/Services/*` và 5 `Dockerfile`, dev xác nhận
ngày 2026-11-13.

## 2. Môi trường

| Env | Mục đích | Người dùng | Dữ liệu | Provider | Trigger | Approval |
|---|---|---|---|---|---|---|
| `production` | người dùng thật | end user | thật | VPS dùng chung | tag `v*` trên `main` | GitHub Environment `production`, không reviewer |

Ghi chú: dev chốt **1 môi trường duy nhất** ngày 2026-11-13. Playbook yêu cầu
deploy non-production trước; ngoại lệ này được dev chấp thuận và bù bằng các
kiểm soát ở §10.

## 3. Hạ tầng

| Mục | Giá trị | Trạng thái |
|---|---|---|
| VPS provider | chưa xác định (IP public `160.22.107.140`) | Undecided |
| IP / SSH alias | `160.22.107.140` / `vps-hycat` (user `deploy`) | Confirmed |
| OS | Ubuntu 22.04 (kernel 5.15.0-46), x86_64 | Confirmed |
| RAM / disk | 3.8 GiB RAM + 4 GiB swap; disk 40 GB, còn 21 GB | Confirmed |
| Riêng hay dùng chung | **dùng chung với stack khác** | Confirmed |
| Stack khác đang chạy | `closy-prod`, `littlecafe-staging`, `littlecafe-prod`, `artist-shop-prod`, `portainer` | Confirmed |
| Port đang bận | `80, 443, 8081, 8082, 8083, 8443, 8444, 8445`, localhost `5432-5434, 9443` | Confirmed |
| Firewall quản ở đâu | panel provider (chưa xác định provider) | Undecided |
| Proxy | `nginx` (đã có trong repo) | Confirmed |
| TLS model | Let's Encrypt **DNS-01** (lego) | Confirmed |
| ACME email | `hycat.support@gmail.com` | Confirmed |
| DNS provider | **Vercel DNS** (lego provider `vercel`, token `VERCEL_API_TOKEN`, thêm `VERCEL_TEAM_ID` nếu domain thuộc team) | Confirmed |
| Ai giữ token DNS | dev; đặt trên VPS, không vào Git/chat | Confirmed |
| Đường dẫn token DNS | `/opt/longevia-secrets/production/dns.token` (mode 600, chỉ user deploy đọc) | Confirmed |
| `sudo` | `deploy` có sudo nhưng **cần password**; thay đổi hệ thống phải qua handoff | Confirmed |

Ràng buộc: VPS dùng chung nên tuân thủ `reference/shared-vps-safety.md`; không
dùng 80/443 nếu stack khác đang giữ; không restart/prune tài nguyên ngoài prefix
`longevia-`.

## 4. Domain và port

| Env | Domain | HTTP port | HTTPS port | Path | Health URL |
|---|---|---|---:|---:|---|---|
| `production` | `api.longevia.hycat.online` | `8084` | `8446` | `/opt/longevia-production` | `https://api.longevia.hycat.online:8446/health` |

DNS: `hycat.online` dùng nameserver Vercel. `api.longevia.hycat.online` hiện trả
A của Vercel (wildcard) nên **phải tạo A record explicit** trỏ `160.22.107.140`
trước khi verify public. Cert cấp bằng DNS-01 nên không cần port 80/443.

## 5. Database

| Mục | Giá trị | Trạng thái |
|---|---|---|
| Engine / version | PostgreSQL 17 + pgvector (`pgvector/pgvector:pg17`) | Confirmed |
| Mỗi service một DB | `identity_db`, `diet_db`, `progress_db`, `recommendation_db` | Confirmed |
| Mapping `<SERVICE>_DB_NAME` | theo `.env.example` hiện tại | Confirmed |
| Migration tool | EF Core Code First, one-shot `--migrate-only` trước rollout (`Database__AutoMigrate=false`) | Confirmed |
| Seed data | corpus tri thức `resources/knowledge/che-do-an-truong-tho` qua `kb-bootstrap` | Confirmed |
| Backup: nơi lưu | `/opt/longevia-backups/production` + bản ngoài VPS (chưa chọn nơi) | Proposed |
| Backup: tần suất / retention | 02:00 hằng ngày, giữ 7 bản | Proposed |
| RPO / RTO | chưa trả lời | Undecided |
| Ai được restore production | chưa trả lời | Undecided |

Database chạy trong Compose trên VPS (dev chốt 2026-11-13), không dùng managed
DB. Volume phải nằm ngoài thư mục bị CD ghi đè và có backup trước mọi migration
phá huỷ.

## 6. Secret

| Mục | Giá trị | Trạng thái |
|---|---|---|
| Model | `.env.production` đặt **trên VPS** + GitHub Environment secrets chỉ cho credential vận chuyển | Confirmed |
| Đường dẫn trên VPS | `/opt/longevia-production/.env.production` (mode 600, owner deploy) | Confirmed |
| Ai giữ key | dev quản lý nội dung `.env.production`; GitHub giữ `DEPLOY_SSH_KEY`, `DEPLOY_HOST`, `DEPLOY_PORT`, `DEPLOY_USER`, `DEPLOY_KNOWN_HOSTS` | Confirmed |
| Backup env | password manager / team vault (bản mã hoá ngoài VPS) | Confirmed |
| Chu kỳ rotate | chưa trả lời | Undecided |
| Quy trình thu hồi | chưa trả lời | Undecided |

Quy ước: CD **không** tạo, không ghi đè, không in `.env.production`; chỉ ghi
`.env.production.deploy` (image digest) và `.prev` để rollback. Không secret nào
nằm trong Git, image, log hay chat.

## 7. Registry và CI

| Mục | Giá trị | Trạng thái |
|---|---|---|
| Registry | `ghcr.io` | Confirmed |
| Image prefix | `ghcr.io/fa26-prn232-group2/longevia-<service>` | Confirmed |
| Package visibility | public (repo public) | Confirmed |
| PAT read owner | không bắt buộc — image public pull anonymous; chỉ thêm PAT nếu GHCR để private | Confirmed |
| CI gate | `make check` + build matrix 5 image + migration trên DB rỗng | Proposed |
| Branch → env | tag `v*` trên `main` → `production` | Confirmed |

Deploy bằng digest, không dùng `latest`. CI xanh là điều kiện, không phải bằng
chứng deploy.

## 8. Vận hành

| Mục | Giá trị | Trạng thái |
|---|---|---|
| Email production | Brevo SMTP Relay `smtp-relay.brevo.com:587`, StartTLS (`SMTP__USE_SSL=true`), auth bằng `SMTP__USER`/`SMTP__PASSWORD` | Confirmed |
| Email local | Mailpit, không auth; **không** deploy ở production | Confirmed |
| Sender email | `no-reply@mail.hycat.online` — phải verify sender/domain trong Brevo trước khi smoke | Confirmed |
| Brevo API keys | `BREVO__API_KEY`/`BREVO__SENDER_EMAIL` không được code dùng; production đi qua SMTP relay | Confirmed |
| Log retention | Docker `json-file`, `max-size=10m`, `max-file=3` | Confirmed |
| Monitoring / alert | Prometheus + Grafana đã có trong `deployments/`; production chạy profile `monitoring`, Grafana bind `127.0.0.1` | Confirmed |
| Kênh cảnh báo | chưa trả lời | Undecided |
| Cert renewal | cron hằng ngày, đề xuất 03:00, qua `lego` DNS-01 | Proposed |
| Backup cron | 02:00 hằng ngày | Proposed |
| Rollback owner | chưa trả lời | Undecided |

## 9. Câu hỏi mở

| # | Câu hỏi | Ảnh hưởng | Tạm thời |
|---|---|---|---|
| Q1 | Sender email đã verify trong Brevo là gì? | email xác thực/reset, smoke gửi thư | `no-reply@hycat.online` (Proposed) |
| Q2 | Origin frontend chính xác (apex, có `www` không)? | CORS ở nginx, `FRONTEND_BASE_URL` | `https://longevia.hycat.online` (Proposed) |
| Q3 | VPS provider/firewall panel nào? | mở TCP `8084`, `8446` | panel provider |
| Q4 | RPO/RTO và ai được restore production? | backup/restore | 02:00 hằng ngày, giữ 7 bản |
| Q5 | Ai nhận cảnh báo, kênh nào? | vận hành | chưa có |

## 10. Lịch sử quyết định

| Ngày | Quyết định | Lý do | Người chốt |
|---|---|---|---|
| 2026-11-13 | Chỉ 1 môi trường `production` | giảm tài nguyên vận hành cho đồ án | dev |
| 2026-11-13 | VPS dùng chung với stack khác | hạ tầng hiện có | dev |
| 2026-11-13 | TLS Let's Encrypt DNS-01 | không phụ thuộc port 80/443 trên VPS dùng chung | dev |
| 2026-11-13 | Secret: `.env.production` trên VPS + GitHub secrets cho credential CD | tránh nhân đôi secret trong Git | dev |
| 2026-11-13 | PostgreSQL container trong Compose | khớp compose hiện tại, không thêm chi phí managed DB | dev |
| 2026-11-13 | Thay đổi deploy trên nhánh `chore/deploy-*`, không chạy spec-kit | công việc hạ tầng, không đổi contract nghiệp vụ | dev |
| 2026-11-13 | Repo public; GHCR image public, VPS pull không cần PAT | đơn giản hoá CD, không có secret để pull | dev |
| 2026-11-13 | Deploy production bằng tag `v*` trên `main` | mốc release bất biến, có Environment reviewer | dev |
| 2026-11-13 | DNS Vercel + lego provider `vercel` (`VERCEL_API_TOKEN`) | dev đang quản lý DNS tại Vercel; lego hỗ trợ từ v4.7.0 | dev |
| 2026-11-13 | Cho phép agent tạo nhánh `chore/deploy-production` và commit tại gate phase 0 (không push/tag) | giữ thay đổi tách khỏi `main` để review | dev |
| 2026-11-13 | Chấp thuận ngoại lệ "deploy production đầu tiên" cho dự án 1 env | không có env non-production; bù bằng verify local + CI xanh + backup + digest rollback | dev |
| 2026-11-13 | Audit read-only VPS: `160.22.107.140`, Ubuntu 22.04, 3.8 GiB RAM + 4 GiB swap, còn 21 GB đĩa; port trống `8084/8446/5435` | chọn port/path mà không đụng stack khác | agent (dev cho phép) |
| 2026-11-13 | Public ports `8084` (HTTP redirect) + `8446` (HTTPS) | 80/443 đã thuộc `closy-prod` | dev |
| 2026-11-13 | Frontend gọi thẳng API, CORS xử lý ở nginx | không cần Vercel rewrite | dev |
| 2026-11-13 | Email production dùng Brevo SMTP Relay 587/StartTLS; Mailpit chỉ local | đúng dịch vụ email của dự án | dev |
| 2026-11-13 | `.env.production` backup trong password manager/team vault | không commit secret vào repo public | dev |
| 2026-11-13 | Không reviewer, không cửa sổ release | dự án học tập, không có user thật | dev |
| 2026-11-13 | Sender email production `no-reply@mail.hycat.online` | dev cập nhật trong `.env.production` | dev |
| 2026-11-13 | Health chuẩn hoá: `/health` liveness, `/ready` kiểm tra DB; Worker có `/health` | Docker healthcheck không phụ thuộc DB, smoke kiểm tra readiness | dev |
| 2026-11-13 | Migration chạy one-shot `--migrate-only`; app production đặt `Database__AutoMigrate=false` | đúng thứ tự deploy, tránh crash-loop khi DB chậm | dev |
| 2026-11-13 | Nginx production terminate TLS tại `:8446`, CORS allowlist `https://longevia.hycat.online` | frontend gọi thẳng API | dev |
