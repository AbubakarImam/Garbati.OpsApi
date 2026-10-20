using System.Reflection;
using Garbati.OpsApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.OpsApi.Tests.Authorization;

// Guards new endpoints. Unlike every sibling service (one policy per API-key scope), OpsApi enforces
// DESIGN.md Section 4's matrix with ASP.NET Core's own built-in [Authorize(Roles = "...")] - this test's
// job is to confirm every action either opts out explicitly ([AllowAnonymous], only login), accepts any
// authenticated session with no role restriction (bare [Authorize] - only /me and /logout, which Section
// 4's matrix does not govern), or names only roles that actually exist (OpsRoles.All) - not a typo'd role
// string that would silently reject everyone, and not a forgotten attribute that would silently fall back
// to "any authenticated session" for an action the matrix means to restrict.
public class ControllerAuthorizationTests
{
    // The two session endpoints the matrix does not govern (DESIGN.md Section 2.1a) - any authenticated
    // staff member may check who they are or log themselves out, regardless of role.
    private static readonly HashSet<string> BareAuthorizeAllowed = ["SessionController.Me", "SessionController.Logout"];

    [Fact]
    public void EveryControllerAction_HasAKnownAuthorizationRule()
    {
        var actions = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ControllerBase)) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName)
            .ToList();

        var violations = new List<string>();

        foreach (var action in actions)
        {
            var qualifiedName = $"{action.DeclaringType!.Name}.{action.Name}";

            if (action.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            {
                continue;
            }

            var methodAuthorize = action.GetCustomAttributes<AuthorizeAttribute>().ToList();
            var effectiveAuthorize = methodAuthorize.Count > 0
                ? methodAuthorize
                : action.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>().ToList();

            if (effectiveAuthorize.Count == 0)
            {
                violations.Add($"{qualifiedName}: no [Authorize]/[AllowAnonymous] at all.");
                continue;
            }

            foreach (var attribute in effectiveAuthorize)
            {
                if (string.IsNullOrEmpty(attribute.Roles))
                {
                    if (!BareAuthorizeAllowed.Contains(qualifiedName))
                    {
                        violations.Add($"{qualifiedName}: bare [Authorize] with no Roles, and not on the allowed list.");
                    }
                    continue;
                }

                var unknownRoles = attribute.Roles.Split(',').Where(role => !OpsRoles.All.Contains(role)).ToList();
                if (unknownRoles.Count > 0)
                {
                    violations.Add($"{qualifiedName}: unknown role(s) {string.Join(", ", unknownRoles)}.");
                }
            }
        }

        Assert.True(actions.Count >= 10, $"Expected at least 10 controller actions, found {actions.Count}.");
        Assert.Empty(violations);
    }
}
