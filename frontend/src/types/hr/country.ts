// Mirrors CountryDto (ErpSystem.Core.DTOs.HR).
export interface Country {
  id: string;
  name: string;
  code: string; // ISO 3166-1 alpha-3
  alpha2Code: string; // ISO 3166-1 alpha-2
  isActive: boolean;
}

// Mirrors CreateCountryDto.
export interface CreateCountryRequest {
  name: string;
  code: string;
  alpha2Code: string;
  isActive: boolean;
}

// Mirrors UpdateCountryDto (adds Id).
export interface UpdateCountryRequest extends CreateCountryRequest {
  id: string;
}
