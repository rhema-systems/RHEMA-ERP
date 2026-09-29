using ErpSystem.Api.Services;
using ErpSystem.Core.Entities.Procedures;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesMaintenanceCloseoutTests
{
    [Fact]
    public void FailedInspectionBlocksCloseout()
    {
        var procedureCase = Case("Failed", "INS-1", "Satisfied", "Repaired");

        var action = () => ProcedureCaseService.EnsureFacilitiesMaintenanceCloseoutFields(procedureCase);

        action.Should().Throw<InvalidOperationException>().WithMessage("*passed inspection*");
    }

    [Fact]
    public void PassedInspectionNeedsReferenceAndFeedback()
    {
        var procedureCase = Case("Passed", "", "Pending", "Repaired");

        var action = () => ProcedureCaseService.EnsureFacilitiesMaintenanceCloseoutFields(procedureCase);

        action.Should().Throw<InvalidOperationException>().WithMessage("*inspection reference*");
        Set(procedureCase, "inspectionReference", "INS-1");
        action.Should().Throw<InvalidOperationException>().WithMessage("*requester feedback*");
    }

    [Fact]
    public void PassedInspectionFeedbackAndNotesAllowCloseout()
    {
        var procedureCase = Case("Passed", "INS-1", "Satisfied", "Customer accepted repair");

        var action = () => ProcedureCaseService.EnsureFacilitiesMaintenanceCloseoutFields(procedureCase);

        action.Should().NotThrow();
    }

    private static ProcedureCase Case(string inspection, string reference, string feedback, string notes) => new()
    {
        Fields =
        [
            Field("inspectionOutcome", inspection), Field("inspectionReference", reference),
            Field("requesterFeedbackStatus", feedback), Field("closureNotes", notes)
        ]
    };

    private static ProcedureCaseField Field(string key, string value) => new()
    {
        Key = key, Label = key, Value = value
    };

    private static void Set(ProcedureCase procedureCase, string key, string value) =>
        procedureCase.Fields.First(item => item.Key == key).Value = value;
}
