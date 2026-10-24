using Longevia.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Identity.Api.Extensions;

public static class IdentityValidationFilterExtensions
{
    private const string ValidationErrorCode = "identity.validation_failed";

    public static RouteHandlerBuilder AddIdentityValidation<TRequest>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(new ValidationFilter<TRequest>(
            StatusCodes.Status422UnprocessableEntity,
            ValidationErrorCode));
}
