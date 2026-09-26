import assert from "node:assert/strict";
import { test } from "node:test";

import {
  composite,
  contrastRatio,
  parseOklch,
  relativeLuminance,
  toSrgb,
  type OklchColour,
  type SrgbColour,
} from "./oklch.ts";

const white: SrgbColour = { red: 1, green: 1, blue: 1 };
const black: SrgbColour = { red: 0, green: 0, blue: 0 };

function assertClose(actual: number, expected: number, tolerance: number, label: string): void {
  assert.ok(
    Math.abs(actual - expected) <= tolerance,
    `${label}: expected ${expected} ± ${tolerance}, got ${actual}`,
  );
}

function assertSrgbClose(actual: SrgbColour, expected: SrgbColour, tolerance: number): void {
  assertClose(actual.red, expected.red, tolerance, "red");
  assertClose(actual.green, expected.green, tolerance, "green");
  assertClose(actual.blue, expected.blue, tolerance, "blue");
}

// CSS Color Module Level 4, "Sample code for Color Conversions": lin_sRGB, lin_sRGB_to_XYZ, XYZ_to_OKLab and
// OKLab_to_OKLCH, the forward direction of the conversion under test.
function oklchFromSrgb(colour: SrgbColour): OklchColour {
  const linear = [colour.red, colour.green, colour.blue].map((channel) =>
    channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4,
  );
  const xyz = multiply(
    [
      [506752 / 1228815, 87881 / 245763, 12673 / 70218],
      [87098 / 409605, 175762 / 245763, 12673 / 175545],
      [7918 / 409605, 87881 / 737289, 1001167 / 1053270],
    ],
    linear,
  );
  const lms = multiply(
    [
      [0.819022437996703, 0.3619062600528904, -0.1288737815209879],
      [0.0329836539323885, 0.9292868615863434, 0.0361446663506424],
      [0.0481771893596242, 0.2642395317527308, 0.6335478284694309],
    ],
    xyz,
  );
  const [lightness = 0, a = 0, b = 0] = multiply(
    [
      [0.210454268309314, 0.7936177747023054, -0.0040720430116193],
      [1.9779985324311684, -2.4285922420485799, 0.450593709617411],
      [0.0259040424655478, 0.7827717124575296, -0.8086757549230774],
    ],
    lms.map((channel) => Math.cbrt(channel)),
  );
  return { lightness, chroma: Math.hypot(a, b), hue: (Math.atan2(b, a) * 180) / Math.PI, alpha: 1 };
}

function multiply(matrix: number[][], vector: number[]): number[] {
  return matrix.map((row) => row.reduce((sum, value, index) => sum + value * (vector[index] ?? 0), 0));
}

test("parseOklch_PlainComponents_ParsesAnOpaqueColour", () => {
  assert.deepEqual(parseOklch("oklch(0.5 0.22 275)"), { lightness: 0.5, chroma: 0.22, hue: 275, alpha: 1 });
  assert.deepEqual(parseOklch("  oklch( .985 0.004 260 )  "), {
    lightness: 0.985,
    chroma: 0.004,
    hue: 260,
    alpha: 1,
  });
});

test("parseOklch_AlphaPercent_ParsesAlpha", () => {
  assert.deepEqual(parseOklch("oklch(1 0 0 / 35%)"), { lightness: 1, chroma: 0, hue: 0, alpha: 0.35 });
  assert.equal(parseOklch("oklch(0.2 0.03 265 / 100%)").alpha, 1);
  assert.equal(parseOklch("oklch(0.2 0.03 265 / 0%)").alpha, 0);
});

test("parseOklch_AlphaNumber_ParsesAlpha", () => {
  assert.equal(parseOklch("oklch(0.2 0.03 265 / 0.1)").alpha, 0.1);
  assert.equal(parseOklch("oklch(0.2 0.03 265/.5)").alpha, 0.5);
});

test("parseOklch_UnsupportedSyntax_ThrowsSyntaxError", () => {
  for (const value of [
    "",
    "rgb(0 0 0)",
    "oklch(50% 0.1 20)",
    "oklch(0.5 0.1 20deg)",
    "oklch(0.5, 0.1, 20)",
    "oklch(0.5 0.1)",
    "oklch(0.5 0.1 20 / )",
    "oklch(none 0.1 20)",
  ]) {
    assert.throws(() => parseOklch(value), SyntaxError, value);
  }
});

test("parseOklch_ComponentOutOfRange_ThrowsRangeError", () => {
  for (const value of [
    "oklch(1.01 0 0)",
    "oklch(-0.1 0 0)",
    "oklch(0.5 -0.1 20)",
    "oklch(0.5 1e999 20)",
    "oklch(0.5 0.1 1e999)",
    "oklch(0.5 0.1 20 / 101%)",
    "oklch(0.5 0.1 20 / 1.5)",
  ]) {
    assert.throws(() => parseOklch(value), RangeError, value);
  }
});

test("toSrgb_Oklch100_IsWhite", () => {
  const conversion = toSrgb(parseOklch("oklch(1 0 0)"));
  assertSrgbClose(conversion.colour, white, 1e-12);
  assert.equal(conversion.inGamut, true);
});

test("toSrgb_Oklch0_IsBlack", () => {
  const conversion = toSrgb(parseOklch("oklch(0 0 0)"));
  assertSrgbClose(conversion.colour, black, 1e-12);
  assert.equal(conversion.inGamut, true);
});

test("toSrgb_KnownMidColour_RoundTripsThroughTheForwardConversion", () => {
  const original: SrgbColour = { red: 0.25, green: 0.5, blue: 0.75 };
  const oklch = oklchFromSrgb(original);
  const conversion = toSrgb(oklch);
  assertSrgbClose(conversion.colour, original, 1e-9);
  assert.equal(conversion.inGamut, true);
  assert.ok(oklch.lightness > 0.4 && oklch.lightness < 0.7, `mid lightness, got ${oklch.lightness}`);
});

test("toSrgb_BeyondTheSrgbGamut_ClampsAndFlagsIt", () => {
  const conversion = toSrgb(parseOklch("oklch(0.7 0.35 145)"));
  assert.equal(conversion.inGamut, false);
  for (const channel of [conversion.colour.red, conversion.colour.green, conversion.colour.blue]) {
    assert.ok(channel >= 0 && channel <= 1, `channel ${channel} is outside 0..1`);
  }
});

test("composite_AlphaAtItsBounds_ReturnsTheBackgroundOrTheForeground", () => {
  const foreground: SrgbColour = { red: 0.8, green: 0.2, blue: 0.4 };
  const background: SrgbColour = { red: 0.1, green: 0.6, blue: 0.9 };
  assert.deepEqual(composite(foreground, 0, background), background);
  assert.deepEqual(composite(foreground, 1, background), foreground);
});

test("composite_PartialAlpha_MixesTheEncodedChannels", () => {
  assertSrgbClose(composite(white, 0.5, black), { red: 0.5, green: 0.5, blue: 0.5 }, 1e-12);
  assertSrgbClose(
    composite({ red: 1, green: 0, blue: 0.5 }, 0.1, { red: 0, green: 1, blue: 0.5 }),
    { red: 0.1, green: 0.9, blue: 0.5 },
    1e-12,
  );
});

test("composite_AlphaOutOfRange_ThrowsRangeError", () => {
  for (const alpha of [-0.01, 1.01, Number.NaN]) {
    assert.throws(() => composite(white, alpha, black), RangeError, String(alpha));
  }
});

test("relativeLuminance_WhiteAndBlack_AreOneAndZero", () => {
  assertClose(relativeLuminance(white), 1, 1e-12, "white");
  assert.equal(relativeLuminance(black), 0);
});

test("relativeLuminance_BelowTheLinearThreshold_DividesBy1292", () => {
  assertClose(relativeLuminance({ red: 0.04, green: 0.04, blue: 0.04 }), 0.04 / 12.92, 1e-12, "grey");
});

test("contrastRatio_WhiteOnBlack_Is21", () => {
  assertClose(contrastRatio(white, black), 21, 1e-12, "white on black");
});

test("contrastRatio_ArgumentOrder_IsSymmetric", () => {
  const grey: SrgbColour = { red: 0.46, green: 0.46, blue: 0.46 };
  assert.equal(contrastRatio(grey, white), contrastRatio(white, grey));
  assert.ok(contrastRatio(grey, white) > 4.5 && contrastRatio(grey, white) < 4.6);
});

test("contrastRatio_SameColour_IsOne", () => {
  const colour: SrgbColour = { red: 0.3, green: 0.6, blue: 0.2 };
  assert.equal(contrastRatio(colour, colour), 1);
});
