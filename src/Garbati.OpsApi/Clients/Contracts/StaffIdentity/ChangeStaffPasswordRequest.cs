namespace Garbati.OpsApi.Clients.Contracts.StaffIdentity;

// Mirrors StaffIdentityApi's own single request shape serving both the admin-reset and self-service
// branches of POST /v1/staff/{staffId}/password - which branch applies is decided server-side there by
// which scope the caller's API key holds (OpsApi's own key always holds staffidentity.admin, so every
// call this service makes always takes the admin-reset branch; CurrentPassword/NewPassword are always
// null on this path). Self-service password change by a staff member for their own account is not a
// screen this task builds (DESIGN.md Section 6 item 5 is "Administrator-only").
public record ChangeStaffPasswordRequest(string ActingStaffId, string? CurrentPassword, string? NewPassword);
