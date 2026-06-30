import { useCallback, useEffect, useState } from "react";
import {
  createSocialHighlight,
  deleteSocialHighlight,
  listSocialHighlights,
  updateSocialHighlight,
  type SocialHighlightDto,
  type SocialHighlightInput,
  type SocialPlatform,
} from "../services/communicationApi";

const PLATFORMS: SocialPlatform[] = [
  "Instagram", "X", "YouTube", "TikTok", "Facebook", "LinkedIn",
];

const PLATFORM_BADGE: Record<SocialPlatform, string> = {
  Instagram: "bg-pink-50 text-pink-700 border-pink-200",
  X:         "bg-slate-100 text-slate-800 border-slate-300",
  YouTube:   "bg-red-50 text-red-700 border-red-200",
  TikTok:    "bg-slate-900/5 text-slate-900 border-slate-300",
  Facebook:  "bg-blue-50 text-blue-700 border-blue-200",
  LinkedIn:  "bg-sky-50 text-sky-700 border-sky-200",
};

export function SocialHighlightsPage() {
  const [items, setItems] = useState<SocialHighlightDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<SocialHighlightDto | "new" | null>(null);

  const reload = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const list = await listSocialHighlights();
      setItems(list);
    } catch (err) { setError((err as Error).message); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { reload(); }, [reload]);

  async function handleDelete(id: string) {
    if (!confirm("Delete this highlight?")) return;
    try {
      await deleteSocialHighlight(id);
      await reload();
    } catch (err) { alert((err as Error).message); }
  }

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Social media highlights</h1>
          <p className="mt-1 text-sm text-slate-500">
            Curate a feed of Instagram / X / YouTube posts that appear on the
            public homepage. Embed snippets render inside the official platform widget.
          </p>
        </div>
        <button type="button" onClick={() => setEditing("new")}
          className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-brand-700">
          Add highlight
        </button>
      </header>

      <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
        {error && <div className="border-b border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}
        {loading && !items.length && <div className="px-4 py-6 text-center text-slate-500">Loading…</div>}
        {!loading && items.length === 0 && (
          <div className="px-4 py-6 text-center text-slate-500">No highlights yet.</div>
        )}
        <ul className="divide-y divide-slate-200">
          {items.map((h) => (
            <li key={h.id} className="flex flex-wrap items-start gap-4 px-4 py-4">
              <div className="flex-1 min-w-0 space-y-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${PLATFORM_BADGE[h.platform]}`}>
                    {h.platform}
                  </span>
                  <span className="text-xs text-slate-500">order {h.displayOrder}</span>
                  {!h.isPublished && (
                    <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-800">
                      Draft
                    </span>
                  )}
                </div>
                <p className="text-sm text-slate-800">{h.caption}</p>
                <a href={h.externalUrl} target="_blank" rel="noopener" className="text-xs text-brand-600 hover:underline truncate block">{h.externalUrl}</a>
              </div>
              <div className="flex gap-1">
                <button type="button" onClick={() => setEditing(h)}
                  className="rounded-lg border border-slate-200 px-2.5 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50">
                  Edit
                </button>
                <button type="button" onClick={() => handleDelete(h.id)}
                  className="rounded-lg border border-red-200 px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-50">
                  Delete
                </button>
              </div>
            </li>
          ))}
        </ul>
      </section>

      {editing !== null && (
        <SocialHighlightModal
          initial={editing === "new" ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); reload(); }}
        />
      )}
    </div>
  );
}

interface ModalProps {
  initial: SocialHighlightDto | null;
  onClose: () => void;
  onSaved: () => void;
}

function SocialHighlightModal({ initial, onClose, onSaved }: ModalProps) {
  const [platform,    setPlatform]    = useState<SocialPlatform>(initial?.platform ?? "Instagram");
  const [caption,     setCaption]     = useState(initial?.caption ?? "");
  const [externalUrl, setExternalUrl] = useState(initial?.externalUrl ?? "");
  const [embedHtml,   setEmbedHtml]   = useState(initial?.embedHtml ?? "");
  const [mediaUrl,    setMediaUrl]    = useState(initial?.mediaUrl ?? "");
  const [displayOrder, setDisplayOrder] = useState(initial?.displayOrder ?? 0);
  const [isPublished,  setIsPublished]  = useState(initial?.isPublished ?? true);
  const [postedAt,     setPostedAt]     = useState(initial?.postedAtUtc ?? "");
  const [busy,  setBusy]  = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    setBusy(true); setError(null);
    try {
      const payload: SocialHighlightInput = {
        platform,
        caption: caption.trim(),
        externalUrl: externalUrl.trim(),
        embedHtml:   embedHtml.trim()   || null,
        mediaUrl:    mediaUrl.trim()    || null,
        displayOrder,
        isPublished,
        postedAtUtc: postedAt ? new Date(postedAt).toISOString() : null,
      };
      if (initial) await updateSocialHighlight(initial.id, payload);
      else         await createSocialHighlight(payload);
      onSaved();
    } catch (err) { setError((err as Error).message); }
    finally { setBusy(false); }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <div className="w-full max-w-2xl overflow-hidden rounded-2xl bg-white shadow-2xl">
        <header className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-900">
            {initial ? "Edit highlight" : "Add highlight"}
          </h2>
          <button type="button" onClick={onClose} className="text-slate-500 hover:text-slate-900">&times;</button>
        </header>

        <div className="grid gap-4 p-6 md:grid-cols-2">
          <label className="text-sm">
            <span className="font-medium text-slate-700">Platform</span>
            <select value={platform} onChange={(e) => setPlatform(e.target.value as SocialPlatform)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2">
              {PLATFORMS.map((p) => <option key={p} value={p}>{p}</option>)}
            </select>
          </label>
          <label className="text-sm">
            <span className="font-medium text-slate-700">Display order</span>
            <input type="number" value={displayOrder}
              onChange={(e) => setDisplayOrder(Number(e.target.value) || 0)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
          </label>
          <label className="md:col-span-2 text-sm">
            <span className="font-medium text-slate-700">Caption</span>
            <textarea value={caption} rows={3} onChange={(e) => setCaption(e.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
          </label>
          <label className="md:col-span-2 text-sm">
            <span className="font-medium text-slate-700">External URL</span>
            <input value={externalUrl} onChange={(e) => setExternalUrl(e.target.value)}
              placeholder="https://www.instagram.com/p/…"
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
          </label>
          <label className="md:col-span-2 text-sm">
            <span className="font-medium text-slate-700">Embed HTML <span className="text-xs text-slate-500">(optional, paste from platform)</span></span>
            <textarea value={embedHtml} rows={5} onChange={(e) => setEmbedHtml(e.target.value)}
              placeholder="<blockquote class='instagram-media' …>…</blockquote>"
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-xs" />
          </label>
          <label className="text-sm">
            <span className="font-medium text-slate-700">Preview image URL <span className="text-xs text-slate-500">(optional fallback)</span></span>
            <input value={mediaUrl} onChange={(e) => setMediaUrl(e.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
          </label>
          <label className="text-sm">
            <span className="font-medium text-slate-700">Posted at (UTC)</span>
            <input type="datetime-local" value={postedAt?.slice(0, 16) ?? ""}
              onChange={(e) => setPostedAt(e.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
          </label>
          <label className="md:col-span-2 flex items-center gap-2 text-sm">
            <input type="checkbox" checked={isPublished} onChange={(e) => setIsPublished(e.target.checked)} />
            Show on public site
          </label>

          {error && <div className="md:col-span-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}
        </div>

        <footer className="flex items-center justify-end gap-2 border-t border-slate-200 px-6 py-4">
          <button type="button" onClick={onClose}
            className="rounded-lg border border-slate-200 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
            Cancel
          </button>
          <button type="button" onClick={submit} disabled={busy || !caption.trim() || !externalUrl.trim()}
            className="rounded-lg bg-brand-600 px-3 py-2 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50">
            {busy ? "Saving…" : initial ? "Save changes" : "Add highlight"}
          </button>
        </footer>
      </div>
    </div>
  );
}
