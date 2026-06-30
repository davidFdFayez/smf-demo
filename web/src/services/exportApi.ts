import { httpClient } from "./httpClient";

export type ExportKind = "members" | "clubs" | "events";

/**
 * Downloads a CSV report from the API and triggers a browser save.
 * Uses a short-lived Blob URL so no file ends up in IndexedDB/cache.
 */
export async function downloadCsv(kind: ExportKind): Promise<void> {
  const { data, headers } = await httpClient.get(`/api/export/${kind}.csv`, {
    responseType: "blob",
  });

  const blob = data as Blob;
  const contentDisposition = headers["content-disposition"] as string | undefined;
  const fileName = parseFilename(contentDisposition) ?? `${kind}.csv`;

  const url = URL.createObjectURL(blob);
  try {
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
  } finally {
    // Release after next tick so the click has time to fire.
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }
}

function parseFilename(header?: string): string | null {
  if (!header) return null;
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header);
  return match ? decodeURIComponent(match[1]) : null;
}
