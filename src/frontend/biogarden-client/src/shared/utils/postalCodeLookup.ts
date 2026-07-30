import { POSTAL_CODE_ENTRIES, type PostalCodeEntry } from "../constants/postalCodeData";

const MAX_CITY_SUGGESTIONS = 250;

function isCustomerAddressPostalCode(code: string, city: string) {
  const numericCode = Number(code);
  if (!Number.isInteger(numericCode) || numericCode < 1011) return false;
  if (city === "Budapest") return numericCode >= 1011 && numericCode <= 1239;
  return true;
}

function normalizeLookupText(value: string) {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase("hu-HU")
    .trim();
}

const customerAddressEntries = POSTAL_CODE_ENTRIES.filter((entry) =>
  isCustomerAddressPostalCode(entry.code, entry.city),
);

const citiesByPostalCode = customerAddressEntries.reduce<Record<string, string[]>>(
  (lookup, entry) => {
    lookup[entry.code] ??= [];
    if (!lookup[entry.code].includes(entry.city)) {
      lookup[entry.code].push(entry.city);
    }
    return lookup;
  },
  {},
);

const searchableEntries = customerAddressEntries.map((entry) => ({
  ...entry,
  normalizedCity: normalizeLookupText(entry.city),
}));

export function getCitiesByPostalCode(postalCode: string) {
  return citiesByPostalCode[postalCode.trim()] ?? [];
}

export function isPostalCodeCityPairValid(postalCode: string, city: string) {
  const normalizedCity = normalizeLookupText(city);
  if (!/^\d{4}$/.test(postalCode.trim()) || normalizedCity.length === 0) {
    return false;
  }

  return getCitiesByPostalCode(postalCode).some(
    (postalCodeCity) => normalizeLookupText(postalCodeCity) === normalizedCity,
  );
}

export function getPostalCodeSuggestions(cityQuery: string, limit = MAX_CITY_SUGGESTIONS): PostalCodeEntry[] {
  const exactCityRequested = /\s$/.test(cityQuery);
  const normalizedQuery = normalizeLookupText(cityQuery);
  if (normalizedQuery.length < 2) return [];

  if (exactCityRequested) {
    return searchableEntries
      .filter((entry) => entry.normalizedCity === normalizedQuery)
      .slice(0, limit)
      .map(({ code, city }) => ({
        code,
        city,
      }));
  }

  const startsWithMatches = searchableEntries.filter((entry) =>
    entry.normalizedCity.startsWith(normalizedQuery),
  );
  const containsMatches = searchableEntries.filter(
    (entry) =>
      !entry.normalizedCity.startsWith(normalizedQuery) &&
      entry.normalizedCity.includes(normalizedQuery),
  );

  return [...startsWithMatches, ...containsMatches].slice(0, limit).map(({ code, city }) => ({
    code,
    city,
  }));
}
