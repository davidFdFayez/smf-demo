import { httpClient } from "./httpClient";

/**
 * Admin client for the Compliance & Governance API. Surface lives in
 * <c>SMF.Api/Endpoints/ComplianceEndpoints.cs</c> — governance documents,
 * policy versions, parental consents, append-only consent log.
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

export type ParentalConsentStatus = "Pending" | "Approved" | "Declined" | "Expired" | "Revoked";

export type GuardianRelation =
  | "Mother"
  | "Father"
  | "LegalGuardian"
  | "Grandparent"
  | "Sibling"
  | "Other";

export type ConsentEventType =
  | "PolicyAccepted"
  | "GuardianLinkIssued"
  | "GuardianLinkOpened"
  | "GuardianApproved"
  | "GuardianDeclined"
  | "LinkExpired"
  | "ConsentRevoked";

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

export interface ParentalConsentDto {
  id: string;
  memberId: string;
  memberName: string;
  status: ParentalConsentStatus;
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

export interface PolicyAcceptanceDto {
  id: string;
  memberId: string;
  memberName: string;
  policyKind: PolicyDocumentKind;
  policyVersion: string;
  contentHash: string;
  acceptedAtUtc: string;
  ipAddress: string | null;
}

export interface ConsentLogDto {
  id: number;
  occurredAtUtc: string;
  eventType: ConsentEventType;
  memberId: string | null;
  policyDocumentId: string | null;
  parentalConsentId: string | null;
  policyKind: string | null;
  policyVersion: string | null;
  ipAddress: string | null;
  detailsJson: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ───────── governance documents ──────────────────────────
export async function listGovernanceDocuments(filter?: {
  type?: GovernanceDocumentType;
  year?: number;
}): Promise<GovernanceDocumentDto[]> {
  const params: Record<string, string | number> = {};
  if (filter?.type) params.type = filter.type;
  if (filter?.year !== undefined) params.year = filter.year;
  const res = await httpClient.get<GovernanceDocumentDto[]>("/api/admin/governance/documents", { params });
  return res.data;
}

export async function uploadGovernanceDocument(form: {
  title: string;
  description?: string;
  documentType: GovernanceDocumentType;
  coveringYear?: number;
  file: File;
}): Promise<GovernanceDocumentDto> {
  const fd = new FormData();
  fd.append("title", form.title);
  if (form.description) fd.append("description", form.description);
  fd.append("documentType", form.documentType);
  if (form.coveringYear !== undefined) fd.append("coveringYear", String(form.coveringYear));
  fd.append("file", form.file);

  const res = await httpClient.post<GovernanceDocumentDto>(
    "/api/admin/governance/documents",
    fd,
    { headers: { "Content-Type": "multipart/form-data" } },
  );
  return res.data;
}

export async function updateGovernanceDocument(
  id: string,
  body: { title: string; description?: string | null; documentType: GovernanceDocumentType; coveringYear?: number | null },
): Promise<GovernanceDocumentDto> {
  const res = await httpClient.put<GovernanceDocumentDto>(`/api/admin/governance/documents/${id}`, body);
  return res.data;
}

export async function publishGovernanceDocument(id: string, publish: boolean): Promise<GovernanceDocumentDto> {
  const url = `/api/admin/governance/documents/${id}/${publish ? "publish" : "unpublish"}`;
  const res = await httpClient.post<GovernanceDocumentDto>(url);
  return res.data;
}

export async function deleteGovernanceDocument(id: string): Promise<void> {
  await httpClient.delete(`/api/admin/governance/documents/${id}`);
}

// ───────── policy versions ───────────────────────────────
export async function listPolicyVersions(kind?: PolicyDocumentKind): Promise<PolicyDocumentDto[]> {
  const res = await httpClient.get<PolicyDocumentDto[]>("/api/admin/policies", { params: kind ? { kind } : {} });
  return res.data;
}

export async function publishPolicyVersion(body: {
  kind: PolicyDocumentKind;
  version: string;
  title: string;
  bodyMarkdown: string;
  effectiveAtUtc?: string;
}): Promise<PolicyDocumentDto> {
  const res = await httpClient.post<PolicyDocumentDto>("/api/admin/policies", body);
  return res.data;
}

export async function listPolicyAcceptances(filter: {
  memberId?: string;
  kind?: PolicyDocumentKind;
  page?: number;
  pageSize?: number;
}): Promise<PagedResult<PolicyAcceptanceDto>> {
  const res = await httpClient.get<PagedResult<PolicyAcceptanceDto>>("/api/admin/policies/acceptances", {
    params: { page: 1, pageSize: 50, ...filter },
  });
  return res.data;
}

// ───────── parental consents + audit log ─────────────────
export async function listParentalConsents(filter: {
  status?: ParentalConsentStatus;
  page?: number;
  pageSize?: number;
}): Promise<PagedResult<ParentalConsentDto>> {
  const res = await httpClient.get<PagedResult<ParentalConsentDto>>("/api/admin/parental-consents", {
    params: { page: 1, pageSize: 50, ...filter },
  });
  return res.data;
}

export async function listConsentLogs(filter: {
  memberId?: string;
  consentId?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResult<ConsentLogDto>> {
  const res = await httpClient.get<PagedResult<ConsentLogDto>>("/api/admin/consent-logs", {
    params: { page: 1, pageSize: 100, ...filter },
  });
  return res.data;
}

export const GOVERNANCE_DOC_TYPES: GovernanceDocumentType[] = [
  "AnnualReport",
  "AntiDopingPolicy",
  "AthleteProtection",
  "Statutes",
  "CodeOfConduct",
  "SafeguardingPolicy",
  "FinancialReport",
  "Strategy",
  "BoardMinutes",
  "Other",
];

export const POLICY_KINDS: PolicyDocumentKind[] = ["TermsOfService", "PrivacyPolicy", "CodeOfConduct"];

export const GUARDIAN_RELATIONS: GuardianRelation[] = [
  "Mother",
  "Father",
  "LegalGuardian",
  "Grandparent",
  "Sibling",
  "Other",
];
