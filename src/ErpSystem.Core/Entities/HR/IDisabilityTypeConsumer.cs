namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// A record that may name a <see cref="DisabilityType"/> (round 3, lane P2): the employee and the
/// dependant today. The catalogue's delete guard counts every implementer, so a third record that
/// gains the column and forgets to say so cannot be added to the count by accident — it will not
/// compile into <c>DisabilityTypeService.CountInto&lt;T&gt;</c>.
/// </summary>
public interface IDisabilityTypeConsumer
{
    /// <summary>The catalogue row this record names, where it names one.</summary>
    Guid? DisabilityTypeId { get; }
}
