# Deferred — ngoài phạm vi feature `duck-003`

## D-01 — Tự đặt calo/macro theo goal

- **Vấn đề**: hiện `calculate` chỉ tính BMR/TDEE và giữ mục tiêu user chọn; chưa có policy tự giảm/tăng theo `LoseWeight`/`BuildMuscle`.
- **Lý do ngoài phạm vi**: PRD hiện chỉ yêu cầu BMR/TDEE + target thủ công; policy calo/macro theo goal chưa được sản phẩm chốt.
- **Điều kiện mở lại**: owner chốt tỷ lệ giảm/tăng và macro split, sau đó thêm `goal` vào API calculate + UI.

## D-02 — Deploy cloud/CD/TLS production

- Toàn bộ deploy thật (cloud/VPS, domain, TLS/HSTS, CD, secrets manager, backup, HA, Brevo production) nằm trong backlog riêng, không thuộc feature này.

## D-03 — Meal edit/delete

- Cần event v2 + reconciliation với Progress; chưa làm trong đợt này.

## D-04 — Playwright E2E

- Đưa vào backlog theo yêu cầu: chưa thêm Playwright config/bộ test E2E trong đợt này.
- Điều kiện mở lại: stack Docker ổn định, có seed demo; thêm `@playwright/test`, config webServer và bộ smoke auth/diet/progress/AI.

