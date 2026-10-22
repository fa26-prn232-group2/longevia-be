using System.Globalization;
using System.Net;
using Longevia.Worker.Adapters.Email;

namespace Longevia.Worker.Templates;

/// <summary>
/// Renders the 4 responsive HTML emails (data-model §2.2) from the event
/// payload ONLY. The Worker MUST NOT aggregate business numbers — the
/// producer prepares every figure (calories, fasting hours, streaks) before
/// publishing; these methods only format them into HTML.
/// All user inputs are HTML-encoded; tokens are URL-encoded into links.
/// </summary>
public static class EmailTemplates
{
    // Gateway base URLs owned by compose (GATEWAY_PORT). The verify path is
    // mandated by data-model §2.2; the reset path follows the same Identity
    // auth-route convention — reconfirm with the Identity feature if its
    // verify/reset routes ever change.
    private const string VerifyBaseUrl = "http://localhost:8080/api/identity/auth/verify";
    private const string ResetBaseUrl = "http://localhost:8080/api/identity/auth/reset-password";

    /// <summary>Account-verification email with an activation link.</summary>
    public static EmailMessage Verification(string toEmail, string toName, string token)
    {
        var link = $"{VerifyBaseUrl}?token={Uri.EscapeDataString(token)}";
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
    /// Password-reset email: OTP code + reset link, valid 15 minutes.
    /// The event carries no display name, so the greeting stays neutral.
    /// </summary>
    public static EmailMessage PasswordReset(string toEmail, string token, DateTimeOffset expiresAt)
    {
        var link = $"{ResetBaseUrl}?token={Uri.EscapeDataString(token)}";
        var body = Layout(
            "Đặt lại mật khẩu Longevia",
            "Chào bạn,",
            $"""
            <p>Bạn vừa yêu cầu đặt lại mật khẩu. Mã OTP của bạn là:</p>
            <p style="text-align:center;font-size:28px;font-weight:bold;letter-spacing:6px;margin:20px 0;">{WebUtility.HtmlEncode(token)}</p>
            <p>Hoặc nhấn nút bên dưới (liên kết hết hạn sau <strong>15 phút</strong>, lúc {expiresAt.UtcDateTime:HH:mm} UTC):</p>
            <p style="text-align:center;margin:24px 0;">
              <a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;padding:12px 28px;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:bold;">Đặt lại mật khẩu</a>
            </p>
            <p style="color:#6b7280;font-size:13px;">Nếu bạn không yêu cầu điều này, hãy bỏ qua email này.</p>
            """);
        return new EmailMessage(toEmail, string.Empty, "Đặt lại mật khẩu Longevia", body);
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
