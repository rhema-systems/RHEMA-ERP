using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Medical controllers' base. The actor-resolution helpers it used to declare now live on
/// <see cref="HrControllerBase"/>, which area 12 shares; behaviour is unchanged.
/// </summary>
public abstract class MedicalControllerBase : HrControllerBase
{
    protected MedicalControllerBase(ICurrentUserService currentUser) : base(currentUser)
    {
    }
}
