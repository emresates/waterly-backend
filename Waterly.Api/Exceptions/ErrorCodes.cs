namespace Waterly.Api.Exceptions;

// Stable, machine readable codes. The mobile app can switch on these values.
public static class ErrorCodes
{
    public const string UserNotFound = "userNotFound";
    public const string EmailAlreadyExists = "emailAlreadyExists";
    public const string InvalidCredentials = "invalidCredentials";
    public const string InvalidToken = "invalidToken";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string IntakeNotFound = "intakeNotFound";
    public const string InvalidWaterAmount = "invalidWaterAmount";
    public const string InvalidConsumedAt = "invalidConsumedAt";
    public const string InvalidTimeZone = "invalidTimeZone";
    public const string InvalidDateRange = "invalidDateRange";
    public const string InvalidReminderSettings = "invalidReminderSettings";
    public const string GoalNotFound = "goalNotFound";
    public const string ReminderSettingsNotFound = "reminderSettingsNotFound";
    public const string ValidationFailed = "validationFailed";
    public const string TooManyRequests = "tooManyRequests";
    public const string InternalServerError = "internalServerError";

    // Achievement system
    public const string AchievementNotFound = "achievementNotFound";
    public const string InvalidAchievementEvent = "invalidAchievementEvent";
    public const string AchievementAlreadyUnlocked = "achievementAlreadyUnlocked";
    public const string RewardNotFound = "rewardNotFound";
    public const string InvalidReward = "invalidReward";
    public const string AchievementEventAlreadyProcessed = "achievementEventAlreadyProcessed";
}
