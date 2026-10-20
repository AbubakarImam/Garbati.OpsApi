namespace Garbati.OpsApi.Clients.Contracts.StaffIdentity;

public record StaffListResponse(IReadOnlyList<StaffResponse> Items, int Page, int PageSize, int TotalCount);
