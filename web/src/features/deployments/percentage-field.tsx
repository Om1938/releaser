import { Controller, type Control, type FieldPath, type FieldValues } from "react-hook-form";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Slider } from "@/components/ui/slider";

/** Rollout percentage with a slider and an exact numeric input; explains eligibility vs adoption. */
export function PercentageField<T extends FieldValues>({ control, name }: { control: Control<T>; name: FieldPath<T> }) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <Field data-invalid={fieldState.invalid}>
          <FieldLabel htmlFor="rollout-percentage">Rollout percentage</FieldLabel>
          <div className="flex items-center gap-3">
            <Slider
              value={[Number(field.value) || 0]}
              min={0}
              max={100}
              step={1}
              onValueChange={([value]) => field.onChange(value)}
              aria-label="Rollout percentage slider"
              className="flex-1"
            />
            <Input
              id="rollout-percentage"
              type="number"
              inputMode="decimal"
              min={0}
              max={100}
              step={0.01}
              className="w-24"
              value={field.value}
              onChange={(event) => field.onChange(event.target.value === "" ? "" : Number(event.target.value))}
              aria-invalid={fieldState.invalid}
            />
          </div>
          <FieldDescription>
            Share of the audience <strong>eligible</strong> to be offered this release — not how many will install it. Cohorts are deterministic:
            raising the percentage only adds installations.
          </FieldDescription>
          {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
        </Field>
      )}
    />
  );
}

export const percentageRule = {
  message: "Use 0-100 with at most two decimals.",
  test: (value: number) => value >= 0 && value <= 100 && Math.round(value * 100) === value * 100,
};
