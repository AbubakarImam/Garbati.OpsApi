using System.Reflection;
using Garbati.OpsApi.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.OpsApi.Tests.Controllers;

// D-29 (garbati-platform): external feedback on the maker-checker flow flagged a real class of bug - a
// time-of-check-to-time-of-use race where a checker reviews a pending request, something mutates it
// before they approve, and the checker's approval ends up tied to different content than what they
// actually reviewed. Investigated directly against this repo's real code, not assumed: neither
// OpsReversalRequest nor OpsCustomerEditRequest has any edit endpoint today - ReversalRequestsController
// and CustomerEditRequestsController each expose only Create/List/Get/Approve/Reject, and the only writes
// either entity's row ever receives after CreateAsync are the terminal Approve/Reject transitions. There
// is nothing a checker reviews (OriginalTransactionReference/Amount/Currency/ReasonCode for a reversal;
// ProposedFullName/ProposedEmail/ProposedPhoneNumber for a customer edit) that can be mutated once
// created, so the TOCTOU race D-29 describes has no path to exploit today.
//
// This test pins that: if either controller ever grows a PUT/PATCH action, it fails and forces whoever
// added it to make a deliberate decision about whether that reopens the D-29 risk, rather than letting
// the safety property erode silently.
public class MakerCheckerNoEditActionRegressionTests
{
    [Fact]
    public void ReversalRequestsController_HasNoPutOrPatchAction()
    {
        AssertNoPutOrPatchAction(typeof(ReversalRequestsController));
    }

    [Fact]
    public void CustomerEditRequestsController_HasNoPutOrPatchAction()
    {
        AssertNoPutOrPatchAction(typeof(CustomerEditRequestsController));
    }

    private static void AssertNoPutOrPatchAction(Type controllerType)
    {
        var offenders = controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes(true)
                .Any(attribute => attribute is HttpPutAttribute or HttpPatchAttribute))
            .Select(method => method.Name)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"{controllerType.Name} has grown an HttpPut/HttpPatch action ({string.Join(", ", offenders)}). " +
            "That reopens the D-29 TOCTOU concern this test pins: a checker's approval could then be tied " +
            "to a version of the request mutated after they reviewed it. If this addition is deliberate, " +
            "address the TOCTOU risk explicitly (e.g. an optimistic-concurrency/version check the approve " +
            "action must match) and update this test accordingly.");
    }
}
