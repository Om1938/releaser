import type { ComponentProps } from "react";
import { Controller, type Control, type FieldPath, type FieldValues } from "react-hook-form";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";

interface TextFieldProps<T extends FieldValues> {
  control: Control<T>;
  name: FieldPath<T>;
  label: string;
  description?: string;
  multiline?: boolean;
  /** Stores the value as a number (empty input becomes an empty string so validation reports it). */
  numeric?: boolean;
  inputProps?: Omit<ComponentProps<"input">, "name" | "value" | "onChange" | "onBlur">;
  rows?: number;
}

/** A labelled, validated text input bound to React Hook Form (shadcn Field pattern). */
export function TextField<T extends FieldValues>({ control, name, label, description, multiline, numeric, inputProps, rows }: TextFieldProps<T>) {
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => {
        const id = `field-${name}`;
        const common = {
          ...field,
          value: field.value ?? "",
          id,
          "aria-invalid": fieldState.invalid,
          onChange: (event: { target: { value: string } }) =>
            field.onChange(numeric && event.target.value !== "" ? Number(event.target.value) : event.target.value),
        };
        return (
          <Field data-invalid={fieldState.invalid}>
            <FieldLabel htmlFor={id}>{label}</FieldLabel>
            {multiline ? <Textarea {...common} rows={rows ?? 4} /> : <Input {...common} type={numeric ? "number" : undefined} {...inputProps} />}
            {description && <FieldDescription>{description}</FieldDescription>}
            {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
          </Field>
        );
      }}
    />
  );
}
