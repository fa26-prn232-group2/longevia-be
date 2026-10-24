using Longevia.Worker.Templates;

namespace Worker.UnitTests;

/// <summary>Render asserts for the 4 responsive templates (spec FR-008, T016).</summary>
public sealed class EmailTemplatesTests
{
    [Fact]
    public void Verification_ContainsSubjectLinkAndToken()
    {
        const string token = "verify-token-123";
        var email = EmailTemplates.Verification("user@example.com", "Nguyen Van A", token);

        Assert.Equal("user@example.com", email.ToEmail);
        Assert.Equal("Xác minh tài khoản Longevia", email.Subject);
        Assert.Contains("Kích hoạt tài khoản", email.HtmlBody);
        Assert.Contains($"token={token}", email.HtmlBody);
        Assert.Contains("http://localhost:8080/api/identity/auth/verify", email.HtmlBody);
    }

    [Fact]
    public void Verification_HtmlEncodesUserInput()
    {
        var email = EmailTemplates.Verification("user@example.com", "<script>alert(1)</script>", "t");

        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
    }

    [Fact]
    public void Verification_UrlEncodesToken()
    {
        var email = EmailTemplates.Verification("user@example.com", "A", "a+b/c?d=e&f");

        Assert.Contains($"token={Uri.EscapeDataString("a+b/c?d=e&f")}", email.HtmlBody);
        Assert.DoesNotContain("token=a+b/c?d=e&f", email.HtmlBody);
    }

    [Fact]
    public void PasswordReset_ContainsOtpCodeAndFifteenMinuteWordingWithoutApiLink()
    {
        var expiresAt = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        var email = EmailTemplates.PasswordReset("user@example.com", "reset-token-xyz", expiresAt);

        Assert.Equal("Đặt lại mật khẩu Longevia", email.Subject);
        Assert.Contains("reset-token-xyz", email.HtmlBody);
        Assert.DoesNotContain("/auth/reset-password?token=", email.HtmlBody);
        Assert.Contains("15 phút", email.HtmlBody);
    }

    [Fact]
    public void Welcome_GreetsByName()
    {
        var email = EmailTemplates.Welcome("user@example.com", "Tran Thi B");

        Assert.Equal("Chào mừng đến với Longevia", email.Subject);
        Assert.Contains("Tran Thi B", email.HtmlBody);
    }

    [Fact]
    public void WeeklyDigest_RendersPrecomputedFiguresOnly()
    {
        var data = new WeeklyDigestData("20/10 – 26/10", 1850.4, 32.5, 12);
        var email = EmailTemplates.WeeklyDigest("user@example.com", "Le Van C", data);

        Assert.Contains("Báo cáo tuần", email.Subject);
        Assert.Contains("1850 kcal", email.HtmlBody);
        Assert.Contains("32.5 giờ", email.HtmlBody);
        Assert.Contains("12 ngày", email.HtmlBody);
    }
}
