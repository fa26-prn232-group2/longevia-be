using Diet.Application.Abstractions;
using Diet.Domain;
using Diet.Domain.Entities;
using Diet.Infrastructure.Grpc;
using Grpc.Core;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Errors;

namespace Diet.IntegrationTests;

public sealed class MealAiClientTests
{
    [Fact]
    public async Task MapsOnlyKnownVisibleCandidatesAndCatalogServing()
    {
        var user = Guid.NewGuid();
        var food = FoodItem.Create(user, "Rice", "Grain", 100, "g", new NutritionValues(130, 3, 28, 1, 1), []);
        var unknown = FoodItem.Create(user, "Unknown", "Grain", 100, "g", new NutritionValues(130, 3, 28, 1, 1));
        var privateFood = FoodItem.Create(Guid.NewGuid(), "Other user", "Grain", 100, "g", new NutritionValues(130, 3, 28, 1, 1), []);
        var reply = new MealRecommendationResponse { Disclaimer = new Disclaimer { Text = "Consult a professional." } };
        var meal = new RecommendedMeal { FoodId = food.Id.ToString(), Name = "Wrong AI name", EstimatedCalories = 999 };
        meal.Citations.Add(new Citation { Quote = "Evidence", SourceFile = "chapter.md", Verified = false });
        reply.Meals.Add(meal);
        var invoker = new ReplyInvoker(reply);
        var provider = new MealRecommendationProvider(new(invoker), TimeProvider.System);
        var context = new MealRecommendationInput(user, 2, 2, new(30, true, 175, 70, "Sedentary", "None", []),
            100, 2000, 100, 200, 50, [food, unknown, privateFood]);
        var result = await provider.GetAsync(context, DateTimeOffset.UtcNow.AddSeconds(3), default);
        Assert.NotNull(invoker.Deadline);
        Assert.True(invoker.Deadline.Value > DateTime.UtcNow.AddSeconds(2.5));
        Assert.Single(invoker.Request!.CandidateFoods);
        Assert.Equal(food.Id.ToString(), invoker.Request.CandidateFoods[0].FoodId);
        var suggestion = Assert.Single(result.Meals);
        Assert.Equal("Rice", suggestion.Name);
        Assert.Equal(100m, suggestion.ServingSize);
        Assert.Equal(130m, suggestion.Calories);
        Assert.False(Assert.Single(suggestion.Citations).Verified);
        reply.Meals[0].Citations.Clear();
        Assert.Equal("diet.ai_integration_failed", (await Assert.ThrowsAsync<AppException>(() =>
            provider.GetAsync(context, DateTimeOffset.UtcNow.AddSeconds(3), default))).Code);
        reply.Meals[0].FoodId = Guid.NewGuid().ToString();
        Assert.Equal("diet.ai_integration_failed", (await Assert.ThrowsAsync<AppException>(() =>
            provider.GetAsync(context, DateTimeOffset.UtcNow.AddSeconds(3), default))).Code);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable, "diet.ai_unavailable")]
    [InlineData(StatusCode.DeadlineExceeded, "diet.ai_timeout")]
    public async Task TransportErrorIsExplicit(StatusCode error, string code)
    {
        var provider = new MealRecommendationProvider(new(new ReplyInvoker(new(), error)), TimeProvider.System);
        var context = new MealRecommendationInput(Guid.NewGuid(), 1, 2, new(30, true, 175, 70, "Sedentary", "None", []),
            0, 2000, 100, 100, 50, []);
        Assert.Equal(code, (await Assert.ThrowsAsync<AppException>(() =>
            provider.GetAsync(context, DateTimeOffset.UtcNow.AddSeconds(3), default))).Code);
    }

    private sealed class ReplyInvoker(MealRecommendationResponse reply, StatusCode? error = null) : CallInvoker
    {
        public DateTime? Deadline { get; private set; }
        public MealRecommendationRequest? Request { get; private set; }
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
            string? host, CallOptions options, TRequest request)
        {
            Deadline = options.Deadline;
            Request = (MealRecommendationRequest)(object)request;
            return new(error.HasValue ? Task.FromException<TResponse>(new RpcException(new Status(error.Value, "private detail"))) : Task.FromResult((TResponse)(object)reply), Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
    }
}
