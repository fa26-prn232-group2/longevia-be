# Tasks close-out

- [x] C001 Audit actual main6df059d, PR13/15/16 merged, CI main success41488082385.
- [x] C002 Ghi ADR005 Accepted và source delegated authority, không claim Huy ký.
- [x] C003 Bộ benchmark actual HTTP/PG/Identity/Rabbit/JWT và evaluator strict được viết.
- [x] C004 Chạy UTC regression mới và hiện hữu trên PG/Broker thật; negative controls.
- [ ] C005 Build/Compose và3valid measured runs; không tick T034 từ scripts.
- [ ] C006 Nếu baseline fail, tối ưu theo số đo + regression + đo lại cùng workload.
- [ ] C007 Full gates, scoped commits/push/newPR, exact finalHEADCI; không merge.

Không đổi REST DTO, proto, event/topology; guides chỉ cập nhật Q01semantics được
ủy quyền. Phần mềm benchmark mới không phải client-facing surface. Generated
credentials/data không stage. T034 pending3valid measured runs; Docker đã hoạt động sau bật SVM/reboot.
Local full solution863/863PASS (project scheduling tuần tự trên host),0skip.
