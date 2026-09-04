using System.Net.Http.Headers;

namespace ReuploadMapToNadeo;

internal sealed class NadeoAccessTokenHandler(string accessToken, HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{
    private readonly string accessToken = string.IsNullOrWhiteSpace(accessToken)
        ? throw new ArgumentException("A Nadeo access token is required.", nameof(accessToken))
        : accessToken.Trim();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("nadeo_v1", $"t={accessToken}");
        return base.SendAsync(request, cancellationToken);
    }
}
