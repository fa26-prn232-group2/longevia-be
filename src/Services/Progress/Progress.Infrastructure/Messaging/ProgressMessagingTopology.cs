namespace Progress.Infrastructure.Messaging;

public static class ProgressMessagingTopology
{
    public const string EventsExchange = "longevia.events";
    public const string MealActivityRoutingKey = "diet.meal.logged";
    public const string MealActivityQueue = "progress.meal-activity";
    public const string DeadLetterExchange = "progress.meal-activity.dlx";
    public const string DeadLetterQueue = "progress.meal-activity.dlq";

    public const int PrefetchCount = 10;

    public static readonly TimeSpan[] RetryIntervals =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    ];
}
