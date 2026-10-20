namespace Garbati.OpsApi.Configuration;

// DESIGN.md Section 2.2, decision #1: "30-minute idle timeout, 8-hour absolute maximum. Reasonable
// starting values for a small back-office staff, not an architectural commitment - easy to change later."
// Bound from the "Session" configuration section rather than hardcoded literals, exactly per the task
// file's instruction ("make both configurable, not hardcoded literals, so they can be tuned later without
// a code change").
public class OpsSessionOptions
{
    public int IdleTimeoutMinutes { get; set; } = 30;
    public int AbsoluteTimeoutHours { get; set; } = 8;
}
