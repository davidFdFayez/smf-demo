import { useSearchParams } from "react-router-dom";
import { Scoreboard } from "../features/scoring/Scoreboard";

/**
 * Transparent broadcast overlay designed to be pulled into OBS / vMix via a
 * "Browser Source". Usage:
 *
 *   /overlay?match=MATCH-CODE[&bg=transparent|black]
 *
 *   - `match` (required) — the same code the referee uses to connect.
 *   - `bg`    (optional) — `transparent` (default) keys out the page so the
 *     stream sees only the scoreboard + timer; `black` shows a solid
 *     backdrop for preview / debugging.
 *
 * The Scoreboard renders with controls + timeline hidden so only the round
 * timer and score are visible to viewers.
 */
export function ScoreboardOverlayPage() {
  const [params] = useSearchParams();
  const matchCode = params.get("match");
  const bg = params.get("bg") === "black" ? "#000" : "transparent";

  if (!matchCode) {
    return (
      <div className="m-6 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
        Missing <code className="rounded bg-rose-100 px-1">?match</code> query
        parameter. Add <code className="rounded bg-rose-100 px-1">?match=CODE</code>{" "}
        to the overlay URL.
      </div>
    );
  }

  return (
    <div
      className="min-h-screen p-4 text-white"
      style={{ background: bg }}
    >
      <div className="mx-auto max-w-2xl rounded-2xl bg-black/70 p-5 shadow-2xl">
        <Scoreboard matchCode={matchCode} autoStart hideControls hideTimeline />
      </div>
    </div>
  );
}
