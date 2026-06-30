import { useEffect, useRef, useState } from "react";
import Hls from "hls.js";

/**
 * Adaptive-bitrate HLS / progressive MP4 player used by the live-watch and
 * overlay pages. Behaviour:
 *
 *   • <c>.m3u8</c> URLs are played via <c>hls.js</c> when the browser can't
 *     play HLS natively (Chrome/Firefox/Edge). Safari and iOS use the
 *     built-in HTMLMediaElement support.
 *   • Anything else (mp4, webm, etc.) is handed straight to the native
 *     <c>&lt;video&gt;</c> element.
 *   • Errors are reported via <c>onError</c> so callers can fall back to
 *     an iframe embed or show a "stream offline" placeholder.
 */
export interface HlsVideoPlayerProps {
  /** Full URL to the stream — `.m3u8`, `.mp4`, etc. */
  src: string;
  /** Auto-start playback when ready. Defaults to true. */
  autoPlay?: boolean;
  /** Mute by default — required by browsers to allow autoplay. */
  muted?: boolean;
  /** Show built-in controls. Defaults to true. */
  controls?: boolean;
  className?: string;
  poster?: string;
  onError?: (msg: string) => void;
}

export function HlsVideoPlayer({
  src,
  autoPlay = true,
  muted = true,
  controls = true,
  className,
  poster,
  onError,
}: HlsVideoPlayerProps) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const hlsRef   = useRef<Hls | null>(null);
  const [stalled, setStalled] = useState(false);

  const onErrorRef = useRef(onError);
  onErrorRef.current = onError;

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;

    setStalled(false);
    let disposed = false;

    const isHls = /\.m3u8(\?|$)/i.test(src);

    if (isHls && Hls.isSupported()) {
      const hls = new Hls({
        liveDurationInfinity: true,
        liveSyncDurationCount: 3,
        enableWorker: true,
        lowLatencyMode: true,
      });
      hlsRef.current = hls;

      hls.loadSource(src);
      hls.attachMedia(video);
      hls.on(Hls.Events.ERROR, (_e, data) => {
        if (disposed) return;
        if (data.fatal) {
          switch (data.type) {
            case Hls.ErrorTypes.NETWORK_ERROR:
              hls.startLoad();
              setStalled(true);
              onErrorRef.current?.("Network error — retrying.");
              break;
            case Hls.ErrorTypes.MEDIA_ERROR:
              hls.recoverMediaError();
              setStalled(true);
              onErrorRef.current?.("Media decode error — recovering.");
              break;
            default:
              hls.destroy();
              hlsRef.current = null;
              onErrorRef.current?.(data.details || "Fatal stream error.");
          }
        }
      });
      hls.on(Hls.Events.FRAG_LOADED, () => setStalled(false));
    } else {
      video.src = src;
    }

    return () => {
      disposed = true;
      if (hlsRef.current) {
        hlsRef.current.destroy();
        hlsRef.current = null;
      }
      video.removeAttribute("src");
      video.load();
    };
  }, [src]);

  return (
    <div className={`relative ${className ?? ""}`}>
      <video
        ref={videoRef}
        autoPlay={autoPlay}
        muted={muted}
        controls={controls}
        playsInline
        poster={poster}
        className="h-full w-full bg-black"
      />
      {stalled && (
        <div className="pointer-events-none absolute inset-x-0 top-2 mx-auto w-fit rounded-full bg-black/70 px-3 py-1 text-xs text-white">
          Reconnecting…
        </div>
      )}
    </div>
  );
}

/** True if the URL looks like an HLS playlist or progressive video file. */
export function isDirectVideoUrl(url: string | null | undefined): boolean {
  if (!url) return false;
  return /\.(m3u8|mp4|webm|mov|m4v)(\?|$)/i.test(url);
}
