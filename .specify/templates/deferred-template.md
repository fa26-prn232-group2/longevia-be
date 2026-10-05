# Deferred — {{feature name}}

Công việc phát hiện trong lúc build {{feature name}} nhưng **không thuộc phạm vi
của feature này**. Không mục nào ở đây chặn việc đóng {{feature name}}; không mục
nào là task của {{feature name}}.

File này tồn tại vì một ô chưa tick trong `tasks.md` mang nghĩa khác: `- [ ]`
nghĩa là *feature này chưa xong*. Việc thuộc feature khác, hoặc thuộc hạ tầng
test, không phải là việc chưa xong của feature này — ghi nó vào `tasks.md` sẽ
khiến một feature đã đóng trông như còn mở, hoặc ép điều kiện thoát bị làm sai
lệch.

## Quy tắc cho file này

- Mỗi mục nêu rõ **nó là gì**, **vì sao ngoài phạm vi**, và **gì sẽ gỡ chặn**.
  Mục chỉ nêu triệu chứng thì không được tính là một mục.
- Một mục làm suy yếu bằng chứng của một task đã tick **phải nói rõ và nêu ID
  task đó**. Nếu không, `[X]` bị đọc như vô điều kiện trong khi thực tế không phải.
- Nếu một mục hóa ra thuộc feature này, nó quay lại `tasks.md` thành task thật.
  **File này không phải bãi đỗ.**
- Báo cáo mọi mục trong báo cáo cuối của skill. Âm thầm bỏ qua là vô hiệu mục
  đích ghi lại.

## Các mục

<!--
Định dạng mỗi mục:

### D-nn — <tiêu đề ngắn>

- **Vấn đề**: <cái gì thực sự sai, cụ thể tới mức người đọc tự kiểm chứng được>
- **Vì sao ngoài phạm vi**: <biên thật sự: việc của feature khác, của hạ tầng test
  dùng chung, hay ràng buộc bên thứ ba — không phải "hết thời gian", "khó">
- **Gỡ chặn bằng cách nào**: <bước cụ thể để gỡ, và nó nên nằm ở đâu>
- **Ảnh hưởng tới task đã tick**: <ID task bị suy yếu bằng chứng, hoặc "không">

Xóa khối comment này khi có mục thật đầu tiên.
-->
