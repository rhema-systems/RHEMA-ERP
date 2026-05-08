using System;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces
{
    public interface IFinanceSettingsService
    {
        Task<FinanceSettingsDto> GetSettingsAsync();
        Task<FinanceSettingsDto> UpdateSettingsAsync(UpdateFinanceSettingsDto dto);
        Task<bool> CanChangeCOATypeAsync();
    }
}
