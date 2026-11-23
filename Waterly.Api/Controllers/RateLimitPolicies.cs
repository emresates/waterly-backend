namespace Waterly.Api.Controllers;

public static class RateLimitPolicies
{
    // Strict limit for login / register / refresh to slow down password guessing.
    public const string Auth = "auth";

    // Mobile achievement events, limited per signed-in user.
    public const string AchievementEvents = "achievementEvents";
}
