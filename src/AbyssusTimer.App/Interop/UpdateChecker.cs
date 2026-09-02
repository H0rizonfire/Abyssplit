using System.Net.Http;
using System.Text.Json;

namespace AbyssusTimer.App.Interop;

internal static class UpdateChecker
{
    public static async Task<string?> GetLatestVersionAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Abyssplit-UpdateCheck");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var json = await client.GetStringAsync(AppInfo.GitHubLatestReleaseApiUrl);
            using var document = JsonDocument.Parse(json);
            var tagName = document.RootElement.GetProperty("tag_name").GetString();
            return tagName?.TrimStart('v', 'V');
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException)
        {
            AppLog.LogException("Update check failed (non-fatal)", ex);
            return null;
        }
    }
}
