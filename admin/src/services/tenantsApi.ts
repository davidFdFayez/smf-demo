import { httpClient } from "./httpClient";

export interface ScoringTenantDto {
  id: string;
  code: string;
  displayName: string;
  primaryColor: string;
  accentColor: string;
  logoUrl: string | null;
  contactEmail: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface ProvisionTenantInput {
  code: string;
  displayName: string;
  primaryColor: string;
  accentColor: string;
  logoUrl?: string;
  contactEmail: string;
}

export interface UpdateTenantInput {
  displayName: string;
  primaryColor: string;
  accentColor: string;
  logoUrl?: string;
  contactEmail: string;
}

export async function listTenants(signal?: AbortSignal): Promise<ScoringTenantDto[]> {
  const { data } = await httpClient.get<ScoringTenantDto[]>("/api/tenants", { signal });
  return data;
}

export async function provisionTenant(input: ProvisionTenantInput): Promise<ScoringTenantDto> {
  const { data } = await httpClient.post<ScoringTenantDto>("/api/tenants", input);
  return data;
}

export async function updateTenant(
  id: string,
  input: UpdateTenantInput,
): Promise<ScoringTenantDto> {
  const { data } = await httpClient.put<ScoringTenantDto>(`/api/tenants/${id}`, input);
  return data;
}

export async function setTenantActive(id: string, isActive: boolean): Promise<void> {
  await httpClient.post(`/api/tenants/${id}/active`, { isActive });
}
