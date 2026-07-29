using ErpSystem.Core.Services.Estate;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public class EstateBoundaryGeometryTests
{
    private const string ParentBoundary =
        """
        [
          { "beacon": "B1", "northing": 0, "easting": 0 },
          { "beacon": "B2", "northing": 0, "easting": 100 },
          { "beacon": "B3", "northing": 100, "easting": 100 },
          { "beacon": "B4", "northing": 100, "easting": 0 }
        ]
        """;

    [Fact]
    public void ValidateContained_AcceptsChildAndCalculatesArea()
    {
        const string child =
            """
            [[10, 10], [10, 40], [30, 40], [30, 10]]
            """;

        var result = EstateBoundaryGeometry.ValidateContained(ParentBoundary, child);

        result.BeaconCount.Should().Be(4);
        result.AreaSquareFeet.Should().Be(600m);
    }

    [Fact]
    public void ValidateContained_RejectsPointOutsideMainCadastral()
    {
        const string child =
            """
            [[10, 10], [10, 110], [30, 40], [30, 10]]
            """;

        var action = () => EstateBoundaryGeometry.ValidateContained(ParentBoundary, child);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*within the main cadastral boundary*");
    }

    [Fact]
    public void ValidateContained_RejectsSelfIntersectingDemarcation()
    {
        const string child =
            """
            [[10, 10], [40, 40], [10, 40], [40, 10]]
            """;

        var action = () => EstateBoundaryGeometry.ValidateContained(ParentBoundary, child);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot cross itself*");
    }

    [Fact]
    public void Overlaps_AllowsSharedBoundaryBetweenNeighbouringParcels()
    {
        const string first =
            """
            [[10, 10], [10, 40], [40, 40], [40, 10]]
            """;
        const string second =
            """
            [[10, 40], [10, 70], [40, 70], [40, 40]]
            """;

        EstateBoundaryGeometry.Overlaps(first, second).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_RejectsIntersectingParcels()
    {
        const string first =
            """
            [[10, 10], [10, 50], [50, 50], [50, 10]]
            """;
        const string second =
            """
            [[30, 30], [30, 70], [70, 70], [70, 30]]
            """;

        EstateBoundaryGeometry.Overlaps(first, second).Should().BeTrue();
    }
}
