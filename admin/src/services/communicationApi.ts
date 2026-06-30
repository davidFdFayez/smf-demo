import { httpClient } from "./httpClient";
import type { PagedResult } from "./storeApi";

/**
 * Admin client for the Communication & Engagement API. Surface lives in
 * <c>SMF.Api/Endpoints/CommunicationEndpoints.cs</c> — broadcasts,
 * notifications log, feedback moderation and social highlight CRUD.
 */

export type BroadcastChannel =
  | "None"
  | "Email"
  | "Sms"
  | "Push"
  // Bitmask combinations the backend serialises as comma-joined names:
  | "Email, Sms"
  | "Email, Push"
  | "Sms, Push"
  | "All";

export type BroadcastStatus =
  | "Draft"
  | "Scheduled"
  | "Sending"
  | "Sent"
  | "Failed"
  | "Cancelled";

export type NotificationStatus = "Queued" | "Sent" | "Failed";
export type NotificationChannel = "Email" | "Sms" | "Push";

export type FeedbackStatus = "Pending" | "Public" | "Hidden";
export type FeedbackSubjectType = "General" | "Event" | "Product" | "Course";

export type SocialPlatform =
  | "Instagram"
  | "X"
  | "YouTube"
  | "TikTok"
  | "Facebook"
  | "LinkedIn";

export type MemberRole =
  | "Athlete"
  | "Coach"
  | "Referee"
  | "ClubAdmin"
  | "FederationStaff"
  | "Visitor";

export interface BroadcastSummary {
  id: string;
  title: string;
  subject: string;
  channels: BroadcastChannel;
  status: BroadcastStatus;
  totalTargets: number;
  deliveredCount: number;
  failedCount: number;
  createdAtUtc: string;
  scheduledAtUtc?: string | null;
  completedAtUtc?: string | null;
}

export interface BroadcastDetail extends BroadcastSummary {
  body: string;
  targetRoles: MemberRole[];
  targetClubId?: string | null;
  targetEventId?: string | null;
  activeMembersOnly: boolean;
  failureReason?: string | null;
  createdByMemberId?: string | null;
  startedAtUtc?: string | null;
}

export interface NotificationDto {
  id: string;
  channel: NotificationChannel;
  recipientAddress: string;
  subject: string;
  body: string;
  status: NotificationStatus;
  attemptCount: number;
  lastError?: string | null;
  providerMessageId?: string | null;
  memberId?: string | null;
  broadcastCampaignId?: string | null;
  createdAtUtc: string;
  sentAtUtc?: string | null;
}

export interface FeedbackDto {
  id: string;
  subjectType: FeedbackSubjectType;
  subjectId?: string | null;
  rating: number;
  comment: string;
  authorMemberId?: string | null;
  authorName: string;
  authorEmail?: string | null;
  status: FeedbackStatus;
  adminNotes?: string | null;
  createdAtUtc: string;
  moderatedAtUtc?: string | null;
}

export interface SocialHighlightDto {
  id: string;
  platform: SocialPlatform;
  caption: string;
  externalUrl: string;
  embedHtml?: string | null;
  mediaUrl?: string | null;
  displayOrder: number;
  isPublished: boolean;
  postedAtUtc?: string | null;
  createdAtUtc: string;
}

export interface AudiencePreviewResult {
  totalMembers: number;
  emailReachable: number;
  smsReachable: number;
  pushReachable: number;
}

// ── broadcasts ─────────────────────────────────────────────────────

export interface CreateBroadcastInput {
  title: string;
  subject: string;
  body: string;
  channels: BroadcastChannel;
  targetRoles?: MemberRole[];
  targetClubId?: string | null;
  targetEventId?: string | null;
  activeMembersOnly: boolean;
  scheduledAtUtc?: string | null;
}

export async function listBroadcasts(args: {
  page?: number;
  pageSize?: number;
  status?: BroadcastStatus;
  search?: string;
}): Promise<PagedResult<BroadcastSummary>> {
  const { data } = await httpClient.get<PagedResult<BroadcastSummary>>(
    "/api/admin/broadcasts",
    { params: clean(args) },
  );
  return data;
}

export async function getBroadcast(id: string): Promise<BroadcastDetail> {
  const { data } = await httpClient.get<BroadcastDetail>(
    `/api/admin/broadcasts/${id}`,
  );
  return data;
}

export async function createBroadcast(input: CreateBroadcastInput): Promise<{ id: string }> {
  const { data } = await httpClient.post<{ id: string }>(
    "/api/admin/broadcasts",
    input,
  );
  return data;
}

export async function sendBroadcast(id: string): Promise<BroadcastDetail> {
  const { data } = await httpClient.post<BroadcastDetail>(
    `/api/admin/broadcasts/${id}/send`,
    {},
  );
  return data;
}

export async function cancelBroadcast(id: string): Promise<void> {
  await httpClient.post(`/api/admin/broadcasts/${id}/cancel`, {});
}

export async function previewBroadcastAudience(input: {
  channels: BroadcastChannel;
  targetRoles?: MemberRole[];
  targetClubId?: string | null;
  targetEventId?: string | null;
  activeMembersOnly: boolean;
}): Promise<AudiencePreviewResult> {
  const { data } = await httpClient.post<AudiencePreviewResult>(
    "/api/admin/broadcasts/preview",
    input,
  );
  return data;
}

// ── notifications log ─────────────────────────────────────────────

export async function listNotifications(args: {
  page?: number;
  pageSize?: number;
  status?: NotificationStatus;
  channel?: NotificationChannel;
  campaignId?: string;
}): Promise<PagedResult<NotificationDto>> {
  const { data } = await httpClient.get<PagedResult<NotificationDto>>(
    "/api/admin/notifications",
    { params: clean(args) },
  );
  return data;
}

// ── feedback ─────────────────────────────────────────────────────

export async function listFeedback(args: {
  page?: number;
  pageSize?: number;
  status?: FeedbackStatus;
  subjectType?: FeedbackSubjectType;
  subjectId?: string;
  minRating?: number;
}): Promise<PagedResult<FeedbackDto>> {
  const { data } = await httpClient.get<PagedResult<FeedbackDto>>(
    "/api/admin/feedback",
    { params: clean(args) },
  );
  return data;
}

export async function moderateFeedback(
  id: string,
  input: { newStatus: FeedbackStatus; moderatorMemberId?: string | null; adminNotes?: string | null },
): Promise<FeedbackDto> {
  const { data } = await httpClient.post<FeedbackDto>(
    `/api/admin/feedback/${id}/moderate`,
    input,
  );
  return data;
}

// ── social highlights ────────────────────────────────────────────

export interface SocialHighlightInput {
  platform: SocialPlatform;
  caption: string;
  externalUrl: string;
  embedHtml?: string | null;
  mediaUrl?: string | null;
  displayOrder: number;
  isPublished: boolean;
  postedAtUtc?: string | null;
}

export async function listSocialHighlights(args: {
  platform?: SocialPlatform;
} = {}): Promise<SocialHighlightDto[]> {
  const { data } = await httpClient.get<SocialHighlightDto[]>(
    "/api/admin/social/highlights",
    { params: clean(args) },
  );
  return data;
}

export async function createSocialHighlight(input: SocialHighlightInput): Promise<{ id: string }> {
  const { data } = await httpClient.post<{ id: string }>(
    "/api/admin/social/highlights",
    input,
  );
  return data;
}

export async function updateSocialHighlight(
  id: string,
  input: SocialHighlightInput,
): Promise<void> {
  await httpClient.put(`/api/admin/social/highlights/${id}`, { id, ...input });
}

export async function deleteSocialHighlight(id: string): Promise<void> {
  await httpClient.delete(`/api/admin/social/highlights/${id}`);
}

function clean<T extends object>(input: T): Partial<T> {
  const out: Partial<T> = {};
  for (const [k, v] of Object.entries(input as Record<string, unknown>)) {
    if (v !== undefined && v !== null && v !== "") {
      (out as Record<string, unknown>)[k] = v;
    }
  }
  return out;
}
