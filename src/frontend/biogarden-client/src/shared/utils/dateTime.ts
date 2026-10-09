export const SITE_TIME_ZONE = "Europe/Budapest";

// SQL datetime2 values from older API responses may omit the UTC suffix.
export function parseApiDate(value: string): Date {
  return new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(value) ? value : `${value}Z`);
}

export function budapestDateTimeInput(value?: string | null): string {
  const parts = new Intl.DateTimeFormat(
    "en-GB",
    {
      timeZone: SITE_TIME_ZONE, year: "numeric", month: "2-digit", day: "2-digit",
      hour: "2-digit", minute: "2-digit", hourCycle: "h23",
    }
  ).formatToParts(value ? parseApiDate(value) : new Date());
  const part = (name: string) => parts.find(item => item.type === name)!.value;
  return `${part("year")}-${part("month")}-${part("day")}T${part("hour")}:${part("minute")}`;
}

export function budapestInputToUtc(value: string): string | null {
  const wallTime = Date.parse(`${value}Z`);
  if (!Number.isFinite(wallTime))
    return null;
  // Budapest uses UTC+1 in winter and UTC+2 in summer. Reject missing or
  // ambiguous clock-change times instead of silently choosing another instant.
  const candidates = [1, 2].map(offset => new Date(wallTime - offset * 3_600_000).toISOString())
    .filter(instant => budapestDateTimeInput(instant) === value);
  return candidates.length === 1 ? candidates[0] : null;
}
