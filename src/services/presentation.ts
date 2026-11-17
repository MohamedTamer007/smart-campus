/** Short display references only; routes and API calls keep the full identifiers. */
export function recordReference(id: string, prefix: "SC" | "LF" | "CR", reference?: string) {
  return reference?.trim() || `${prefix}-${id.replaceAll("-", "").slice(0, 12).toUpperCase()}`;
}

export function formatRecordDate(value: string, language: string, includeTime = false) {
  const date = new Date(/^\d{4}-\d{2}-\d{2}$/.test(value) ? value + "T12:00:00Z" : value);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat(language === "ar" ? "ar-EG" : "en-GB", {
    dateStyle: "medium",
    ...(includeTime ? { timeStyle: "short" as const } : {}),
    timeZone: "Africa/Cairo",
  }).format(date);
}
