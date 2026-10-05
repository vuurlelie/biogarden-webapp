export type AdminListQuery = {
  page: number;
  pageSize: number;
  search?: string;
  status?: string | number;
  fulfillment?: string | number;
  role?: string;
  stock?: string;
};

export function adminListQuery(query: AdminListQuery): string {
  const parameters = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== "all" && String(value).trim() !== "") parameters.set(key, String(value).trim());
  }
  return `?${parameters}`;
}
