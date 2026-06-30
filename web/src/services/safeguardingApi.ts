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

export interface SubmitReportInput {
  category: SafeguardingCategory;
  subject: string;
  description: string;
  incidentLocation?: string;
  incidentDate?: string;
  isAnonymous: boolean;
  reporterName?: string;
  reporterEmail?: string;
  reporterPhone?: string;
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

export async function submitSafeguardingReport(
  input: SubmitReportInput,
): Promise<SafeguardingReportDetails> {
  const { data } = await httpClient.post<SafeguardingReportDetails>(
    "/api/safeguarding/reports",
    input,
  );
  return data;
}

export async function getReportByCode(
  code: string,
): Promise<SafeguardingReportDetails> {
  const { data } = await httpClient.get<SafeguardingReportDetails>(
    `/api/safeguarding/reports/by-code/${encodeURIComponent(code)}`,
  );
  return data;
}
