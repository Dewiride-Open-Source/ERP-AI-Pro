export interface OklchColour {
  readonly lightness: number;
  readonly chroma: number;
  readonly hue: number;
  readonly alpha: number;
}

export interface SrgbColour {
  readonly red: number;
  readonly green: number;
  readonly blue: number;
}

export interface SrgbConversion {
  readonly colour: SrgbColour;
  readonly inGamut: boolean;
}

type Vector3 = readonly [number, number, number];

type Matrix3 = readonly [Vector3, Vector3, Vector3];

const numberPattern = String.raw`[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:e[+-]?\d+)?`;

const oklchPattern = new RegExp(
  String.raw`^oklch\(\s*(${numberPattern})\s+(${numberPattern})\s+(${numberPattern})\s*(?:\/\s*(${numberPattern})(%?)\s*)?\)$`,
  "i",
);

const floatingPointTolerance = 1e-9;

// CSS Color Module Level 4, "Sample code for Color Conversions": OKLab_to_XYZ and XYZ_to_lin_sRGB.
const oklabToLms: Matrix3 = [
  [1, 0.3963377773761749, 0.2158037573099136],
  [1, -0.1055613458156586, -0.0638541728258133],
  [1, -0.0894841775298119, -1.2914855480194092],
];

const lmsToXyz: Matrix3 = [
  [1.2268798758459243, -0.5578149944602171, 0.2813910456659647],
  [-0.0405757452148008, 1.112286803280317, -0.0717110580655164],
  [-0.0763729366746601, -0.4214933324022432, 1.5869240198367816],
];

const xyzToLinearSrgb: Matrix3 = [
  [12831 / 3959, -329 / 214, -1974 / 3959],
  [-851781 / 878810, 1648619 / 878810, 36519 / 878810],
  [705 / 12673, -2585 / 12673, 705 / 667],
];

export function parseOklch(value: string): OklchColour {
  const match = oklchPattern.exec(value.trim());
  if (!match) {
    throw new SyntaxError(
      `Expected oklch(L C H) or oklch(L C H / A) with unitless L, C and H, got "${value}".`,
    );
  }

  const lightness = Number(match[1]);
  const chroma = Number(match[2]);
  const hue = Number(match[3]);
  const alpha = parseAlpha(match[4], match[5]);

  if (!(lightness >= 0 && lightness <= 1)) {
    throw new RangeError(`OKLCH lightness must be between 0 and 1, got ${lightness} in "${value}".`);
  }
  if (!(Number.isFinite(chroma) && chroma >= 0)) {
    throw new RangeError(`OKLCH chroma must be a finite number of at least 0, got ${chroma} in "${value}".`);
  }
  if (!Number.isFinite(hue)) {
    throw new RangeError(`OKLCH hue must be a finite number of degrees, got ${hue} in "${value}".`);
  }
  if (!(alpha >= 0 && alpha <= 1)) {
    throw new RangeError(`Alpha must be between 0 and 1 (0% and 100%), got ${alpha} in "${value}".`);
  }

  return { lightness, chroma, hue, alpha };
}

export function toSrgb(colour: OklchColour): SrgbConversion {
  const hueRadians = (colour.hue * Math.PI) / 180;
  const oklab: Vector3 = [
    colour.lightness,
    colour.chroma * Math.cos(hueRadians),
    colour.chroma * Math.sin(hueRadians),
  ];
  const lms = cube(multiply(oklabToLms, oklab));
  const linear = multiply(xyzToLinearSrgb, multiply(lmsToXyz, lms));
  const inGamut = linear.every(
    (channel) => channel >= -floatingPointTolerance && channel <= 1 + floatingPointTolerance,
  );

  return {
    colour: {
      red: encode(clampToUnit(linear[0])),
      green: encode(clampToUnit(linear[1])),
      blue: encode(clampToUnit(linear[2])),
    },
    inGamut,
  };
}

// Compositing and Blending Level 1, §5.1 simple alpha compositing with an opaque backdrop, on encoded sRGB values.
export function composite(foreground: SrgbColour, alpha: number, background: SrgbColour): SrgbColour {
  if (!(alpha >= 0 && alpha <= 1)) {
    throw new RangeError(`Alpha must be between 0 and 1, got ${alpha}.`);
  }

  const mix = (front: number, back: number): number => front * alpha + back * (1 - alpha);
  return {
    red: mix(foreground.red, background.red),
    green: mix(foreground.green, background.green),
    blue: mix(foreground.blue, background.blue),
  };
}

// WCAG 2.2, definition of relative luminance.
export function relativeLuminance(colour: SrgbColour): number {
  return 0.2126 * decode(colour.red) + 0.7152 * decode(colour.green) + 0.0722 * decode(colour.blue);
}

// WCAG 2.2, definition of contrast ratio.
export function contrastRatio(first: SrgbColour, second: SrgbColour): number {
  const firstLuminance = relativeLuminance(first);
  const secondLuminance = relativeLuminance(second);
  const lighter = Math.max(firstLuminance, secondLuminance);
  const darker = Math.min(firstLuminance, secondLuminance);
  return (lighter + 0.05) / (darker + 0.05);
}

function parseAlpha(amount: string | undefined, unit: string | undefined): number {
  if (amount === undefined) {
    return 1;
  }
  return unit === "%" ? Number(amount) / 100 : Number(amount);
}

function multiply(matrix: Matrix3, vector: Vector3): Vector3 {
  return [dot(matrix[0], vector), dot(matrix[1], vector), dot(matrix[2], vector)];
}

function dot(row: Vector3, vector: Vector3): number {
  return row[0] * vector[0] + row[1] * vector[1] + row[2] * vector[2];
}

function cube(vector: Vector3): Vector3 {
  return [vector[0] ** 3, vector[1] ** 3, vector[2] ** 3];
}

function clampToUnit(channel: number): number {
  return Math.min(1, Math.max(0, channel));
}

function encode(linear: number): number {
  return linear > 0.0031308 ? 1.055 * linear ** (1 / 2.4) - 0.055 : 12.92 * linear;
}

function decode(encoded: number): number {
  return encoded <= 0.04045 ? encoded / 12.92 : ((encoded + 0.055) / 1.055) ** 2.4;
}
