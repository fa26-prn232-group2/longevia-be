# HANDOFF — Longevia production

Các bước còn lại chỉ dev làm được (quyền hệ thống, DNS, GitHub, firewall).

**Trạng thái 2026-11-13:** production đã deploy tag `v0.1.4`; HTTPS health và
3 readiness endpoint trả 200. Cert đến 2027-02-11. GHCR private pull dùng PAT
trong Environment `production` (không lưu VPS). Còn lại: xác nhận email xác thực
trên Brevo/Gmail, hoàn tất SPF+DKIM cho `mail.hycat.online`, cài cron backup,
lưu `.env.production` vào password manager.

`.env.production` đã có trên VPS; DNS trỏ
`api.longevia.hycat.online → 160.22.107.140`.

## 1. Vercel DNS token (DNS-01)

```bash
ssh vps-hycat
printf '%s' '<VERCEL_API_TOKEN>' > /opt/longevia-secrets/production/dns.token
chmod 600 /opt/longevia-secrets/production/dns.token
exit
```

Nếu domain thuộc team Vercel, thêm biến `VERCEL_TEAM_ID` khi chạy
`issue-certs.sh` (hoặc export trước khi chạy).

Xác nhận:

```bash
ssh vps-hycat "stat -c '%a %U:%G %n' /opt/longevia-secrets/production/dns.token"
```

## 2. GitHub Environment `production`

Settings → Environments → `production` (không reviewer). Secrets:

| Secret | Giá trị |
|---|---|
| `DEPLOY_SSH_KEY` | private key `longevia_github_ed25519` |
| `DEPLOY_HOST` | `160.22.107.140` |
| `DEPLOY_PORT` | `22` |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_KNOWN_HOSTS` | output `ssh-keyscan -p 22 -t ed25519 160.22.107.140` |

## 3. Firewall

Mở TCP `8084` và `8446` trên panel provider. Không mở `5432`, `5672`,
`6379`, `9090`, `3002`.

## 4. GHCR package visibility

Hiện 5 package vẫn **private** (anonymous manifest trả 401). Deploy dùng PAT
riêng trong GitHub Environment `production`:

- Secret `GHCR_READ_TOKEN` — PAT classic, chỉ scope `read:packages`
- Variable `GHCR_USERNAME` — account sở hữu PAT

Workflow login tạm trên VPS bằng `DOCKER_CONFIG=/tmp/longevia-docker`; token
không lưu trên VPS, cleanup sau deploy. Đổi PAT: chỉ cập nhật secret, không cần
SSH VPS.

Nếu muốn bỏ PAT, đổi 5 package sang Public trong GitHub Packages.

## 5. Cấp cert lần đầu

Chạy trên VPS (sau khi đã có `dns.token` và `deployments/` được upload):

```bash
ssh vps-hycat
cd /opt/longevia-production
DOMAIN=api.longevia.hycat.online \
ACME_EMAIL=hycat.support@gmail.com \
DNS_PROVIDER=vercel \
TLS_DIR=/opt/longevia-tls \
sh deployments/scripts/issue-certs.sh issue
```

Kết quả mong đợi:

```text
/opt/longevia-tls/api.longevia.hycat.online/fullchain.pem
/opt/longevia-tls/api.longevia.hycat.online/privkey.pem
```

Kiểm tra hạn:

```bash
openssl x509 -enddate -noout -in /opt/longevia-tls/api.longevia.hycat.online/fullchain.pem
```

## 6. Cron (sau khi deploy ổn định)

```cron
0 2 * * * cd /opt/longevia-production && sh deployments/scripts/backup-db.sh production >> /home/deploy/longevia-backup.log 2>&1
0 3 * * * cd /opt/longevia-production && sh deployments/scripts/renew-certs.sh >> /home/deploy/longevia-cert.log 2>&1
```

Chạy thử từng script bằng tay một lần trước khi tin cron.

## 7. Backup `.env.production`

Lưu bản đã điền vào password manager/team vault. Không commit, không gửi chat.

## 8. Rollback

Nếu release mới lỗi, quay lại digest trước:

```bash
ssh vps-hycat
cd /opt/longevia-production
sh deployments/scripts/rollback.sh production
```

Script dùng `.env.production.deploy.prev`, giữ bản lỗi ở `.env.production.deploy.failed`.

## 9. Kích hoạt deploy

```bash
git tag -a v0.1.0 -m "Release v0.1.0"
git push origin v0.1.0
```

Theo dõi workflow `Release production`, rồi xác nhận:

```bash
curl -fsS https://api.longevia.hycat.online:8446/health
```
