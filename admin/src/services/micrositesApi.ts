import { httpClient } from "./httpClient";

export interface ClubMicrositeUpcomingEvent {
  id: string;
  slug: string;
  title: string;
  startDateUtc: string;
  endDateUtc: string;
  city: string;
  country: string;
}

export interface ClubMicrositeDetails {
  id: string;
  slug: string;
  name: string;
  city: string;
  country: string | null;
  description: string | null;
  contactEmail: string;
  contactPhone: string;
  website: string | null;
  memberCount: number;
  micrositeHeadline: string | null;
  micrositeAbout: string | null;
  micrositeHeroImageUrl: string | null;
  micrositePrimaryColor: string | null;
  micrositeInstagramHandle: string | null;
  micrositeTwitterHandle: string | null;
  micrositeYoutubeChannel: string | null;
  upcomingEvents: ClubMicrositeUpcomingEvent[];
}

export interface UpdateMicrositeInput {
  headline?: string;
  about?: string;
  heroImageUrl?: string;
  primaryColor?: string;
  instagramHandle?: string;
  twitterHandle?: string;
  youtubeChannel?: string;
}

export async function getMicrositeBySlug(
  slug: string,
  signal?: AbortSignal,
): Promise<ClubMicrositeDetails> {
  const { data } = await httpClient.get<ClubMicrositeDetails>(
    `/api/clubs/by-slug/${encodeURIComponent(slug)}`,
    { signal },
  );
  return data;
}

export async function updateMicrosite(
  clubId: string,
  input: UpdateMicrositeInput,
): Promise<ClubMicrositeDetails> {
  const { data } = await httpClient.put<ClubMicrositeDetails>(
    `/api/clubs/${clubId}/microsite`,
    input,
  );
  return data;
}
