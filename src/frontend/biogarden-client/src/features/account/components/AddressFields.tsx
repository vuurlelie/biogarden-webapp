import type { AddressFormData } from "../types/address.types";

type AddressFieldsProps = {
  idPrefix: string;
  value: AddressFormData;
  onChange: (value: AddressFormData) => void;
  disabled?: boolean;
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
  invalidFields = {},
  errorMessages = {},
}: AddressFieldsProps) {
  function setField(field: keyof AddressFormData, fieldValue: string) {
    onChange({ ...value, [field]: fieldValue });
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
      <div>
        <label htmlFor={`${idPrefix}-postal-code`} className="block text-sm font-semibold text-site-heading">
          Irányítószám <span className="text-site-danger">*</span>
        </label>
        <input
          id={`${idPrefix}-postal-code`}
          value={value.postalCode}
          onChange={(event) => setField("postalCode", event.target.value)}
          autoComplete={idPrefix === "shipping" ? "shipping postal-code" : "billing postal-code"}
          className={getInputClassName("postalCode")}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.postalCode)}
          aria-describedby={errorMessages.postalCode ? getErrorId("postalCode") : undefined}
          required={!disabled}
        />
        {renderError("postalCode")}
      </div>
      <div className="sm:col-span-1 lg:col-span-3">
        <label htmlFor={`${idPrefix}-city`} className="block text-sm font-semibold text-site-heading">
          Település <span className="text-site-danger">*</span>
        </label>
        <input
          id={`${idPrefix}-city`}
          value={value.city}
          onChange={(event) => setField("city", event.target.value)}
          autoComplete={idPrefix === "shipping" ? "shipping address-level2" : "billing address-level2"}
          className={getInputClassName("city")}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.city)}
          aria-describedby={errorMessages.city ? getErrorId("city") : undefined}
          required={!disabled}
        />
        {renderError("city")}
      </div>
      <div className="sm:col-span-2 lg:col-span-3">
        <label htmlFor={`${idPrefix}-street`} className="block text-sm font-semibold text-site-heading">
          Közterület neve és jellege <span className="text-site-danger">*</span>
        </label>
        <input
          id={`${idPrefix}-street`}
          value={value.street}
          onChange={(event) => setField("street", event.target.value)}
          placeholder="Jókai utca"
          autoComplete={idPrefix === "shipping" ? "shipping address-line1" : "billing address-line1"}
          className={getInputClassName("street")}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.street)}
          aria-describedby={errorMessages.street ? getErrorId("street") : undefined}
          required={!disabled}
        />
        {renderError("street")}
      </div>
      <div>
        <label htmlFor={`${idPrefix}-house-number`} className="block text-sm font-semibold text-site-heading">
          Házszám <span className="text-site-danger">*</span>
        </label>
        <input
          id={`${idPrefix}-house-number`}
          value={value.houseNumber}
          onChange={(event) => setField("houseNumber", event.target.value)}
          className={getInputClassName("houseNumber")}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.houseNumber)}
          aria-describedby={errorMessages.houseNumber ? getErrorId("houseNumber") : undefined}
          required={!disabled}
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

