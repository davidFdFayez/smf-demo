import { httpClient } from "./httpClient";

/**
 * Mirrors SMF.Application.Features.Certificates.CertificateSummary and
 * SMF.Domain.Enums.CertificateType. Enums come back as identifiers
 * thanks to JsonStringEnumConverter on the backend.
 */

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

/** Returns the direct download URL for embedding, etc. */
export function certificateDownloadUrl(id: string): string {
  return `/api/certificates/${encodeURIComponent(id)}/download`;
}
