import { httpClient } from "./httpClient";

export interface ScoringTenantDto {
  id: string;
  code: string;
  displayName: string;
  primaryColor: string;
  accentColor: string;
  logoUrl: string | null;
  darkLogoUrl: string | null;
  contactEmail: string;
  customDomain: string | null;
  websiteUrl: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

/**
 * Slim tenant projection returned from <c>/api/tenants/current</c>. The
 * shell uses it to theme itself based on the host the user landed on
 * (custom domain / X-Tenant header / ?tenant= query string).
 */
export interface ResolvedTenantDto {
  id: string;
  code: string;
  displayName: string;
  primaryColor: string;
  accentColor: string;
  logoUrl: string | null;
  customDomain: string | null;
}

export async function getTenantByCode(
  code: string,
  signal?: AbortSignal,
): Promise<ScoringTenantDto | null> {
  try {
    const { data } = await httpClient.get<ScoringTenantDto>(
      `/api/tenants/by-code/${encodeURIComponent(code)}`,
      { signal },
    );
    return data;
  } catch {
    return null;
  }
}

/**
 * Returns the tenant the API resolved for this request. <c>null</c>
 * when no tenant matched (single-tenant deployment) — callers should
 * fall back to default branding.
 */
export async function getCurrentTenant(
  signal?: AbortSignal,
): Promise<ResolvedTenantDto | null> {
  try {
    const res = await httpClient.get<ResolvedTenantDto>("/api/tenants/current", { signal });
    if (res.status === 204) return null;
    return res.data ?? null;
  } catch {
    return null;
  }
}
