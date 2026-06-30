import { httpClient } from "./httpClient";

export type CertificateType =
  | "Membership"
  | "Rank"
  | "RefereeLicense"
  | "CoachLicense"
  | "MedicalClearance"
  | "EventParticipation"
  | "CourseCompletion";

export interface CertificateSummary {
  id: string;
  memberId: string;
  type: CertificateType;
  title: string;
  issuingAuthority: string | null;
  verificationCode: string;
  issuedAtUtc: string;
  expiresAtUtc: string | null;
  isRevoked: boolean;
}

export interface IssueCertificateInput {
  memberId: string;
  type: CertificateType;
  title: string;
  issuingAuthority?: string | null;
  expiresAtUtc?: string | null;
}

export async function issueCertificate(
  input: IssueCertificateInput,
  signal?: AbortSignal,
): Promise<CertificateSummary> {
  const { data } = await httpClient.post<CertificateSummary>(
    "/api/certificates",
    input,
    { signal },
  );
  return data;
}

export async function listMemberCertificates(
  memberId: string,
  signal?: AbortSignal,
): Promise<CertificateSummary[]> {
  const { data } = await httpClient.get<CertificateSummary[]>(
    `/api/certificates/by-member/${encodeURIComponent(memberId)}`,
    { signal },
  );
  return data;
}

export async function verifyCertificate(
  code: string,
  signal?: AbortSignal,
): Promise<CertificateSummary> {
  const { data } = await httpClient.get<CertificateSummary>(
    `/api/certificates/verify/${encodeURIComponent(code)}`,
    { signal },
  );
  return data;
}

export function certificateDownloadUrl(id: string): string {
  // The admin app proxies /api to the backend via vite dev server, so
  // linking to the relative URL works both in dev and prod.
  return `/api/certificates/${encodeURIComponent(id)}/download`;
}
