namespace Radar.Api.Services;

public static class DelayRules
{
    // On time = from 1 minute early to 3 minutes late (see docs/data-notes.md)
    public const int OnTimeEarlyLimitSeconds = -60;
    public const int OnTimeLateLimitSeconds = 180;
    public const int OnTimeLateLimitMinutes = OnTimeLateLimitSeconds / 60;
    public const int RankingMinStops = 50;
    public const int MaxDays = 31;

    public static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(15);

    public static bool IsValidDays(int days) => days >= 1 && days <= MaxDays;
}

