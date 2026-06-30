import { httpClient } from "./httpClient";

export interface ClubMicrositeRosterMember {
  id: string;
  fullName: string;
  smfId: string;
  role: "Athlete" | "Coach" | "Referee" | "ClubAdmin" | "FederationAdmin";
}

export interface ClubMicrositeUpcomingEvent {
  id: string;
  title: string;
  location: string;
  startsAtUtc: string;
}

export interface ClubMicrositeDetails {
  id: string;
  slug: string;
  name: string;
  city: string;
  status: "Pending" | "Active" | "Suspended";
  contactEmail: string;
  contactPhone: string;
  websiteUrl: string | null;
  logoUrl: string | null;
  description: string | null;
  headline: string | null;
  about: string | null;
  heroImageUrl: string | null;
  primaryColor: string | null;
  instagramHandle: string | null;
  twitterHandle: string | null;
  youtubeChannel: string | null;
  memberCount: number;
  roster: ClubMicrositeRosterMember[];
  upcomingEvents: ClubMicrositeUpcomingEvent[];
}

export async function getClubMicrosite(
  slug: string,
  signal?: AbortSignal,
): Promise<ClubMicrositeDetails> {
  const { data } = await httpClient.get<ClubMicrositeDetails>(
    `/api/clubs/by-slug/${encodeURIComponent(slug)}`,
    { signal },
  );
  return data;
}
