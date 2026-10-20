namespace Garbati.OpsApi.Contracts.Requests;

// DESIGN.md Section 2.1: POST /v1/ops/login.
public record LoginRequest(string Username, string Password);
