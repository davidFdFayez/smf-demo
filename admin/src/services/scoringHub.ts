import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { MessagePackHubProtocol } from "@microsoft/signalr-protocol-msgpack";
import { httpClient } from "./httpClient";
import type { MatchReplay } from "../types/scoring";

const HUB_PATH = "/hubs/match-scoring";

function resolveHubUrl(): string {
  const baseUrl = (httpClient.defaults.baseURL ?? "/").replace(/\/$/, "");
  return baseUrl.length === 0 ? HUB_PATH : `${baseUrl}${HUB_PATH}`;
}

export function buildScoringConnection(
  accessTokenFactory: () => string | Promise<string>,
): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(resolveHubUrl(), {
      accessTokenFactory: () => Promise.resolve(accessTokenFactory()),
    })
    .withHubProtocol(new MessagePackHubProtocol())
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 20_000])
    .configureLogging(LogLevel.Information)
    .build();
}

export async function safeStop(conn: HubConnection | null): Promise<void> {
  if (!conn) return;
  if (
    conn.state === HubConnectionState.Connected ||
    conn.state === HubConnectionState.Connecting ||
    conn.state === HubConnectionState.Reconnecting
  ) {
    try {
      await conn.stop();
    } catch {
      /* ignore – tearing down */
    }
  }
}

export async function fetchMatchReplay(matchCode: string): Promise<MatchReplay> {
  const { data } = await httpClient.get<MatchReplay>(
    `/api/matches/${encodeURIComponent(matchCode)}/events`,
  );
  return data;
}
