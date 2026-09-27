import type { z } from "zod";

export function schemaProblems(
  schema: z.ZodType,
  value: unknown,
  path: readonly PropertyKey[] = [],
): readonly string[] | undefined {
  const result = schema.safeParse(value);
  if (result.success) return undefined;

  const messages = result.error.issues
    .filter((issue) => path.every((segment, index) => issue.path[index] === segment))
    .map((issue) => issue.message);
  return messages.length > 0 ? messages : undefined;
}
