import { httpClient } from "./httpClient";

export interface DevTokenResponse {
  accessToken: string;
  expiresAt: string;
}

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
