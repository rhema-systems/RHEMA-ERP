namespace ErpSystem.Core.Exceptions;

/// <summary>
/// Thrown by the succession &amp; talent services when a request collides with a record that already
/// exists — a position that already has an active plan, or a plan number already in use.
/// </summary>
/// <remarks>
/// <para>The API layer maps this to <b>409 Conflict</b> and surfaces the message, so the message
/// must be safe to display and must name the record that caused the collision.</para>
///
/// <para>It exists because these rules were previously enforced <i>only</i> by unique indexes in
/// the database, so they reached the caller as an unhandled <c>DbUpdateException</c> — a 500
/// reading "An unexpected error occurred while processing your request." A rule that fires
/// correctly but cannot explain itself leaves the user staring at a failed save with no idea what
/// to change; see §3.6 of <c>plans/HR-Area-13-Succession-Build-Plan.md</c>.</para>
/// </remarks>
public sealed class SuccessionConflictException : Exception
{
    public SuccessionConflictException(string message) : base(message)
    {
    }

    public SuccessionConflictException(string message, Exception inner) : base(message, inner)
    {
    }
}
