export type CubicBezier = readonly [number, number, number, number];

export type MotionTiming = {
  readonly duration: number;
  readonly ease: CubicBezier;
};

export function durationInSeconds(value: string): number | undefined {
  const match = /^(\d*\.?\d+)(ms|s)$/.exec(value.trim());
  const amount = match?.[1];
  const unit = match?.[2];
  if (amount === undefined || unit === undefined) return undefined;
  return unit === "ms" ? Number(amount) / 1000 : Number(amount);
}

export function cubicBezier(value: string): CubicBezier | undefined {
  const match = /^cubic-bezier\(([^)]*)\)$/.exec(value.trim());
  const parts = match?.[1]?.split(",").map((part) => part.trim());
  if (parts?.length !== 4 || parts.some((part) => part === "")) return undefined;
  const points = parts.map(Number);
  if (points.some((point) => !Number.isFinite(point))) return undefined;
  const [x1, y1, x2, y2] = points as [number, number, number, number];
  if (x1 < 0 || x1 > 1 || x2 < 0 || x2 > 1) return undefined;
  return [x1, y1, x2, y2];
}

export function motionTiming(duration: string, ease: string): MotionTiming | undefined {
  const seconds = durationInSeconds(duration);
  const curve = cubicBezier(ease);
  if (seconds === undefined || curve === undefined) return undefined;
  return { duration: seconds, ease: curve };
}
