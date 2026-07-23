import type { AddressFormData } from "../types/address.types";

type AddressFieldsProps = {
  idPrefix: string;
  value: AddressFormData;
  onChange: (value: AddressFormData) => void;
  disabled?: boolean;
  invalidFields?: Partial<Record<keyof AddressFormData, boolean>>;
};

const inputClassName =
  "mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20 disabled:cursor-not-allowed disabled:bg-site-surface-soft disabled:text-site-muted";

export default function AddressFields({
  idPrefix,
  value,
  onChange,
  disabled = false,
  invalidFields = {},
}: AddressFieldsProps) {
  function setField(field: keyof AddressFormData, fieldValue: string) {
    onChange({ ...value, [field]: fieldValue });
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
          className={inputClassName}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.postalCode)}
          required={!disabled}
        />
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
          className={inputClassName}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.city)}
          required={!disabled}
        />
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
          className={inputClassName}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.street)}
          required={!disabled}
        />
      </div>
      <div>
        <label htmlFor={`${idPrefix}-house-number`} className="block text-sm font-semibold text-site-heading">
          Házszám <span className="text-site-danger">*</span>
        </label>
        <input
          id={`${idPrefix}-house-number`}
          value={value.houseNumber}
          onChange={(event) => setField("houseNumber", event.target.value)}
          className={inputClassName}
          disabled={disabled}
          aria-invalid={Boolean(invalidFields.houseNumber)}
          required={!disabled}
        />
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
              className={inputClassName}
              disabled={disabled}
              aria-invalid={Boolean(invalidFields[field])}
            />
          </div>
        );
      })}
    </div>
  );
}

