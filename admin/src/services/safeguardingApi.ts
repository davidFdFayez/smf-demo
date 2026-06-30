import { httpClient } from "./httpClient";

export type SafeguardingCategory =
  | "AntiDoping"
  | "Safeguarding"
  | "Harassment"
  | "MatchFixing"
  | "Other";

export type SafeguardingReportStatus =
  | "Submitted"
  | "UnderReview"
  | "Resolved"
  | "Dismissed";

export interface SafeguardingReportSummary {
  id: string;
  referenceCode: string;
  category: SafeguardingCategory;
  subject: string;
  status: SafeguardingReportStatus;
  isAnonymous: boolean;
  submittedAtUtc: string;
  resolvedAtUtc?: string | null;
}

export interface SafeguardingReportDetails {
  id: string;
  referenceCode: string;
  category: SafeguardingCategory;
  subject: string;
  description: string;
  incidentLocation?: string | null;
  incidentDate?: string | null;
  isAnonymous: boolean;
  reporterName?: string | null;
  reporterEmail?: string | null;
  reporterPhone?: string | null;
  status: SafeguardingReportStatus;
  reviewerNotes?: string | null;
  submittedAtUtc: string;
  resolvedAtUtc?: string | null;
}

export async function listSafeguardingReports(
  status?: SafeguardingReportStatus,
  signal?: AbortSignal,
): Promise<SafeguardingReportSummary[]> {
  const { data } = await httpClient.get<SafeguardingReportSummary[]>(
    "/api/safeguarding/reports",
    { params: status ? { status } : undefined, signal },
  );
  return data;
}

export async function getReportByCode(code: string): Promise<SafeguardingReportDetails> {
  const { data } = await httpClient.get<SafeguardingReportDetails>(
    `/api/safeguarding/reports/by-code/${encodeURIComponent(code)}`,
  );
  return data;
}

export async function triageReport(
  id: string,
  newStatus: SafeguardingReportStatus,
  reviewerNotes?: string,
): Promise<SafeguardingReportDetails> {
  const { data } = await httpClient.post<SafeguardingReportDetails>(
    `/api/safeguarding/reports/${encodeURIComponent(id)}/triage`,
    { newStatus, reviewerNotes },
  );
  return data;
}
