import { useEffect, useState } from "react";
import {
  listSocialHighlights,
  type SocialHighlightDto,
  type SocialPlatform,
} from "../../services/communicationApi";

const PLATFORM_ICON: Record<SocialPlatform, string> = {
  Instagram: "📸",
  X:         "𝕏",
  YouTube:   "▶",
  TikTok:    "♪",
  Facebook:  "f",
  LinkedIn:  "in",
};

const PLATFORM_LABEL: Record<SocialPlatform, string> = {
  Instagram: "Instagram",
  X:         "X / Twitter",
  YouTube:   "YouTube",
  TikTok:    "TikTok",
  Facebook:  "Facebook",
  LinkedIn:  "LinkedIn",
};

const PLATFORM_TINT: Record<SocialPlatform, string> = {
  Instagram: "from-pink-500 to-amber-500",
  X:         "from-slate-700 to-slate-900",
  YouTube:   "from-red-500 to-red-700",
  TikTok:    "from-cyan-400 to-pink-500",
  Facebook:  "from-blue-500 to-blue-700",
  LinkedIn:  "from-sky-500 to-sky-700",
};

interface Props {
  /** Limit to a single network (default: show everything). */
  platform?: SocialPlatform;
  /** Cap total cards (default 6). */
  take?: number;
  /** Section heading. */
  title?: string;
  /** Sub-heading. */
  subtitle?: string;
}

/**
 * Renders a curated grid of social posts pulled from the federation's
 * admin-managed highlight reel. If a post includes platform embed HTML
 * (e.g. an Instagram <blockquote>), we render it with the official
 * platform script. Otherwise we fall back to a styled card with the
 * media URL + deep link to the original post.
 */
export function SocialFeed({
  platform,
  take = 6,
  title = "Federation on social",
  subtitle = "Latest highlights from our official Instagram, X, and YouTube channels.",
}: Props) {
  const [items, setItems] = useState<SocialHighlightDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true); setError(null);
    listSocialHighlights({ platform, take })
      .then((rows) => { if (!cancelled) setItems(rows); })
      .catch((err)  => { if (!cancelled) setError((err as Error).message); })
      .finally(()   => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [platform, take]);

  // Once posts mount, kick the Instagram and Twitter widget loaders so
  // any embedded blockquotes get hydrated into real iframes.
  useEffect(() => {
    if (!items.length) return;
    const platforms = new Set(items.map((i) => i.platform));
    if (platforms.has("Instagram")) loadScript("https://www.instagram.com/embed.js", () => {
      // @ts-expect-error: instgrm is injected by the script
      window.instgrm?.Embeds?.process?.();
    });
    if (platforms.has("X")) loadScript("https://platform.twitter.com/widgets.js", () => {
      // @ts-expect-error: twttr is injected by the script
      window.twttr?.widgets?.load?.();
    });
  }, [items]);

  if (loading) return null; // Stay invisible until we know there's something to show.
  if (error)   return null;
  if (!items.length) return null;

  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-card">
      <header className="mb-5">
        <h2 className="text-2xl font-bold text-slate-900">{title}</h2>
        <p className="mt-1 text-sm text-slate-500">{subtitle}</p>
      </header>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {items.map((post) => (
          <article key={post.id} className="overflow-hidden rounded-xl border border-slate-200 bg-white">
            <a href={post.externalUrl} target="_blank" rel="noopener noreferrer"
              className="block aspect-square overflow-hidden bg-slate-100">
              {post.mediaUrl ? (
                <img src={post.mediaUrl} alt={post.caption}
                  className="h-full w-full object-cover transition group-hover:scale-105" loading="lazy" />
              ) : (
                <div className={`flex h-full w-full items-center justify-center bg-gradient-to-br ${PLATFORM_TINT[post.platform]} text-white`}>
                  <span className="text-5xl">{PLATFORM_ICON[post.platform]}</span>
                </div>
              )}
            </a>
            <div className="space-y-2 p-3">
              <div className="flex items-center gap-2 text-xs">
                <span className={`flex h-5 w-5 items-center justify-center rounded-full bg-gradient-to-br ${PLATFORM_TINT[post.platform]} text-[10px] font-bold text-white`}>
                  {PLATFORM_ICON[post.platform]}
                </span>
                <span className="font-medium text-slate-800">{PLATFORM_LABEL[post.platform]}</span>
                {post.postedAtUtc && (
                  <span className="ms-auto text-slate-500">
                    {new Date(post.postedAtUtc).toLocaleDateString()}
                  </span>
                )}
              </div>
              <p className="line-clamp-3 text-sm text-slate-700">{post.caption}</p>
              {post.embedHtml ? (
                <div className="overflow-hidden rounded-lg" dangerouslySetInnerHTML={{ __html: post.embedHtml }} />
              ) : (
                <a href={post.externalUrl} target="_blank" rel="noopener noreferrer"
                  className="inline-block text-xs font-medium text-smf-700 hover:underline">
                  View on {PLATFORM_LABEL[post.platform]} →
                </a>
              )}
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

/** Inject a <script> once and run a callback when it's ready. */
function loadScript(src: string, onReady: () => void) {
  if (typeof document === "undefined") return;
  const existing = document.querySelector<HTMLScriptElement>(`script[src="${src}"]`);
  if (existing) {
    if (existing.dataset.loaded === "true") onReady();
    else existing.addEventListener("load", onReady, { once: true });
    return;
  }
  const script = document.createElement("script");
  script.src = src; script.async = true;
  script.onload = () => { script.dataset.loaded = "true"; onReady(); };
  document.body.appendChild(script);
}
