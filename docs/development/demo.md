# Demo script — Longevia MVP

1. `make bootstrap && make docker-up`
2. Mở `http://localhost:3000`.
3. Đăng ký tại `/login?tab=register`; mở Mailpit `http://localhost:8025` lấy link xác minh.
4. Đăng nhập, hoàn tất `/onboarding`.
5. `/diet/targets`: nhập target hoặc tính BMR/TDEE.
6. `/diet`: ghi một bữa ăn.
7. `/diet/suggestions`: tạo gợi ý AI (có thể chậm; kiểm tra `diet.ai_timeout` nếu provider quá hạn).
8. `/diet/meal-plan`: tạo kế hoạch tuần, mở lại để chứng minh đã lưu.
9. `/diet/nutrients`: xem vi chất 7 ngày và cảnh báo thiếu dữ liệu.
10. `/knowledge`: hỏi sách, kiểm tra citation/abstain.
11. `/progress`: xem dashboard; `/progress/workout`, `/progress/longevity`.
12. `/profile`: bật “Nhận báo cáo tuần”; xem toggle lưu qua API.
13. Swagger `http://localhost:8080/swagger`: 32+ endpoint; Prometheus `:9090` target `recommendation` UP; Grafana `:3001`.
14. Kiểm tra rate limit: gọi liên tục `/api/identity/auth/login` đến khi nhận 429 ProblemDetails.
