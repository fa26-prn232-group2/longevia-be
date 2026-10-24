namespace Diet.Application.Features.Meals;

/// <summary>Tổng ba nhóm chất dinh dưỡng.</summary>
public sealed record MacroSummary(MacroProgress Protein, MacroProgress Carbs, MacroProgress Fat);
