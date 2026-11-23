namespace Waterly.Api.Models;

// Where an intake was created on the phone. Old clients do not send it, so Manual is the default.
public enum IntakeSource
{
    Manual,
    ReminderNotification
}
