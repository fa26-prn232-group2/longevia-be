using FluentValidation;

namespace Progress.IntegrationTests;

public sealed class ProbeRequestValidator : AbstractValidator<ProbeRequest>
{
    public ProbeRequestValidator() => RuleFor(request => request.Value).GreaterThan(0);
}
