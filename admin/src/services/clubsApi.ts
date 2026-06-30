import { httpClient } from "./httpClient";

export type ClubStatus = "Pending" | "Active" | "Suspended";

export interface ClubSummary {
  id: string;
  name: string;
  slug: string;
  city: string;
  contactEmail: string;
  contactPhone: string;
  websiteUrl: string | null;
  logoUrl: string | null;
  description: string | null;
  status: ClubStatus;
  memberCount: number;
  createdAtUtc: string;
}

export async function listClubs(
  status?: ClubStatus,
  signal?: AbortSignal,
): Promise<ClubSummary[]> {
  const { data } = await httpClient.get<ClubSummary[]>("/api/clubs", {
    params: status ? { status } : undefined,
    signal,
  });
  return data;
}

export async function approveClub(id: string, signal?: AbortSignal): Promise<void> {
  await httpClient.post(`/api/clubs/${encodeURIComponent(id)}/approve`, null, { signal });
}
