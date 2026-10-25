# Progress business capabilities và real Identity Height gRPC

PR #13 giữ Fasting, Water, Sleep, Body Metrics, Dashboard cùng corrective meal
consumer/inbox/reconciliation/UTC hiện hữu. Lượt này thay Height stub trả503 bằng
canonical Identity client thật từ PR #14 đã merge, để POST /metrics lấy profile
height qua HTTP/2, tính BMI và lưu immutable snapshot trong progress_db.

## Đồng bộ và contract

Merge main c679b6f174a3cfcdfc0097b03ee81b3729bb72e2 vào feat/progress-service bằng
merge commit, giữ toàn bộ14 commit Progress trước đó, không rewrite/force-push.
Identity PR14 source72555e139e31e94bc014be4ada97532cd486974c được giữ nguyên;
không sửa Identity implementation, database hoặc canonical proto.

Client codegen GrpcServices=Client dùng contracts/identity/v1/identity.proto:
longevia.identity.v1.IdentityService/GetUserHeight, user_id GUID, optional int32
height_cm_x100. Không nhầm optional unset với0; chia100m giữ17525→175.25cm.
Grpc.Net.Client/Grpc.Tools dùng CPM hiện có; thêm protobuf runtime3.35.1 cùng
version transitive Identity. Không reference Identity assemblies/source.

## Runtime và error semantics

- Singleton channel/client, DI quản lý disposal; actual HTTP/2.
- IdentityGrpc:Address bắt buộc; Compose http://identity:50051.
- DeadlineMilliseconds default2000, range50–10000; không application retry.
  Caller cancellation truyền tới outbound call, không tiếp tục persist.
- NOT_FOUND→404/profile_not_found; unset→422/height_unavailable;
  UNAVAILABLE/DEADLINE_EXCEEDED→503/identity_unavailable; invalid/internal hoặc
  contract-invalid→502/identity_integration_failure. Prefix errorcode là progress.
- GrpcStatus giữ nội bộ; không lộ RpcException detail, URL, profile hoặc credentials.
- Dockerfile COPY canonical proto; CI build Progress image sau make check.
  Identity HTTP1/8080 và HTTP2/50051 giữ nguyên, gRPC không host-publish/qua Nginx.

## Evidence

Height-specific17 scenarios pass: primary dùng actual Identity entry point,
migrations và EF repository, seed/update qua API Identity, PG riêng cho mỗi service,
Progress JWT API + real adapter (không stub). Controlled HTTP2 server chỉ cho optional,
invalid response, fault, deadline/cancellation; không đổi non-null Identity schema.

175cm/70kg→201, stored175/22.86/NormalWeight;175.25 giữwire17525 và decimal BMI.
Update180cm tạo snapshot21.60 mới, record175/22.86 cũ và history không recalculate.
Missing user/profile, invalid height, unavailable, deadline và cancellation không
để lại partial metric. Hai authenticated user dùng đúng profile, không tin body userId.

9 options unit cases mới; scoped Progress84unit+120integration pass, không skip.
Negative controls: stub503 làm primary201 test đỏ; integer division làm175.25 thành
175.00 và test đỏ. Đã restore trước gates. Regression fixtures dùng cùng fixed clock
với host để không phụ thuộc ngày chạy; business logic/validation không nới. Test host chờ MassTransit started (bounded30s) trước short HTTP assertions để tránh startup/disposal race phát hiện ở CI đầu; không skip test hoặc đổi production transport.

Full solution Release **766/766 PASS, 0 skip**. Full format, Debug/Release warnings-as-errors, vulnerability scan27projects gồm transitive đều PASS. Không có package vulnerable theo sources hiện tại.

Docker build và Compose config pass; EF model không pending changes, đủ5 migrations,
không migration mới/sửa migration cũ. JWT/BMI/Fasting/Sleep/Rabbit/reconcile/UTC
regressions vẫn chạy trong full suite. Evidence và môi trường:
[verification.md](verification.md), [backend-integration-guide.md](backend-integration-guide.md).

## Security blocker và Draft

**Functional Height integration: VERIFIED. Authenticated service-to-service trust:
PENDING. Full secure production readiness: BLOCKED.**

ADR001 §2.2 cho phép Compose network trust, chưa có service identity, mTLS hoặc
service token. Actual Identity provider nhận request không credentials; network
isolation không tương đương caller authentication. Frontend JWT pass không phải
authenticated service flow. Không tự thêm hard-coded key hoặc forward user JWT.

Nguyên+Bảo cần phê duyệt rõ trust model hoặc implement provider caller auth,
credential lifecycle và authorized/unauthorized tests trước khi gỡ blocker.
[Handoff Nguyên](handoffs/nguyen-height-auth.md). Q01 acknowledgment Huy, DietPR12
merge/rollout/history completeness và Q10 Weight giữ trạng thái riêng.

Giữ PR13 **OPEN/Draft**. Không merge PR12/13, không pushmain/force-push.

Verdict: HEIGHT_GRPC_FUNCTIONAL_AUTH_BLOCKED.
