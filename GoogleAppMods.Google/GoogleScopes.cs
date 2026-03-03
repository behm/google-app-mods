using Google.Apis.Gmail.v1;
using Google.Apis.YouTube.v3;

namespace GoogleAppMods.Google;

public static class GoogleScopes
{
    public static readonly string[] All =
    [
        GmailService.Scope.GmailModify,
        YouTubeService.Scope.Youtube,
    ];
}
