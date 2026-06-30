import { httpClient } from "./httpClient";

export type EventStatus =
  | "Draft"
  | "Published"
  | "RegistrationOpen"
  | "RegistrationClosed"
  | "InProgress"
  | "Completed"
  | "Cancelled";

export type LiveStreamProvider = "YouTube" | "Twitch" | "Vimeo" | "CustomEmbed";

export interface EventSummary {
  id: string;
  title: string;
  description: string | null;
  location: string;
  startsAtUtc: string;
  endsAtUtc: string;
  registrationOpensAtUtc: string;
  registrationClosesAtUtc: string;
  entryFeeMinor: number;
  currency: string;
  capacity: number | null;
  status: EventStatus;
  registrationCount: number;
  liveStreamProvider: LiveStreamProvider | null;
  liveStreamUrl: string | null;
}

export interface CreateEventInput {
  title: string;
  description?: string;
  location: string;
  startsAtUtc: string;
  endsAtUtc: string;
  registrationOpensAtUtc: string;
  registrationClosesAtUtc: string;
  entryFeeMinor: number;
  currency: string;
  capacity?: number | null;
}

export async function listEvents(signal?: AbortSignal): Promise<EventSummary[]> {
  const { data } = await httpClient.get<EventSummary[]>("/api/events", { signal });
  return data;
}

export async function createEvent(
  input: CreateEventInput,
  signal?: AbortSignal,
): Promise<EventSummary> {
  const { data } = await httpClient.post<EventSummary>("/api/events", input, { signal });
  return data;
}

export async function publishEvent(id: string, signal?: AbortSignal): Promise<void> {
  await httpClient.post(`/api/events/${encodeURIComponent(id)}/publish`, null, { signal });
}

export async function cancelEvent(id: string, signal?: AbortSignal): Promise<void> {
  await httpClient.post(`/api/events/${encodeURIComponent(id)}/cancel`, null, { signal });
}

export async function getEventById(
  id: string,
  signal?: AbortSignal,
): Promise<EventSummary> {
  const { data } = await httpClient.get<EventSummary>(
    `/api/events/${encodeURIComponent(id)}`,
    { signal },
  );
  return data;
}

export async function registerForEvent(
  eventId: string,
  memberId: string,
  signal?: AbortSignal,
): Promise<string> {
  const { data } = await httpClient.post<{ registrationId: string }>(
    `/api/events/${encodeURIComponent(eventId)}/registrations`,
    { memberId },
    { signal },
  );
  return data.registrationId;
}

export async function cancelRegistration(
  regId: string,
  signal?: AbortSignal,
): Promise<void> {
  await httpClient.post(
    `/api/events/registrations/${encodeURIComponent(regId)}/cancel`,
    null,
    { signal },
  );
}
