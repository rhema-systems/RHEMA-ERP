using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementTenderAwardSnapshotTests
{
    private const string Approved = "{\"sourceId\":\"award-1\",\"status\":\"Awarded\",\"currency\":\"GHS\",\"amount\":52000,\"lines\":[{\"description\":\"Goods\",\"quantity\":20}]}";
    private static string Activated => Approved.Replace("Awarded", "ContractSigned");
    private static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void OnlyForwardAwardLifecycleChangeMatchesWithoutRewritingStoredEvidence()
    {
        ProcurementTenderAwardSnapshot.IsContractActivationOnlyChange(
            Approved, Hash(Approved).ToLowerInvariant(), Activated, Hash(Activated)).Should().BeTrue();
    }

    [Theory]
    [InlineData("award-1", "award-2")]
    [InlineData("GHS", "USD")]
    [InlineData("52000", "51000")]
    [InlineData("Goods", "Other goods")]
    [InlineData("20}", "21}")]
    [InlineData("ContractSigned", "Cancelled")]
    [InlineData("ContractSigned", "Draft")]
    public void AnyAdditionalCommercialOrLifecycleChangeFails(string before, string after)
    {
        var changed = Activated.Replace(before, after);
        ProcurementTenderAwardSnapshot.IsContractActivationOnlyChange(
            Approved, Hash(Approved), changed, Hash(changed)).Should().BeFalse();
    }

    [Fact]
    public void ReverseTransitionAndTamperedHashesFail()
    {
        ProcurementTenderAwardSnapshot.IsContractActivationOnlyChange(
            Activated, Hash(Activated), Approved, Hash(Approved)).Should().BeFalse();
        ProcurementTenderAwardSnapshot.IsContractActivationOnlyChange(
            Approved, new string('a', 64), Activated, Hash(Activated)).Should().BeFalse();
        ProcurementTenderAwardSnapshot.IsContractActivationOnlyChange(
            Approved, Hash(Approved), Activated, new string('b', 64)).Should().BeFalse();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("[]")]
    [InlineData("{\"status\":1}")]
    [InlineData("{\"status\":\"Awarded\",\"status\":\"Awarded\"}")]
    public void MalformedAmbiguousOrWrongShapeSnapshotsFail(string json)
    {
        ProcurementTenderAwardSnapshot.IsContractActivationOnlyChange(
            json, Hash(json), Activated, Hash(Activated)).Should().BeFalse();
    }
}
