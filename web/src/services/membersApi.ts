import axios from "axios";
import { httpClient } from "./httpClient";
import type {
  MemberListItem,
  PagedResult,
  RegisterMemberInput,
  RegisterMemberResponse,
} from "../features/members/schema";

/**
 * ASP.NET Core `ValidationProblemDetails` shape returned by the
 * ExceptionHandlingMiddleware when FluentValidation rejects a request.
 */
export interface ValidationProblemDetails {
  title?: string;
  status?: number;
  instance?: string;
  errors?: Record<string, string[]>;
}

export class ApiValidationError extends Error {
  constructor(
    public readonly errors: Record<string, string[]>,
    message = "Validation failed.",
  ) {
    super(message);
    this.name = "ApiValidationError";
  }
}

export class ApiError extends Error {
  constructor(
    public readonly status: number | undefined,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/** POST /api/members — sends the command payload and parses typed errors. */
export async function registerMember(
  input: RegisterMemberInput,
  signal?: AbortSignal,
): Promise<RegisterMemberResponse> {
  try {
    const { data } = await httpClient.post<RegisterMemberResponse>(
      "/api/members",
      toRegisterPayload(input),
      { signal },
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

/** Omit empty optional guardian fields so the API never sees "" for enums. */
function toRegisterPayload(input: RegisterMemberInput): Record<string, unknown> {
  const payload: Record<string, unknown> = { ...input };
  for (const key of [
    "guardianFullName",
    "guardianEmail",
    "guardianPhone",
    "guardianNationalId",
    "guardianRelation",
  ] as const) {
    const value = payload[key];
    if (value === "" || value === undefined || value === null) {
      delete payload[key];
    }
  }
  return payload;
}

export interface ListMembersFilters {
  search?: string;
  role?: string;
  status?: string;
}

/** GET /api/members?page&pageSize&search&role&status — lists registered members. */
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

/** POST /api/members/{id}/medical-clearance — records readiness status (athletes only). */
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

/** POST /api/members/{id}/approve — flips a Pending member to Approved. */
export async function approveMember(
  id: string,
  signal?: AbortSignal,
): Promise<void> {
  try {
    await httpClient.post(`/api/members/${encodeURIComponent(id)}/approve`, null, {
      signal,
    });
  } catch (err) {
    throw toApiError(err);
  }
}

function toApiError(err: unknown): Error {
  if (axios.isAxiosError(err)) {
    const status = err.response?.status;
    const body = err.response?.data as ValidationProblemDetails | undefined;

    if (status === 400 && body?.errors) {
      return new ApiValidationError(
        normalizeServerErrors(body.errors),
        body.title ?? "Validation failed.",
      );
    }

    return new ApiError(status, body?.title ?? err.message ?? "Request failed.");
  }

  return new ApiError(undefined, (err as Error).message ?? "Network error.");
}

/**
 * Server keys come back PascalCased (e.g. `FullName`, `GuardianConsent`).
 * Map to the camelCase field names used by react-hook-form.
 */
function normalizeServerErrors(
  errors: Record<string, string[]>,
): Record<string, string[]> {
  const out: Record<string, string[]> = {};
  for (const [key, messages] of Object.entries(errors)) {
    const camel = key.length > 0 ? key[0].toLowerCase() + key.slice(1) : key;
    out[camel] = messages;
  }
  return out;
}
