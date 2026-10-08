import { Checkbox } from "@/components/ui/checkbox";
import { Field, FieldDescription, FieldLabel, FieldLegend, FieldSet } from "@/components/ui/field";
import { platformLabels } from "@/lib/format";
import { allPlatforms, inStandardOrder, type Platform } from "@/lib/platforms";

interface PlatformCheckboxesProps {
  legend: string;
  description?: string;
  value: readonly Platform[];
  onChange: (platforms: Platform[]) => void;
  /** Platforms that can be chosen; defaults to every platform. */
  options?: readonly Platform[];
  idPrefix: string;
  error?: string;
}

/** Accessible checkbox group for choosing platforms; always reports values in the standard order. */
export function PlatformCheckboxes({ legend, description, value, onChange, options = allPlatforms, idPrefix, error }: PlatformCheckboxesProps) {
  return (
    <FieldSet data-invalid={!!error}>
      <FieldLegend variant="label">{legend}</FieldLegend>
      {description && <FieldDescription>{description}</FieldDescription>}
      <div className="flex flex-wrap gap-4">
        {inStandardOrder(options).map((platform) => (
          <Field key={platform} orientation="horizontal" className="w-auto">
            <Checkbox
              id={`${idPrefix}-${platform}`}
              checked={value.includes(platform)}
              aria-invalid={!!error}
              onCheckedChange={(checked) => onChange(inStandardOrder(checked ? [...value, platform] : value.filter((p) => p !== platform)))}
            />
            <FieldLabel htmlFor={`${idPrefix}-${platform}`} className="font-normal">
              {platformLabels[platform]}
            </FieldLabel>
          </Field>
        ))}
      </div>
      {error && <p className="text-sm text-destructive">{error}</p>}
    </FieldSet>
  );
}
