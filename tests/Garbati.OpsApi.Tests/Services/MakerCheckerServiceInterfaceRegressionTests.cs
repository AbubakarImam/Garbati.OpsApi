using Garbati.OpsApi.Services.Interfaces;

namespace Garbati.OpsApi.Tests.Services;

// D-29 (garbati-platform): external feedback on the maker-checker flow flagged a real class of bug - a
// time-of-check-to-time-of-use race where a checker reviews a pending request, something mutates it
// before they approve, and the checker's approval ends up tied to different content than what they
// actually reviewed. Investigated directly against this repo's real code, not assumed: IReversalRequestService
// and ICustomerEditRequestService each expose only CreateAsync/ListAsync/GetAsync/ApproveAsync/RejectAsync
// - no UpdateAsync or anything like it - and the only writes either entity's row ever receives after
// CreateAsync are the terminal Approve/Reject transitions. There is no method on either interface that can
// mutate a pending request's own substantive fields, so the TOCTOU race D-29 describes has no path to
// exploit today.
//
// This is deliberately a closed-set equality assertion over the full method-name set, not a "doesn't
// contain Update" substring check, so it catches ANY new method added to either interface - not just one
// literally named Edit/Update. If either interface ever grows a new method, this test fails and forces
// whoever added it to make a deliberate decision about whether it reopens the D-29 risk, rather than
// letting the safety property erode silently.
public class MakerCheckerServiceInterfaceRegressionTests
{
    private static readonly HashSet<string> ExpectedMethodNames = new(StringComparer.Ordinal)
    {
        "CreateAsync", "ListAsync", "GetAsync", "ApproveAsync", "RejectAsync",
    };

    [Fact]
    public void IReversalRequestService_ExposesExactlyTheFiveKnownMethods()
    {
        AssertClosedMethodSet(typeof(IReversalRequestService));
    }

    [Fact]
    public void ICustomerEditRequestService_ExposesExactlyTheFiveKnownMethods()
    {
        AssertClosedMethodSet(typeof(ICustomerEditRequestService));
    }

    private static void AssertClosedMethodSet(Type interfaceType)
    {
        var actualMethodNames = new HashSet<string>(
            interfaceType.GetMethods().Select(method => method.Name), StringComparer.Ordinal);

        Assert.True(
            ExpectedMethodNames.SetEquals(actualMethodNames),
            $"{interfaceType.Name}'s method set has changed. Expected exactly " +
            $"[{string.Join(", ", ExpectedMethodNames.OrderBy(name => name))}] but found " +
            $"[{string.Join(", ", actualMethodNames.OrderBy(name => name))}]. A new method here may " +
            "reopen the D-29 TOCTOU concern this test pins (a checker approving content silently changed " +
            "after they reviewed it) if it can mutate a pending request's substantive fields. Confirm that " +
            "isn't the case (or address it explicitly) before updating this test's expected set.");
    }
}
