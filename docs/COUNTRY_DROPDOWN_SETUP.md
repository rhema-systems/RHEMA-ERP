# Country Dropdown Setup Guide

**Date:** 2025-11-26  
**Feature:** Country dropdown with flag emojis for Business Partner Registration

---

## 📍 **Where is the Country List Set Up?**

### **File Location:**
```
frontend/src/lib/countries.ts
```

This is the **centralized location** for all country data used throughout the application.

---

## 🎨 **Country List Structure**

### **Data Format:**
```typescript
export const COUNTRIES = [
  { code: 'AF', name: 'Afghanistan', flag: '🇦🇫' },
  { code: 'AL', name: 'Albania', flag: '🇦🇱' },
  { code: 'DZ', name: 'Algeria', flag: '🇩🇿' },
  // ... 195 countries total
  { code: 'ZW', name: 'Zimbabwe', flag: '🇿🇼' },
] as const;
```

### **Properties:**
- **`code`** - ISO 3166-1 alpha-2 country code (2 letters)
- **`name`** - Full country name (displayed in dropdown)
- **`flag`** - Unicode flag emoji (🇦🇫, 🇺🇸, etc.)

---

## 🚀 **How to Add/Remove Countries**

### **Add a New Country:**

1. Open `frontend/src/lib/countries.ts`
2. Add a new entry in alphabetical order:

```typescript
{ code: 'XX', name: 'New Country Name', flag: '🇽🇽' },
```

**Example - Adding Kosovo:**
```typescript
{ code: 'XK', name: 'Kosovo', flag: '🇽🇰' },
```

### **Remove a Country:**

1. Open `frontend/src/lib/countries.ts`
2. Delete the entire line for that country

### **Change Country Name:**

1. Find the country in `frontend/src/lib/countries.ts`
2. Update the `name` property:

```typescript
// Before
{ code: 'US', name: 'United States', flag: '🇺🇸' },

// After
{ code: 'US', name: 'United States of America', flag: '🇺🇸' },
```

---

## 🎯 **How Flag Emojis Work**

### **Unicode Regional Indicator Symbols:**

Flag emojis are created using Unicode regional indicator symbols (U+1F1E6 to U+1F1FF).

**Formula:**
```
🇺🇸 = 🇺 (U+1F1FA) + 🇸 (U+1F1F8)
     = Regional Indicator U + Regional Indicator S
```

### **Helper Function:**

The file includes a helper function to generate flag emojis from country codes:

```typescript
export function getFlagEmoji(countryCode: string): string {
  const codePoints = countryCode
    .toUpperCase()
    .split('')
    .map(char => 127397 + char.charCodeAt(0));
  return String.fromCodePoint(...codePoints);
}
```

**Usage:**
```typescript
getFlagEmoji('US') // Returns: 🇺🇸
getFlagEmoji('GB') // Returns: 🇬🇧
getFlagEmoji('CA') // Returns: 🇨🇦
```

---

## 🔧 **Helper Functions**

### **Get Country Name from Code:**

```typescript
export function getCountryName(code: string): string {
  const country = COUNTRIES.find(c => c.code === code);
  return country?.name || code;
}
```

**Usage:**
```typescript
getCountryName('US') // Returns: "United States"
getCountryName('GB') // Returns: "United Kingdom"
```

### **Get Country Code from Name:**

```typescript
export function getCountryCode(name: string): string | undefined {
  const country = COUNTRIES.find(c => c.name === name);
  return country?.code;
}
```

**Usage:**
```typescript
getCountryCode('United States') // Returns: "US"
getCountryCode('United Kingdom') // Returns: "GB"
```

---

## 📦 **Where is it Used?**

### **Current Usage:**

1. **Business Partner Registration - Contact Information Step**
   - File: `frontend/src/components/procurement/registration/ContactInformation.tsx`
   - Line: ~115-146
   - Purpose: Country selection for business address

### **How to Use in Other Components:**

```typescript
import { COUNTRIES, getCountryName, getFlagEmoji } from '@/lib/countries';

// In your component
<Select>
  <SelectTrigger>
    <SelectValue placeholder="Select country" />
  </SelectTrigger>
  <SelectContent className="max-h-[300px]">
    {COUNTRIES.map((country) => (
      <SelectItem key={country.code} value={country.name}>
        <span className="flex items-center gap-2">
          <span className="text-xl">{country.flag}</span>
          <span>{country.name}</span>
        </span>
      </SelectItem>
    ))}
  </SelectContent>
</Select>
```

---

## 🎨 **Customization Options**

### **Change Flag Size:**

Modify the `text-xl` class in the component:

```typescript
// Small flags
<span className="text-base">{country.flag}</span>

// Medium flags (current)
<span className="text-xl">{country.flag}</span>

// Large flags
<span className="text-2xl">{country.flag}</span>
```

### **Change Dropdown Height:**

Modify the `max-h-[300px]` class:

```typescript
// Shorter dropdown
<SelectContent className="max-h-[200px]">

// Taller dropdown
<SelectContent className="max-h-[400px]">
```

### **Add Search/Filter:**

The shadcn/ui Select component already supports keyboard search by default!

**Try it:**
1. Open the country dropdown
2. Start typing a country name (e.g., "United")
3. The list automatically filters to matching countries

---

## 📊 **Statistics**

- **Total Countries:** 195
- **All UN Member States:** ✅ Included
- **Observer States:** ✅ Included (Vatican City, Palestine)
- **Flag Emojis:** ✅ All countries have flags
- **ISO Codes:** ✅ Standard ISO 3166-1 alpha-2

---

## 🔍 **Quick Reference**

| Task | File | Line |
|------|------|------|
| Add/Remove Countries | `frontend/src/lib/countries.ts` | 18-213 |
| Modify Helper Functions | `frontend/src/lib/countries.ts` | 215-227 |
| Update Dropdown UI | `frontend/src/components/procurement/registration/ContactInformation.tsx` | 115-146 |

---

## ✅ **Summary**

✅ **Country list location:** `frontend/src/lib/countries.ts`  
✅ **195 countries** with flags included  
✅ **Easy to add/remove** countries  
✅ **Helper functions** for code/name conversion  
✅ **Flag emojis** automatically generated from ISO codes  
✅ **Searchable dropdown** built-in  
✅ **Reusable** across the entire application  

**The country dropdown is now production-ready with beautiful flag emojis!** 🎉

