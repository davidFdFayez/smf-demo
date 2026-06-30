import { useEffect, useRef, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { issueDevToken } from "../../services/authApi";
import { getTournament, type TournamentDetails } from "../../services/tournamentsApi";
import {
  buildTournamentConnection,
  safeStopTournament,
} from "../../services/tournamentHub";

export type StreamState = "disconnected" | "connecting" | "connected" | "reconnecting" | "error";

interface Config {
  tournamentId: string;
  /** Identity used to obtain a dev JWT for the hub. Any valid member GUID works. */
  viewerId: string;
  viewerName: string;
}

/**
 * Subscribes to live bracket updates for a tournament. Works as follows:
 *   1. Fetches the initial snapshot via REST so the UI renders immediately.
 *   2. Connects to /hubs/tournaments, joins the tournament group, and
 *      replaces the snapshot every time the backend broadcasts an update.
 *
 * Guarded against stale state using a cancelled flag on unmount.
 */
export function useTournamentStream(config: Config | null): {
  state: StreamState;
  error: string | null;
  tournament: TournamentDetails | null;
} {
  const [state, setState] = useState<StreamState>("disconnected");
  const [error, setError] = useState<string | null>(null);
  const [tournament, setTournament] = useState<TournamentDetails | null>(null);
  const connRef = useRef<HubConnection | null>(null);
  const tokenRef = useRef<string | null>(null);

  useEffect(() => {
    if (!config) {
      setState("disconnected");
      setTournament(null);
      return;
    }
    let cancelled = false;
    setState("connecting");
    setError(null);

    const run = async () => {
      try {
        // Snapshot first: the screen should show *something* even if the hub
        // is slow / unreachable (graceful degradation).
        const snapshot = await getTournament(config.tournamentId);
        if (cancelled) return;
        setTournament(snapshot);

        const { accessToken } = await issueDevToken(config.viewerId, config.viewerName);
        if (cancelled) return;
        tokenRef.current = accessToken;

        const conn = buildTournamentConnection(() => tokenRef.current ?? "");
        connRef.current = conn;

        conn.on("ReceiveBracketUpdate", (payload: TournamentDetails) => {
          if (payload.id !== config.tournamentId) return;
          setTournament(payload);
        });

        conn.onreconnecting(() => setState("reconnecting"));
        conn.onreconnected(() => {
          setState("connected");
          void conn.invoke("JoinTournament", config.tournamentId);
        });
        conn.onclose(() => setState("disconnected"));

        await conn.start();
        if (cancelled) {
          await safeStopTournament(conn);
          return;
        }

        await conn.invoke("JoinTournament", config.tournamentId);
        setState("connected");
      } catch (err) {
        if (cancelled) return;
        setState("error");
        setError(err instanceof Error ? err.message : "Failed to connect.");
      }
    };

    void run();

    return () => {
      cancelled = true;
      void safeStopTournament(connRef.current);
      connRef.current = null;
    };
  }, [config?.tournamentId, config?.viewerId, config?.viewerName]);

  return { state, error, tournament };
}
