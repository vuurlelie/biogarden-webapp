import { useState } from "react";
import type { AddressFormData } from "../../types/address.types";
import {
  getCitiesByPostalCode,
  getPostalCodeSuggestions,
} from "../../utils/postalCodeLookup";

type AddressFieldsProps = {
  idPrefix: string;
  value: AddressFormData;
  onChange: (value: AddressFormData) => void;
  disabled?: boolean;
  required?: boolean;
  autoCompleteScope?: "billing" | "shipping";
  invalidFields?: Partial<Record<keyof AddressFormData, boolean>>;
  errorMessages?: Partial<Record<keyof AddressFormData, string>>;
};

const inputClassName =
  "mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20 disabled:cursor-not-allowed disabled:bg-site-surface-soft disabled:text-site-muted";

const invalidInputClassName =
  "mt-2 block w-full rounded-sm border border-site-danger bg-site-danger-soft px-3 py-2.5 text-site-heading outline-none transition focus:border-site-danger focus:ring-2 focus:ring-site-danger/20 disabled:cursor-not-allowed disabled:bg-site-surface-soft disabled:text-site-muted";

export default function AddressFields({
  idPrefix,
  value,
  onChange,
  disabled = false,
  required = true,
  autoCompleteScope = "billing",
  invalidFields = {},
  errorMessages = {},
}: AddressFieldsProps) {
  const [postalCodeCityOptions, setPostalCodeCityOptions] = useState<string[]>([]);
  const [citySuggestions, setCitySuggestions] = useState<ReturnType<typeof getPostalCodeSuggestions>>([]);
  const autoCompletePrefix = autoCompleteScope === "shipping" ? "shipping " : "";

  function setField(field: keyof AddressFormData, fieldValue: string) {
    onChange({ ...value, [field]: fieldValue });
  }

  function applyCity(city: string) {
    setPostalCodeCityOptions([]);
    setCitySuggestions([]);
    onChange({ ...value, city });
  }

  function applyPostalCodeSuggestion(code: string, city: string) {
    setPostalCodeCityOptions([]);
    setCitySuggestions([]);
    onChange({ ...value, postalCode: code, city });
  }

  function handlePostalCodeChange(postalCode: string) {
    const sanitizedPostalCode = postalCode.replace(/\D/g, "").slice(0, 4);
    const nextValue = { ...value, postalCode: sanitizedPostalCode };

    if (sanitizedPostalCode.length === 4) {
      const cities = getCitiesByPostalCode(sanitizedPostalCode);
      if (cities.length === 1) {
        onChange({ ...nextValue, city: cities[0] });
        setPostalCodeCityOptions([]);
        setCitySuggestions([]);
        return;
      }
      setPostalCodeCityOptions(cities);
    } else {
      setPostalCodeCityOptions([]);
    }

    onChange(nextValue);
  }

  function handleCityChange(city: string) {
    setPostalCodeCityOptions([]);
    setCitySuggestions(getPostalCodeSuggestions(city));
    onChange({ ...value, city });
  }

  function getInputClassName(field: keyof AddressFormData) {
    return invalidFields[field] ? invalidInputClassName : inputClassName;
  }

  function getErrorId(field: keyof AddressFormData) {
    return `${idPrefix}-${field}-error`;
  }

  function renderError(field: keyof AddressFormData) {
    if (!errorMessages[field]) return null;

    return (
      <p id={getErrorId(field)} className="mt-1 text-xs font-semibold text-site-danger">
        {errorMessages[field]}
      </p>
    );
  }

  return (
    <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
      <div className="relative">
        <label htmlFor={`${idPrefix}-postal-code`} className="block text-sm font-semibold text-site-heading">
          Irányítószám {required && <span className="text-site-danger">*</span>}
        </label>
        <input
          id={`${idPrefix}-postal-code`}
          value={value.postalCode}
          onChange={(event) => handlePostalCodeChange(event.target.value)}
          onBlur={() => window.setTimeout(() => setPostalCodeCityOptions([]), 100)}
          autoComplete={`${autoCompletePrefix}postal-code`}
          inputMode="numeric"
          maxLength={4}
          className={getInputClassName("postalCode")}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.postalCode)}
          aria-controls={postalCodeCityOptions.length > 0 ? `${idPrefix}-postal-code-options` : undefined}
          aria-expanded={postalCodeCityOptions.length > 0}
          aria-describedby={errorMessages.postalCode ? getErrorId("postalCode") : undefined}
          required={required && !disabled}
        />
        {postalCodeCityOptions.length > 0 && !disabled && (
          <div
            id={`${idPrefix}-postal-code-options`}
            className="absolute z-20 mt-1 max-h-56 w-full overflow-auto rounded-sm border border-site-border bg-white py-1 text-sm shadow-lg"
            role="listbox"
          >
            {postalCodeCityOptions.map((city) => (
              <button
                key={city}
                type="button"
                className="block w-full px-3 py-2 text-left text-site-heading hover:bg-site-surface-soft focus:bg-site-surface-soft focus:outline-none"
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => applyCity(city)}
                role="option"
              >
                {city}
              </button>
            ))}
          </div>
        )}
        {renderError("postalCode")}
      </div>
      <div className="relative sm:col-span-1 lg:col-span-3">
        <label htmlFor={`${idPrefix}-city`} className="block text-sm font-semibold text-site-heading">
          Település {required && <span className="text-site-danger">*</span>}
        </label>
        <input
          id={`${idPrefix}-city`}
          value={value.city}
          onChange={(event) => handleCityChange(event.target.value)}
          onBlur={() => window.setTimeout(() => setCitySuggestions([]), 100)}
          autoComplete={`${autoCompletePrefix}address-level2`}
          className={getInputClassName("city")}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.city)}
          aria-controls={citySuggestions.length > 0 ? `${idPrefix}-city-options` : undefined}
          aria-expanded={citySuggestions.length > 0}
          aria-describedby={errorMessages.city ? getErrorId("city") : undefined}
          required={required && !disabled}
        />
        {citySuggestions.length > 0 && !disabled && (
          <div
            id={`${idPrefix}-city-options`}
            className="absolute z-20 mt-1 max-h-56 w-full overflow-auto rounded-sm border border-site-border bg-white py-1 text-sm shadow-lg"
            role="listbox"
          >
            {citySuggestions.map((suggestion) => (
              <button
                key={`${suggestion.code}-${suggestion.city}`}
                type="button"
                className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-site-heading hover:bg-site-surface-soft focus:bg-site-surface-soft focus:outline-none"
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => applyPostalCodeSuggestion(suggestion.code, suggestion.city)}
                role="option"
              >
                <span>{suggestion.city}</span>
                <span className="font-semibold text-site-green-dark">{suggestion.code}</span>
              </button>
            ))}
          </div>
        )}
        {renderError("city")}
      </div>
      <div className="sm:col-span-2 lg:col-span-3">
        <label htmlFor={`${idPrefix}-street`} className="block text-sm font-semibold text-site-heading">
          Közterület neve és jellege {required && <span className="text-site-danger">*</span>}
        </label>
        <input
          id={`${idPrefix}-street`}
          value={value.street}
          onChange={(event) => setField("street", event.target.value)}
          placeholder="Jókai utca"
          autoComplete={`${autoCompletePrefix}address-line1`}
          className={getInputClassName("street")}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.street)}
          aria-describedby={errorMessages.street ? getErrorId("street") : undefined}
          required={required && !disabled}
        />
        {renderError("street")}
      </div>
      <div>
        <label htmlFor={`${idPrefix}-house-number`} className="block text-sm font-semibold text-site-heading">
          Házszám {required && <span className="text-site-danger">*</span>}
        </label>
        <input
          id={`${idPrefix}-house-number`}
          value={value.houseNumber}
          onChange={(event) => setField("houseNumber", event.target.value)}
          className={getInputClassName("houseNumber")}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.houseNumber)}
          aria-describedby={errorMessages.houseNumber ? getErrorId("houseNumber") : undefined}
          required={required && !disabled}
        />
        {renderError("houseNumber")}
      </div>
      {(["building", "floor", "door", "doorbell"] as const).map((field) => {
        const labels = {
          building: "Épület",
          floor: "Emelet",
          door: "Ajtó",
          doorbell: "Kapucsengő",
        };
        return (
          <div key={field}>
            <label htmlFor={`${idPrefix}-${field}`} className="block text-sm font-semibold text-site-heading">
              {labels[field]}
            </label>
            <input
              id={`${idPrefix}-${field}`}
              value={value[field] ?? ""}
              onChange={(event) => setField(field, event.target.value)}
              className={getInputClassName(field)}
              disabled={disabled}
              aria-invalid={Boolean(invalidFields[field])}
              aria-describedby={errorMessages[field] ? getErrorId(field) : undefined}
            />
            {renderError(field)}
          </div>
        );
      })}
    </div>
  );
}
