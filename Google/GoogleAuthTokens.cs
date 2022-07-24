using System;
using GooglePhotoSync.Google.Api;

namespace GooglePhotoSync.Google;

public record GoogleAuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresUtc)
{
    private const int _REFRESH_WITHIN_MINUTES = 5;
        
    public bool IsExpiring()
    {
        return (ExpiresUtc - DateTimeOffset.UtcNow).TotalMinutes < _REFRESH_WITHIN_MINUTES;
    }

    public static GoogleAuthTokens FromAuthState(GoogleAuthState authState)
    {
        return new GoogleAuthTokens(authState.AccessToken, authState.RefreshToken, 
                                    DateTimeOffset.UtcNow.AddSeconds(authState.ExpiresIn));

    }
}