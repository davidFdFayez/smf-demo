/**
 * Thin wrappers around browser-only checks we use in the checkout flow.
 * Centralised here so the component stays declarative and testable
 * against a stubbed implementation.
 */

interface ApplePaySessionCtor {
  canMakePayments?: () => boolean;
}

declare global {
  interface Window {
    ApplePaySession?: ApplePaySessionCtor;
  }
}

/**
 * Returns true when this browser can actually host an Apple Pay session.
 * Safari on iOS / macOS exposes `window.ApplePaySession` with a
 * `canMakePayments()` predicate; everywhere else returns false and we
 * render the tile disabled instead of pretending we support it.
 */
export function isApplePayAvailable(): boolean {
  if (typeof window === "undefined") return false;

  const session = window.ApplePaySession;
  if (!session || typeof session.canMakePayments !== "function") return false;

  try {
    return session.canMakePayments() === true;
  } catch {
    // Some browsers throw on `canMakePayments` inside insecure contexts
    // (non-HTTPS); treat any failure as "not available".
    return false;
  }
}

/**
 * Parse a GUID-shaped value from the current URL's `?memberId=` query
 * parameter. Returns null for missing / invalid input so callers can
 * fall back to their default selection.
 */
const GUID_RE =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export function memberIdFromLocation(
  search = typeof window === "undefined" ? "" : window.location.search,
): string | null {
  if (!search) return null;
  const raw = new URLSearchParams(search).get("memberId");
  if (!raw) return null;
  return GUID_RE.test(raw) ? raw.toLowerCase() : null;
}
