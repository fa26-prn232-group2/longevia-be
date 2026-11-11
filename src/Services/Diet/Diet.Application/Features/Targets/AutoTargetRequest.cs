namespace Diet.Application.Features.Targets;

/// <summary>Tính mục tiêu từ TDEE + goal hồ sơ; `Goal` null thì dùng goal trong hồ sơ.</summary>
public sealed record AutoTargetRequest(string? Goal = null);
