const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

export function formatDateTime(value: string | null | undefined): string {
  return value ? dateTime.format(new Date(value)) : "—";
}

export function formatPercent(value: number): string {
  return `${Number.isInteger(value) ? value : value.toFixed(2)}%`;
}

export const platformLabels = {
  Windows: "Windows",
  MacOS: "macOS",
  LinuxX64: "Linux x64",
  LinuxArm64: "Linux arm64",
  LinuxArmv7l: "Linux armv7l",
} as const;
