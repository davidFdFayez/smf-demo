import { httpClient } from "./httpClient";

export type ExportKind = "members" | "clubs" | "events";

export async function downloadCsv(kind: ExportKind): Promise<void> {
  const { data, headers } = await httpClient.get(`/api/export/${kind}.csv`, {
    responseType: "blob",
  });
  const blob = data as Blob;
  const cd = headers["content-disposition"] as string | undefined;
  const fileName = parseFilename(cd) ?? `${kind}.csv`;

  const url = URL.createObjectURL(blob);
  try {
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
  } finally {
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }
}

function parseFilename(header?: string): string | null {
  if (!header) return null;
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header);
  return match ? decodeURIComponent(match[1]) : null;
}
