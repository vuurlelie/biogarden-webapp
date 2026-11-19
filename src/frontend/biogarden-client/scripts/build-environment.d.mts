export function resolveBuildEnvironment(mode: string, directory?: string): Record<string, string | undefined> & {
  VITE_API_BASE_URL: string;
  VITE_SITE_URL: string;
  VITE_INDEXING_ENABLED: string;
};
