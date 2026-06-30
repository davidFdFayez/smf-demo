import axios from "axios";
import { httpClient } from "./httpClient";
import type { MemberListItem, PagedResult } from "../types/member";

export class ApiError extends Error {
  constructor(public readonly status: number | undefined, message: string) {
    super(message);
    this.name = "ApiError";
  }
}

export interface ListMembersFilters {
  search?: string;
  role?: string;
  status?: string;
}

export async function listMembers(
  page = 1,
  pageSize = 25,
  filters: ListMembersFilters = {},
  signal?: AbortSignal,
): Promise<PagedResult<MemberListItem>> {
  try {
    const params: Record<string, string | number> = { page, pageSize };
    if (filters.search?.trim()) params.search = filters.search.trim();
    if (filters.role) params.role = filters.role;
    if (filters.status) params.status = filters.status;
    const { data } = await httpClient.get<PagedResult<MemberListItem>>(
      "/api/members",
      { params, signal },
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export interface MemberDetails extends MemberListItem {}

export async function getMemberById(
  id: string,
  signal?: AbortSignal,
): Promise<MemberDetails> {
  try {
    const { data } = await httpClient.get<MemberDetails>(
      `/api/members/${encodeURIComponent(id)}`,
      { signal },
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export async function approveMember(id: string, signal?: AbortSignal): Promise<void> {
  try {
    await httpClient.post(`/api/members/${encodeURIComponent(id)}/approve`, null, { signal });
  } catch (err) {
    throw toApiError(err);
  }
}

export async function recordMedicalClearance(
  id: string,
  input: { cleared: boolean; weightCategoryKg?: number },
  signal?: AbortSignal,
): Promise<void> {
  try {
    await httpClient.post(
      `/api/members/${encodeURIComponent(id)}/medical-clearance`,
      input,
      { signal },
    );
  } catch (err) {
    throw toApiError(err);
  }
}

export interface IssueDigitalIdResult {
  memberId: string;
  token: string;
  expiresAtUtc: string;
  member: MemberDetails;
}

export async function issueDigitalId(
  id: string,
  signal?: AbortSignal,
): Promise<IssueDigitalIdResult> {
  try {
    const { data } = await httpClient.post<IssueDigitalIdResult>(
      `/api/members/${encodeURIComponent(id)}/digital-id`,
      null,
      { signal },
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

function toApiError(err: unknown): Error {
  if (axios.isAxiosError(err)) {
    const status = err.response?.status;
    const body = err.response?.data as { title?: string } | undefined;
    return new ApiError(status, body?.title ?? err.message ?? "Request failed.");
  }
  return new ApiError(undefined, (err as Error).message ?? "Network error.");
}
