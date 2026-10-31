import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { test } from "node:test";

import {
  canonicalBody,
  canonicalText,
  identifiableLicences,
  identifyLicence,
  isCopyrightLine,
  licenceIds,
  licencesFolder,
  spdxExpression,
  splitCopyright,
  TextTable,
  textBase,
  textKey,
} from "../lib/licence-texts.ts";

test("copyright notices are told apart from licence prose, list items and placeholders", () => {
  for (const line of ["Copyright (c) 2015 Sindre Sorhus", "  Copyright 2009 The Go Authors.", "(c) 2010-2020 Foo Ltd", "© 2024 Sasha Koss", "Copyright Joyent, Inc. and other Node contributors.", "(C) Copyright 2014 Bar"]) {
    assert.equal(isCopyrightLine(line), true, line);
  }
  for (const line of [
    "copyright notice and this permission notice shall be included",
    "      copyright statement to Your modifications and may provide additional or",
    "(c) allow anyone who receives a copy of the Modified Version to",
    "Copyright (c) <year> <copyright holders>",
    "Copyright [yyyy] [name of copyright owner]",
    "The above copyright notice and this permission notice",
  ]) {
    assert.equal(isCopyrightLine(line), false, line);
  }
});

test("splitCopyright takes the notices out of a licence file and drops placeholder notices", () => {
  const split = splitCopyright("﻿MIT License\r\n\r\nCopyright (c) 2021 Foo\r\nCopyright   (c) 2022   Bar\r\n\r\n\r\n\r\nPermission is hereby granted.  \r\n");
  assert.deepEqual(split.copyright, ["Copyright (c) 2021 Foo", "Copyright (c) 2022 Bar"]);
  assert.equal(split.body, "MIT License\n\nPermission is hereby granted.");
  assert.equal(splitCopyright(canonicalText("MIT")).copyright.length, 0);
  assert.doesNotMatch(canonicalBody("BSD-3-Clause"), /<year>|<owner>/);
});

test("texts that differ only in their title, case, punctuation, layout or the Apache appendix share one entry", () => {
  const mit = canonicalBody("MIT").replace(/^MIT License\n\n/, "");
  assert.equal(textKey(`The MIT License (MIT)\n\n${mit}`), textKey(mit));
  assert.equal(textKey(`(The MIT License)\n${mit.toUpperCase().replaceAll("\n", " ")}`), textKey(mit));
  const apache = canonicalBody("Apache-2.0");
  const withoutAppendix = apache.slice(0, apache.indexOf("APPENDIX: How to apply"));
  assert.equal(textKey(withoutAppendix), textKey(apache));
  const table = new TextTable();
  assert.equal(table.add("MIT", mit), table.add("MIT", `MIT License\n\n${mit}`));
  assert.equal(table.size, 1);
});

test("a text with anything appended after a licence keeps its own entry", () => {
  const apache = canonicalBody("Apache-2.0");
  assert.notEqual(textKey(`${apache}\n\nThis product bundles FlatBuffers, under the following notice.`), textKey(apache));
  assert.notEqual(textKey(canonicalBody("BSD-3-Clause").replace("the copyright holder", "Google LLC")), textKey(canonicalBody("BSD-3-Clause")));
});

test("a licence text is identified as the SPDX licence it reproduces, organisation names and a missing appendix included", () => {
  assert.equal(identifyLicence(`Copyright (c) 2014 Foo\n\n${canonicalBody("MIT")}`), "MIT");
  const bsd3 = canonicalBody("BSD-3-Clause").replace(/the copyright holder/g, "Google LLC").replace(/COPYRIGHT HOLDERS? AND CONTRIBUTORS/g, "COPYRIGHT OWNER OR CONTRIBUTORS");
  assert.equal(identifyLicence(`Copyright 2009 The Go Authors.\n\n${bsd3}`), "BSD-3-Clause");
  assert.equal(identifyLicence(canonicalBody("BSD-2-Clause")), "BSD-2-Clause");
  const apache = canonicalBody("Apache-2.0");
  assert.equal(identifyLicence(apache.slice(0, apache.indexOf("APPENDIX: How to apply"))), "Apache-2.0");
  for (const id of identifiableLicences) assert.equal(identifyLicence(canonicalText(id)), id, id);
});

test("a text that holds more than one licence, or none of the known ones, is not identified", () => {
  assert.equal(identifyLicence(`${canonicalBody("Apache-2.0")}\n\n${canonicalBody("BSD-3-Clause")}`), undefined);
  assert.equal(identifyLicence(`${canonicalBody("MIT")}\n\n${canonicalBody("MIT")}\n\nPortions are under the terms of the Apache License as well, see below for the whole list.`), undefined);
  assert.equal(identifyLicence("All rights reserved. No part of this software may be copied."), undefined);
  assert.equal(identifyLicence(canonicalText("GPL-2.0")), undefined);
});

test("spdxExpression accepts SPDX expressions in any case and spells them canonically", () => {
  assert.equal(spdxExpression("mit"), "MIT");
  assert.equal(spdxExpression("(MIT OR Apache-2.0)"), "(MIT OR Apache-2.0)");
  assert.equal(spdxExpression("gpl-2.0-or-later or lgpl-3.0-or-later"), "GPL-2.0-or-later OR LGPL-3.0-or-later");
  assert.equal(spdxExpression("GPL-3.0-or-later WITH gcc-exception-3.1"), "GPL-3.0-or-later WITH GCC-exception-3.1");
  assert.equal(spdxExpression("LGPL-2.1+"), "LGPL-2.1+");
  assert.equal(spdxExpression("Apache-2.0 AND (MIT OR BSD-3-Clause)"), "Apache-2.0 AND (MIT OR BSD-3-Clause)");
});

test("spdxExpression refuses free text, unknown identifiers and broken expressions", () => {
  for (const text of ["SIL OPEN FONT LICENSE", "Public Domain", "UNLICENSED", "MIT AND", "(MIT", "MIT WITH Nonsense-Exception", ""]) assert.equal(spdxExpression(text), undefined, text);
});

test("licenceIds and textBase name the licence texts an expression needs", () => {
  assert.deepEqual(licenceIds("BSD-3-Clause OR (GPL-2.0-only AND MIT)"), ["BSD-3-Clause", "GPL-2.0-only", "MIT"]);
  assert.deepEqual(["GPL-2.0-only", "GPL-2.0-or-later", "LGPL-2.1+", "MIT"].map(textBase), ["GPL-2.0", "GPL-2.0", "LGPL-2.1", "MIT"]);
});

test("every canonical text matches the source and digest recorded in sources.json", () => {
  const sources = JSON.parse(readFileSync(join(licencesFolder, "sources.json"), "utf8")) as Record<string, { source: string; sha256: string }>;
  const texts = readdirSync(licencesFolder).filter((file) => file.endsWith(".txt"));
  assert.deepEqual(texts.map((file) => file.slice(0, -4)).sort(), Object.keys(sources).sort());
  for (const file of texts) {
    const entry = sources[file.slice(0, -4)] as { source: string; sha256: string };
    assert.match(entry.source, /^https:\/\/(?:spdx\.org\/licenses\/|www\.gnu\.org\/licenses\/|raw\.githubusercontent\.com\/golang\/go\/)/, file);
    assert.equal(createHash("sha256").update(readFileSync(join(licencesFolder, file))).digest("hex"), entry.sha256, file);
  }
});
