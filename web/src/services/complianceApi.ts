import { httpClient } from "./httpClient";

/**
 * Public client for the Compliance & Governance API. Mirrors the surface
 * defined in <c>SMF.Api/Endpoints/ComplianceEndpoints.cs</c>.
 */

export type GovernanceDocumentType =
  | "AnnualReport"
  | "AntiDopingPolicy"
  | "AthleteProtection"
  | "Statutes"
  | "CodeOfConduct"
  | "SafeguardingPolicy"
  | "FinancialReport"
  | "Strategy"
  | "BoardMinutes"
  | "Other";

export type PolicyDocumentKind = "TermsOfService" | "PrivacyPolicy" | "CodeOfConduct";

export type GuardianRelation =
  | "Mother"
  | "Father"
  | "LegalGuardian"
  | "Grandparent"
  | "Sibling"
  | "Other";

export interface GovernanceDocumentDto {
  id: string;
  title: string;
  description: string | null;
  documentType: GovernanceDocumentType;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  sha256: string;
  coveringYear: number | null;
  isPublished: boolean;
  publishedAtUtc: string | null;
  uploadedAtUtc: string;
  downloadUrl: string;
}

export interface PolicyDocumentDto {
  id: string;
  kind: PolicyDocumentKind;
  version: string;
  title: string;
  bodyMarkdown: string;
  contentHash: string;
  isActive: boolean;
  effectiveAtUtc: string;
  createdAtUtc: string;
}

export interface GuardianTaskDto {
  consentId: string;
  memberName: string;
  memberDateOfBirth: string;
  guardianFullName: string;
  relation: GuardianRelation;
  tokenExpiresAtUtc: string;
  policy: PolicyDocumentDto | null;
  isExpired: boolean;
  alreadyDecided: boolean;
}

export interface ParentalConsentDto {
  id: string;
  memberId: string;
  memberName: string;
  status: "Pending" | "Approved" | "Declined" | "Expired" | "Revoked";
  guardianFullName: string;
  relation: GuardianRelation;
  guardianEmail: string;
  guardianPhone: string;
  createdAtUtc: string;
  tokenExpiresAtUtc: string;
  openedAtUtc: string | null;
  decidedAtUtc: string | null;
  declineReason: string | null;
}

export async function listPublicGovernanceDocuments(filter?: {
  type?: GovernanceDocumentType;
  year?: number;
}): Promise<GovernanceDocumentDto[]> {
  const params: Record<string, string | number> = {};
  if (filter?.type) params.type = filter.type;
  if (filter?.year !== undefined) params.year = filter.year;
  const res = await httpClient.get<GovernanceDocumentDto[]>("/api/governance/documents", { params });
  return res.data;
}

export async function getActivePolicies(): Promise<PolicyDocumentDto[]> {
  const res = await httpClient.get<PolicyDocumentDto[]>("/api/policies");
  return res.data;
}

export async function acceptPolicies(memberId: string, policyDocumentIds: string[]): Promise<void> {
  await httpClient.post("/api/policies/accept", { memberId, policyDocumentIds });
}

export async function getGuardianTask(consentId: string, token: string): Promise<GuardianTaskDto | null> {
  try {
    const res = await httpClient.get<GuardianTaskDto>(
      `/api/parental-consent/${consentId}/${encodeURIComponent(token)}`,
    );
    return res.data;
  } catch (err) {
    if ((err as { response?: { status: number } }).response?.status === 404) return null;
    throw err;
  }
}

export async function decideGuardianTask(
  consentId: string,
  token: string,
  approve: boolean,
  declineReason?: string,
): Promise<ParentalConsentDto> {
  const res = await httpClient.post<ParentalConsentDto>(
    `/api/parental-consent/${consentId}/${encodeURIComponent(token)}`,
    { approve, declineReason },
  );
  return res.data;
}
