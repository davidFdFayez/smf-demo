import axios from "axios";
import { httpClient } from "./httpClient";
import type {
  InitializePaymentRequest,
  InitializePaymentResponse,
} from "../features/payments/types";
import { ApiError, type ValidationProblemDetails } from "./membersApi";

/**
 * Thrown when the caller aborts the request via AbortController.
 * Callers should treat this as a normal cancel, not as an error to
 * surface to the user.
 */
export class PaymentAbortedError extends Error {
  constructor() {
    super("Payment initialization was cancelled.");
    this.name = "PaymentAbortedError";
  }
}

/**
 * POST /api/payments — starts a payment attempt. The backend persists a
 * Pending Payment row and returns the provider's checkout URL; the caller
 * is expected to redirect the browser there.
 *
 * Pass an `AbortSignal` to cancel the request if the user navigates away
 * mid-flight (e.g. component unmount). On abort we throw
 * `PaymentAbortedError` so the call site can silently swallow it.
 */
export async function initializePayment(
  input: InitializePaymentRequest,
  signal?: AbortSignal,
): Promise<InitializePaymentResponse> {
  try {
    const { data } = await httpClient.post<InitializePaymentResponse>(
      "/api/payments",
      input,
      { signal },
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

function toApiError(err: unknown): Error {
  if (axios.isCancel(err) || (err instanceof Error && err.name === "CanceledError")) {
    return new PaymentAbortedError();
  }

  if (axios.isAxiosError(err)) {
    const status = err.response?.status;
    const body = err.response?.data as ValidationProblemDetails | undefined;
    const title = body?.title ?? err.message ?? "Request failed.";

    if (status === 400 && body?.errors) {
      const first = Object.values(body.errors)[0]?.[0];
      return new ApiError(status, first ?? title);
    }
    if (status === 404) {
      // Intentional sentinel text — the component maps 404 onto its
      // localised "member not found" string. Matching on `status === 404`
      // is the stable contract, not this message.
      return new ApiError(status, "Member not found.");
    }

    return new ApiError(status, title);
  }

  return new ApiError(undefined, (err as Error).message ?? "Network error.");
}
