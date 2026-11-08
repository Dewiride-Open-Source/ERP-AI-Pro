"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import {
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLegend,
  FieldSet,
} from "@dewiride/erp-ui/components/ui/field";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import { Label } from "@dewiride/erp-ui/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@dewiride/erp-ui/components/ui/radio-group";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { useState, type ChangeEvent, type ComponentProps, type CompositionEvent, type Ref } from "react";
import { Controller, useFormContext, useWatch } from "react-hook-form";

import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";

import { formatOf, type NumberFormatValues } from "../forms/number-series.schema";

import { maxDigits, prefixMaxLength, yearLabel, type SerialNumberRule } from "./number-format";
import {
  appliesFrom,
  characterLabel,
  digitsDescription,
  fixedNextNumberDescription,
  nextNumberDescription,
  prefixDescription,
  previewText,
  separatorChoiceDescription,
  yearChoiceDescription,
} from "./numbering-text";
import { SegmentedText } from "./segmented-text";

export type NumberSeriesFieldPath =
  "format.prefix" | "format.year" | "format.separator" | "format.digits" | "nextNumber";

export interface FixedNextNumber {
  readonly running: string;
  readonly nextText: string | null;
}

type NumberSeriesFieldValues = { format: NumberFormatValues; nextNumber: string };

type Choice = { readonly value: string; readonly label: string };

const noSeparator = "nothing";

// Both forms hold the format and the next number under the same names, so the fields read them from the form they sit in.
export function NumberFormatFields({
  fieldId,
  rule,
  currentYear,
  fixedNextNumber,
}: {
  fieldId: (name: NumberSeriesFieldPath) => string;
  rule: SerialNumberRule;
  currentYear: string;
  fixedNextNumber: FixedNextNumber | undefined;
}) {
  const { control, register, formState } = useFormContext<NumberSeriesFieldValues>();
  const year = useWatch({ control, name: "format.year" });
  const yearChoices: readonly Choice[] = [
    { value: "none", label: "Not shown" },
    { value: "full", label: yearLabel(currentYear, "full") },
    { value: "abbreviated", label: yearLabel(currentYear, "abbreviated") },
  ];
  const separatorChoices: readonly Choice[] = [
    { value: noSeparator, label: "Nothing" },
    ...rule.specialCharacters
      .split("")
      .map((character) => ({ value: character, label: characterLabel(character) })),
  ];

  return (
    <div className="grid gap-5">
      <FieldGroup>
        <Controller
          control={control}
          name="format.prefix"
          render={({ field, fieldState }) => (
            <FormField
              id={fieldId("format.prefix")}
              label="Starts with"
              description={prefixDescription(rule)}
              errors={fieldErrorMessages(fieldState.error)}
            >
              {(frame) => (
                <PrefixInput
                  {...frame}
                  ref={field.ref}
                  name={field.name}
                  value={field.value}
                  onValueChange={field.onChange}
                  onBlur={field.onBlur}
                  maxLength={prefixMaxLength}
                />
              )}
            </FormField>
          )}
        />
        <Controller
          control={control}
          name="format.year"
          render={({ field, fieldState }) => (
            <ChoiceField
              id={fieldId("format.year")}
              legend="Financial year"
              description={yearChoiceDescription}
              choices={yearChoices}
              value={field.value}
              onValueChange={field.onChange}
              onBlur={field.onBlur}
              ref={field.ref}
              errors={fieldErrorMessages(fieldState.error)}
            />
          )}
        />
        {year === "none" ? null : (
          <Controller
            control={control}
            name="format.separator"
            render={({ field, fieldState }) => (
              <ChoiceField
                id={fieldId("format.separator")}
                legend="After the year"
                description={separatorChoiceDescription}
                choices={separatorChoices}
                value={field.value === "" ? noSeparator : field.value}
                onValueChange={(value) => field.onChange(value === noSeparator ? "" : value)}
                onBlur={field.onBlur}
                ref={field.ref}
                errors={fieldErrorMessages(fieldState.error)}
              />
            )}
          />
        )}
        <FormField
          id={fieldId("format.digits")}
          label="Digits"
          description={digitsDescription}
          required
          errors={fieldErrorMessages(formState.errors.format?.digits)}
        >
          {(frame) => (
            <Input
              {...frame}
              {...register("format.digits")}
              inputMode="numeric"
              autoComplete="off"
              maxLength={String(maxDigits).length}
              className="max-w-24"
            />
          )}
        </FormField>
        {fixedNextNumber === undefined ? (
          <FormField
            id={fieldId("nextNumber")}
            label="Next number"
            description={nextNumberDescription(currentYear)}
            required
            errors={fieldErrorMessages(formState.errors.nextNumber)}
          >
            {(frame) => (
              <Input
                {...frame}
                {...register("nextNumber")}
                inputMode="numeric"
                autoComplete="off"
                maxLength={maxDigits}
                className="max-w-56"
              />
            )}
          </FormField>
        ) : (
          <FormField
            label="Next number"
            description={fixedNextNumberDescription(currentYear, fixedNextNumber.nextText)}
            disabled
          >
            {(frame) => (
              <Input {...frame} value={fixedNextNumber.running} readOnly disabled className="max-w-56" />
            )}
          </FormField>
        )}
      </FieldGroup>

      <NumberPreview rule={rule} currentYear={currentYear} nextNumberFixed={fixedNextNumber !== undefined} />
    </div>
  );
}

function NumberPreview({
  rule,
  currentYear,
  nextNumberFixed,
}: {
  rule: SerialNumberRule;
  currentYear: string;
  nextNumberFixed: boolean;
}) {
  const { control } = useFormContext<NumberSeriesFieldValues>();
  const [format, nextNumber] = useWatch({ control, name: ["format", "nextNumber"] });

  return (
    <div className="grid gap-1 rounded-lg border bg-muted/50 p-3" data-testid="numbering-preview">
      <p className="text-sm font-medium">Preview</p>
      <p className="text-sm wrap-anywhere text-muted-foreground">
        <SegmentedText
          segments={previewText(
            formatOf(format),
            nextNumberFixed ? "" : nextNumber,
            rule,
            appliesFrom(currentYear, nextNumberFixed),
          )}
        />
      </p>
    </div>
  );
}

// The first choice carries the field's own id, so a message about the choice links to and focuses the group.
function ChoiceField({
  id,
  legend,
  description,
  choices,
  value,
  onValueChange,
  onBlur,
  ref,
  errors,
}: {
  id: string;
  legend: string;
  description: string;
  choices: readonly Choice[];
  value: string;
  onValueChange: (value: string) => void;
  onBlur: () => void;
  ref: Ref<HTMLButtonElement>;
  errors: readonly string[] | undefined;
}) {
  const legendId = `${id}-legend`;
  const descriptionId = `${id}-description`;
  const errorId = `${id}-error`;
  const messages = errors?.filter((message) => message !== "") ?? [];
  const invalid = messages.length > 0;

  return (
    <FieldSet data-invalid={invalid || undefined} className="gap-2">
      <FieldLegend id={legendId} variant="label" className="mb-0">
        {legend}
      </FieldLegend>
      <RadioGroup
        aria-labelledby={legendId}
        aria-describedby={invalid ? `${descriptionId} ${errorId}` : descriptionId}
        aria-invalid={invalid || undefined}
        value={value}
        onValueChange={onValueChange}
        className="flex flex-wrap gap-x-5 gap-y-2"
      >
        {choices.map((choice, index) => {
          const itemId = index === 0 ? id : `${id}-${index}`;
          return (
            <div key={choice.value} className="flex items-center gap-2">
              <RadioGroupItem
                id={itemId}
                ref={index === 0 ? ref : undefined}
                value={choice.value}
                onBlur={onBlur}
                aria-invalid={invalid || undefined}
              />
              <Label htmlFor={itemId} className="font-normal">
                {choice.label}
              </Label>
            </div>
          );
        })}
      </RadioGroup>
      <FieldDescription id={descriptionId}>{description}</FieldDescription>
      {invalid ? <FieldError id={errorId} errors={messages.map((message) => ({ message }))} /> : null}
    </FieldSet>
  );
}

// Rewriting the text while an input method is still composing makes mobile keyboards repeat characters, so the composed text is
// shown as typed and upper-cased once the composition ends.
function PrefixInput({
  value,
  onValueChange,
  className,
  onCompositionEnd,
  ...inputProps
}: Omit<ComponentProps<"input">, "value" | "defaultValue" | "onChange" | "type"> & {
  value: string;
  onValueChange: (value: string) => void;
}) {
  const [composition, setComposition] = useState<string | null>(null);

  const commit = (input: HTMLInputElement) => {
    const text = upperCased(input);
    if (text !== value) onValueChange(text);
  };

  const onChange = (event: ChangeEvent<HTMLInputElement>) => {
    if (event.nativeEvent instanceof InputEvent && event.nativeEvent.isComposing) {
      setComposition(event.currentTarget.value);
      return;
    }
    setComposition(null);
    commit(event.currentTarget);
  };

  const handleCompositionEnd = (event: CompositionEvent<HTMLInputElement>) => {
    setComposition(null);
    commit(event.currentTarget);
    onCompositionEnd?.(event);
  };

  return (
    <Input
      {...inputProps}
      type="text"
      autoComplete="off"
      autoCapitalize="characters"
      autoCorrect="off"
      spellCheck={false}
      className={cn("font-mono", className)}
      value={composition ?? value}
      onChange={onChange}
      onCompositionEnd={handleCompositionEnd}
    />
  );
}

// The caret stays after the characters it followed, which upper-casing can lengthen ("ß" becomes "SS").
function upperCased(input: HTMLInputElement): string {
  const raw = input.value;
  const text = raw.toUpperCase();
  if (text === raw) return text;

  const caret = raw.slice(0, input.selectionEnd ?? raw.length).toUpperCase().length;
  input.value = text;
  input.setSelectionRange(caret, caret);
  return text;
}
