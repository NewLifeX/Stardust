using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ClientTest.TestInfrastructure;

internal static class TestEnvironment
{
    public static Boolean CanGet(String url, Int32 timeout = 2_000) => CanGetAsync(url, timeout).GetAwaiter().GetResult();

    public static async Task<Boolean> CanGetAsync(String url, Int32 timeout = 2_000)
    {
        if (String.IsNullOrWhiteSpace(url)) return false;

        try
        {
            using var cts = new CancellationTokenSource(timeout);
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) };
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

            return (Int32)response.StatusCode < 500;
        }
        catch
        {
            return false;
        }
    }
}
