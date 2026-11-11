using System.Globalization;
using System.Net;
using Longevia.Worker.Adapters.Email;

namespace Longevia.Worker.Templates;

/// <summary>
/// Renders the 4 responsive HTML emails (data-model §2.2) from the event
/// payload ONLY. The Worker MUST NOT aggregate business numbers — the
/// producer prepares every figure (calories, fasting hours, streaks) before
/// publishing; these methods only format them into HTML.
/// All user inputs are HTML-encoded; only verification tokens appear in links.
/// </summary>
public static class EmailTemplates
{
    /// <summary>Account-verification email with a frontend activation link.</summary>
    public static EmailMessage Verification(string toEmail, string toName, string token, string frontendBaseUrl)
    {
        var link = $"{frontendBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(token)}";
        var body = Layout(
            "Xác minh tài khoản Longevia",
            $"Chào {WebUtility.HtmlEncode(toName)},",
            $"""
            <p>Cảm ơn bạn đã đăng ký <strong>Longevia</strong>! Nhấn nút bên dưới để kích hoạt tài khoản:</p>
            <p style="text-align:center;margin:24px 0;">
              <a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;padding:12px 28px;background-color:#16a34a;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:bold;">Kích hoạt tài khoản</a>
            </p>
            <p style="color:#6b7280;font-size:13px;">Liên kết hết hạn sau 24 giờ. Nếu nút không hoạt động, sao chép liên kết sau vào trình duyệt:<br/>{WebUtility.HtmlEncode(link)}</p>
            """);
        return new EmailMessage(toEmail, toName, "Xác minh tài khoản Longevia", body);
    }

    /// <summary>
    /// Password-reset email: bấm link hoặc nhập mã dự phòng, hiệu lực 15 phút.
    /// Hiển thị giờ Việt Nam để người dùng không phải tự đổi múi giờ.
    /// </summary>
    public static EmailMessage PasswordReset(string toEmail, string? toName, string token,
        DateTimeOffset expiresAt, string frontendBaseUrl)
    {
        var link = $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";
        var greeting = string.IsNullOrWhiteSpace(toName)
            ? "Chào bạn,"
            : $"Chào {WebUtility.HtmlEncode(toName)},";
        var localExpiry = TimeZoneInfo.ConvertTime(expiresAt, Longevia.Shared.Time.BusinessTime.Resolve(null));

        var body = Layout(
            "Đặt lại mật khẩu Longevia",
            greeting,
            $"""
            <p>Bạn vừa yêu cầu đặt lại mật khẩu. Bấm nút bên dưới để chọn mật khẩu mới:</p>
            <p style="text-align:center;margin:24px 0;">
              <a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;padding:12px 28px;background-color:#16a34a;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:bold;">Đặt lại mật khẩu</a>
            </p>
            <p>Nếu nút không hoạt động, bạn có thể nhập mã dự phòng sau trong ứng dụng Longevia:</p>
            <p style="text-align:center;font-size:20px;font-weight:bold;letter-spacing:4px;margin:16px 0;word-break:break-all;">{WebUtility.HtmlEncode(token)}</p>
            <p>Link và mã hết hạn sau <strong>15 phút</strong>, lúc {localExpiry:HH:mm} ngày {localExpiry:dd/MM/yyyy} (giờ Việt Nam).</p>
            <p style="color:#6b7280;font-size:13px;">Nếu bạn không yêu cầu điều này, hãy bỏ qua email này.</p>
            """);
        return new EmailMessage(toEmail, toName ?? string.Empty, "Đặt lại mật khẩu Longevia", body);
    }

    /// <summary>Welcome email after successful activation.</summary>
    public static EmailMessage Welcome(string toEmail, string toName)
    {
        var body = Layout(
            "Chào mừng đến với Longevia",
            $"Chào mừng {WebUtility.HtmlEncode(toName)}!",
            """
            <p>Tài khoản của bạn đã được kích hoạt. Với <strong>Longevia</strong> bạn có thể:</p>
            <ul>
              <li>Theo dõi chế độ ăn uống và lượng calo mỗi ngày</li>
              <li>Ghi nhận quá trình nhịn ăn gián đoạn (fasting)</li>
              <li>Duy trì chuỗi streak sức khỏe của mình</li>
            </ul>
            <p>Chúc bạn một hành trình khỏe mạnh!</p>
            """);
        return new EmailMessage(toEmail, toName, "Chào mừng đến với Longevia", body);
    }

    /// <summary>
    /// Weekly digest rendered from pre-aggregated <see cref="WeeklyDigestData"/>
    /// only — no queries, no math beyond formatting.
    /// </summary>
    public static EmailMessage WeeklyDigest(string toEmail, string toName, WeeklyDigestData data)
    {
        // Invariant formatting: email content must not depend on server culture.
        var avgCalories = data.AverageCalories.ToString("F0", CultureInfo.InvariantCulture);
        var fastingHours = data.FastingHours.ToString("F1", CultureInfo.InvariantCulture);
        var body = Layout(
            $"Báo cáo tuần {WebUtility.HtmlEncode(data.WeekLabel)}",
            $"Chào {WebUtility.HtmlEncode(toName)}, đây là tổng kết tuần của bạn:",
            $"""
            <table style="width:100%;border-collapse:collapse;margin:16px 0;">
              <tr><td style="padding:10px;border:1px solid #e5e7eb;">Calo trung bình / ngày</td><td style="padding:10px;border:1px solid #e5e7eb;font-weight:bold;">{avgCalories} kcal</td></tr>
              <tr><td style="padding:10px;border:1px solid #e5e7eb;">Tổng giờ nhịn ăn</td><td style="padding:10px;border:1px solid #e5e7eb;font-weight:bold;">{fastingHours} giờ</td></tr>
              <tr><td style="padding:10px;border:1px solid #e5e7eb;">Chuỗi streak</td><td style="padding:10px;border:1px solid #e5e7eb;font-weight:bold;">{data.StreakDays} ngày</td></tr>
            </table>
            <p style="color:#6b7280;font-size:13px;">Số liệu do dịch vụ theo dõi tổng hợp — Worker chỉ hiển thị.</p>
            """);
        return new EmailMessage(toEmail, toName, $"Báo cáo tuần {data.WeekLabel} — Longevia", body);
    }

    /// <summary>Email xác nhận đổi địa chỉ email, gửi tới địa chỉ mới.</summary>
    public static EmailMessage EmailChangeConfirm(string toEmail, string toName, string token,
        DateTimeOffset expiresAt, string frontendBaseUrl)
    {
        var link = $"{frontendBaseUrl.TrimEnd('/')}/confirm-email-change?token={Uri.EscapeDataString(token)}";
        var localExpiry = TimeZoneInfo.ConvertTime(expiresAt,
            Longevia.Shared.Time.BusinessTime.Resolve(null));
        var body = Layout(
            "Xác nhận đổi email Longevia",
            $"Chào {WebUtility.HtmlEncode(toName)},",
            $"""
            <p>Bạn vừa yêu cầu đổi địa chỉ email đăng nhập Longevia sang <strong>{WebUtility.HtmlEncode(toEmail)}</strong>.</p>
            <p style="text-align:center;margin:24px 0;">
              <a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;padding:12px 28px;background-color:#16a34a;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:bold;">Xác nhận đổi email</a>
            </p>
            <p>Link hết hạn sau <strong>24 giờ</strong>, lúc {localExpiry:HH:mm} ngày {localExpiry:dd/MM/yyyy} (giờ Việt Nam).</p>
            <p style="color:#6b7280;font-size:13px;">Nếu không phải bạn, hãy bỏ qua email này; địa chỉ cũ vẫn giữ nguyên.</p>
            """);
        return new EmailMessage(toEmail, toName, "Xác nhận đổi email Longevia", body);
    }

    /// <summary>Cảnh báo tới địa chỉ cũ khi có yêu cầu đổi email.</summary>
    public static EmailMessage EmailChangeWarning(string toEmail, string toName, string newEmail)
    {
        var body = Layout(
            "Có yêu cầu đổi email Longevia",
            $"Chào {WebUtility.HtmlEncode(toName)},",
            $"""
            <p>Tài khoản Longevia của bạn vừa được yêu cầu đổi email đăng nhập sang
              <strong>{WebUtility.HtmlEncode(newEmail)}</strong>.</p>
            <p>Nếu đây là bạn, hãy mở email gửi tới địa chỉ mới để xác nhận. Địa chỉ hiện tại chỉ bị thay
              đổi sau khi bấm link xác nhận.</p>
            <p>Nếu không phải bạn, hãy đổi mật khẩu ngay và liên hệ hỗ trợ Longevia.</p>
            """);
        return new EmailMessage(toEmail, toName, "Có yêu cầu đổi email Longevia", body);
    }

    /// <summary>Thông báo hoàn tất đổi email, gửi tới địa chỉ cũ.</summary>
    public static EmailMessage EmailChanged(string toEmail, string toName, string newEmail)
    {
        var body = Layout(
            "Email Longevia đã được thay đổi",
            $"Chào {WebUtility.HtmlEncode(toName)},",
            $"""
            <p>Địa chỉ email đăng nhập Longevia của bạn đã được đổi thành
              <strong>{WebUtility.HtmlEncode(newEmail)}</strong>.</p>
            <p>Từ giờ hãy đăng nhập bằng địa chỉ mới. Nếu không phải bạn thực hiện, hãy liên hệ hỗ trợ
              Longevia ngay.</p>
            """);
        return new EmailMessage(toEmail, toName, "Email Longevia đã được thay đổi", body);
    }

    private static string Layout(string title, string heading, string content) =>
        $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/><title>{WebUtility.HtmlEncode(title)}</title></head>
        <body style="margin:0;padding:0;background-color:#f3f4f6;font-family:Arial,Helvetica,sans-serif;">
          <div style="max-width:600px;margin:0 auto;padding:24px 16px;">
            <div style="background-color:#ffffff;border-radius:12px;padding:32px 28px;">
              <h1 style="color:#111827;font-size:22px;margin:0 0 16px;">{heading}</h1>
              <div style="color:#374151;font-size:15px;line-height:1.6;">{content}</div>
              <hr style="border:none;border-top:1px solid #e5e7eb;margin:24px 0;"/>
              <p style="color:#9ca3af;font-size:12px;margin:0;">Longevia — email tự động, vui lòng không trả lời.</p>
            </div>
          </div>
        </body>
        </html>
        """;
}
