import { httpClient } from "./httpClient";

export interface DevTokenResponse {
  accessToken: string;
  expiresAt: string;
}

/**
 * Requests a development-only JWT for the given referee GUID.
 * The backend endpoint is only enabled in Development / Testing environments.
 */
export async function issueDevToken(
  refereeId: string,
  displayName: string,
  roles: string[] = [],
): Promise<DevTokenResponse> {
  const { data } = await httpClient.post<DevTokenResponse>("/api/auth/dev-token", {
    refereeId,
    displayName,
    roles,
  });
  return data;
}
