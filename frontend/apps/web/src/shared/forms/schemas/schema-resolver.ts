import {
  get,
  set,
  type FieldError,
  type FieldErrors,
  type FieldValues,
  type Resolver,
} from "react-hook-form";
import type { z } from "zod";

import { issuePath } from "../errors/field-paths.ts";

export function schemaResolver<TSchema extends z.ZodType<unknown, FieldValues>>(
  schema: TSchema,
): Resolver<z.input<TSchema>, unknown, z.output<TSchema>> {
  return async (values) => {
    const result = await schema.safeParseAsync(values);
    if (result.success) return { values: result.data, errors: {} };
    return { values: {}, errors: nestedErrors(result.error.issues, values) };
  };
}

// react-hook-form reads resolver errors as a tree shaped like the values, keeps the error of a list itself under the
// list's "root" and a path-less error under the form's "root"; with criteriaMode "firstError" each field keeps its first.
function nestedErrors<TValues extends FieldValues>(
  issues: readonly z.core.$ZodIssue[],
  values: TValues,
): FieldErrors<TValues> {
  const errors: FieldErrors<TValues> = {};
  for (const issue of issues) {
    const path = issuePath(issue.path);
    const target = path === "" ? "root" : Array.isArray(get(values, path)) ? `${path}.root` : path;
    const existing: unknown = get(errors, target);
    if (isFieldError(existing)) continue;

    const error: FieldError = { type: issue.code, message: issue.message };
    set(errors, target, isRecord(existing) ? { ...existing, ...error } : error);
  }
  return errors;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function isFieldError(value: unknown): boolean {
  return isRecord(value) && typeof value.type === "string";
}
