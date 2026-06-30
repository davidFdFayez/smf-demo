import { useEffect, useRef, useState } from "react";
import { chatbotStatus, streamChatReply } from "../../services/communicationApi";
import { useI18n } from "../../i18n";

/**
 * Floating FAQ assistant. Opens as a bottom-right pill, expands into a
 * dialog, and streams the assistant's response token-by-token via SSE.
 *
 * Conversation state lives in component state; the backend persists each
 * exchange so admins can audit usage. A guest UUID is generated once per
 * browser and stored in localStorage so a returning user gets the same
 * thread tagged in the audit log.
 */

interface ChatTurn { role: "user" | "assistant"; content: string }

const GUEST_KEY_STORAGE = "smf.chat.guestKey";

function getGuestKey(): string {
  if (typeof window === "undefined") return crypto.randomUUID();
  let key = window.localStorage.getItem(GUEST_KEY_STORAGE);
  if (!key) {
    key = crypto.randomUUID();
    window.localStorage.setItem(GUEST_KEY_STORAGE, key);
  }
  return key;
}

export function FloatingChatbot() {
  const { dir } = useI18n();
  const [open, setOpen] = useState(false);
  const [available, setAvailable] = useState<boolean | null>(null);
  const [history, setHistory] = useState<ChatTurn[]>([]);
  const [input, setInput] = useState("");
  const [streaming, setStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const guestKeyRef = useRef<string | null>(null);

  // Lazy probe the backend so we know whether to show the widget at all.
  useEffect(() => {
    let cancelled = false;
    chatbotStatus().then((s) => {
      if (!cancelled) setAvailable(s.configured);
    }).catch(() => {
      // Treat probe failure as "still try to show" — backend may simply not
      // be reachable yet. The send call will surface the error if so.
      if (!cancelled) setAvailable(true);
    });
    return () => { cancelled = true; };
  }, []);

  // Auto-scroll the transcript to the bottom on new tokens.
  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [history, streaming]);

  // Cancel any in-flight stream when the widget unmounts.
  useEffect(() => () => abortRef.current?.abort(), []);

  if (available === false) return null;

  async function send() {
    const trimmed = input.trim();
    if (!trimmed || streaming) return;
    setError(null);
    setInput("");

    const userTurn: ChatTurn = { role: "user", content: trimmed };
    const nextHistory = [...history, userTurn];
    setHistory([...nextHistory, { role: "assistant", content: "" }]);
    setStreaming(true);

    if (!guestKeyRef.current) guestKeyRef.current = getGuestKey();

    const ac = new AbortController();
    abortRef.current = ac;

    try {
      let buffer = "";
      const stream = streamChatReply(
        {
          guestKey: guestKeyRef.current,
          title: "Federation FAQ chat",
          messages: nextHistory.map((t) => ({ role: t.role, content: t.content })),
        },
        ac.signal,
      );
      for await (const chunk of stream) {
        buffer += chunk;
        setHistory((prev) => {
          const copy = prev.slice(0, -1);
          copy.push({ role: "assistant", content: buffer });
          return copy;
        });
      }
    } catch (err) {
      if ((err as Error).name === "AbortError") return;
      setError((err as Error).message);
      // Drop the empty assistant placeholder we added pre-stream.
      setHistory((prev) => prev[prev.length - 1]?.content === "" ? prev.slice(0, -1) : prev);
    } finally {
      setStreaming(false);
      abortRef.current = null;
    }
  }

  function stop() {
    abortRef.current?.abort();
  }

  function reset() {
    stop();
    setHistory([]);
    setError(null);
  }

  return (
    <div
      className={`fixed bottom-4 z-50 ${dir === "rtl" ? "left-4" : "right-4"}`}
      dir={dir}
    >
      {open ? (
        <div className="flex h-[32rem] w-[22rem] flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl">
          <header className="flex items-center justify-between gap-2 border-b border-slate-200 bg-gradient-to-r from-smf-600 to-smf-700 px-4 py-3 text-white">
            <div className="flex items-center gap-2">
              <span className="flex h-8 w-8 items-center justify-center rounded-full bg-white/15 text-base">💬</span>
              <div>
                <div className="text-sm font-semibold">SMF Assistant</div>
                <div className="text-[11px] opacity-80">Federation FAQ &amp; help</div>
              </div>
            </div>
            <div className="flex items-center gap-1">
              <button type="button" onClick={reset} title="New conversation"
                className="rounded p-1 hover:bg-white/15">↺</button>
              <button type="button" onClick={() => setOpen(false)} title="Close"
                className="rounded p-1 hover:bg-white/15">×</button>
            </div>
          </header>

          <div ref={scrollRef} className="flex-1 space-y-3 overflow-y-auto px-4 py-3 text-sm">
            {history.length === 0 && (
              <div className="rounded-xl bg-slate-50 p-3 text-slate-600">
                <div className="font-medium text-slate-800">Hi 👋</div>
                <p className="mt-1 text-xs">
                  Ask me about membership registration, events, scoring rules, the federation
                  store, or any other federation question. مرحبا — اسألني بالعربية أيضًا.
                </p>
              </div>
            )}
            {history.map((turn, i) => (
              <div key={i} className={`flex ${turn.role === "user" ? "justify-end" : "justify-start"}`}>
                <div className={`max-w-[85%] whitespace-pre-wrap rounded-2xl px-3 py-2 text-sm ${
                  turn.role === "user"
                    ? "bg-smf-600 text-white"
                    : "bg-slate-100 text-slate-800"
                }`}>
                  {turn.content || (streaming && i === history.length - 1
                    ? <span className="inline-flex gap-1"><Dot /><Dot delay={150} /><Dot delay={300} /></span>
                    : null)}
                </div>
              </div>
            ))}
            {error && (
              <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">{error}</div>
            )}
          </div>

          <form
            onSubmit={(e) => { e.preventDefault(); send(); }}
            className="flex items-end gap-2 border-t border-slate-200 p-3"
          >
            <textarea
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && !e.shiftKey) {
                  e.preventDefault();
                  send();
                }
              }}
              rows={1}
              placeholder="Type your question…"
              className="flex-1 resize-none rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm focus:border-smf-500 focus:outline-none focus:ring focus:ring-smf-200"
            />
            {streaming ? (
              <button type="button" onClick={stop}
                className="rounded-lg border border-slate-200 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
                Stop
              </button>
            ) : (
              <button type="submit" disabled={!input.trim()}
                className="rounded-lg bg-smf-600 px-3 py-2 text-sm font-semibold text-white hover:bg-smf-700 disabled:opacity-50">
                Send
              </button>
            )}
          </form>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="flex items-center gap-2 rounded-full bg-smf-600 px-4 py-3 text-sm font-semibold text-white shadow-lg transition hover:bg-smf-700 focus:outline-none focus:ring focus:ring-smf-200"
        >
          <span aria-hidden>💬</span>
          <span>Ask the Federation</span>
        </button>
      )}
    </div>
  );
}

function Dot({ delay = 0 }: { delay?: number }) {
  return (
    <span
      className="inline-block h-1.5 w-1.5 animate-bounce rounded-full bg-slate-400"
      style={{ animationDelay: `${delay}ms` }}
    />
  );
}
