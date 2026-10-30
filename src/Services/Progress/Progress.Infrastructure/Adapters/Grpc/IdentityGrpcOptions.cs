namespace Progress.Infrastructure.Adapters.Grpc;

public sealed class IdentityGrpcOptions
{
    public string? Address { get; set; }
    public int DeadlineMilliseconds { get; set; } = 2000;

    public bool IsValid() => DeadlineMilliseconds is >= 50 and <= 10000
        && Uri.TryCreate(Address, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo)
        && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
