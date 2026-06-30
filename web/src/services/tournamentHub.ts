import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { httpClient } from "./httpClient";
import type { TournamentDetails } from "./tournamentsApi";

const HUB_PATH = "/hubs/tournaments";

function resolveHubUrl(): string {
  const baseUrl = (httpClient.defaults.baseURL ?? "/").replace(/\/$/, "");
  return baseUrl.length === 0 ? HUB_PATH : `${baseUrl}${HUB_PATH}`;
}

/**
 * Builds a SignalR HubConnection configured for the tournament broadcast hub.
 * Uses JSON protocol (the hub sends moderate-size TournamentDetails snapshots;
 * JSON is easier to debug and wire-small enough for bracket payloads).
 */
export function buildTournamentConnection(
  accessTokenFactory: () => string | Promise<string>,
): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(resolveHubUrl(), {
      accessTokenFactory: () => Promise.resolve(accessTokenFactory()),
    })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 20_000])
    .configureLogging(LogLevel.Information)
    .build();
}

export async function safeStopTournament(conn: HubConnection | null): Promise<void> {
  if (!conn) return;
  if (
    conn.state === HubConnectionState.Connected ||
    conn.state === HubConnectionState.Connecting ||
    conn.state === HubConnectionState.Reconnecting
  ) {
    try {
      await conn.stop();
    } catch {
      // tearing down anyway
    }
  }
}

export type TournamentUpdateHandler = (payload: TournamentDetails) => void;
