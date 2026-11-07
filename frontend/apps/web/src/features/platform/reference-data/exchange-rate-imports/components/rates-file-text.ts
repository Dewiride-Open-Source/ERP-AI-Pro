export type FileTextRefusal = "not-csv" | "not-utf8" | "unreadable";

export type RatesFileText = { readonly text: string } | { readonly refusal: FileTextRefusal };

// Windows names a .csv file's type after the program that opens it, often Excel's, so the type alone cannot tell a CSV file
// from a workbook; the file's name decides, and the reading of its text decides the rest.
export const ratesFileTypes: readonly string[] = ["text/csv", "application/vnd.ms-excel", "text/plain"];

const ratesFileName = /\.(?:csv|txt)$/i;

// A file whose bytes are not UTF-8, such as Excel's "CSV (Comma delimited)" in the Windows code page, is refused rather than
// read with replacement characters, which would record a source such as "Zürich desk" mangled; the byte order mark that
// Excel's "CSV UTF-8" starts with is not part of the text.
export async function readFileText(name: string, bytes: () => Promise<ArrayBuffer>): Promise<RatesFileText> {
  if (!ratesFileName.test(name)) return { refusal: "not-csv" };
  let content: ArrayBuffer;
  try {
    content = await bytes();
  } catch {
    return { refusal: "unreadable" };
  }
  try {
    return { text: new TextDecoder("utf-8", { fatal: true }).decode(content) };
  } catch {
    return { refusal: "not-utf8" };
  }
}
