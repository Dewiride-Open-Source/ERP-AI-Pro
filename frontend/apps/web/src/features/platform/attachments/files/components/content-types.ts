const labels: Readonly<Record<string, string>> = {
  "application/pdf": "PDF",
  "image/png": "PNG image",
  "image/jpeg": "JPEG image",
  "image/gif": "GIF image",
  "image/webp": "WebP image",
  "text/plain": "Text",
  "text/csv": "CSV",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": "Excel workbook",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document": "Word document",
};

export function contentTypeLabel(contentType: string | null | undefined): string {
  if (!contentType) return "Unknown type";
  return labels[contentType] ?? contentType;
}
