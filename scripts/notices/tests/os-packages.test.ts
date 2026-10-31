import assert from "node:assert/strict";
import { test } from "node:test";

import { ImageFiles, type ImageFile } from "../lib/container-files.ts";
import { TextTable } from "../lib/licence-texts.ts";
import {
  alpineComponents,
  controlParagraphs,
  debianComponents,
  debianCopyright,
  debianLicenceExpression,
  installedAlpinePackages,
  installedDebianPackages,
} from "../lib/os-packages.ts";

const status = `Package: libc6
Status: install ok installed
Source: glibc
Version: 2.39-0ubuntu8.9
Architecture: amd64
Maintainer: Ubuntu Developers <ubuntu-devel-discuss@lists.ubuntu.com>
Description: GNU C Library
 Shared libraries.
 .
 More text.

Package: libgcc-s1
Status: install ok installed
Source: gcc-14 (14.2.0-4ubuntu2~24.04.1)
Version: 14.2.0-4ubuntu2~24.04.1
Architecture: amd64

Package: removed
Status: deinstall ok config-files
Version: 1.0

Package: tzdata
Version: 2026c-0ubuntu0.24.04.1
Architecture: all
`;

const dep5 = `Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: ca-certificates

Files: *
Copyright: 2003 Fumitoshi UKAI
License: GPL-2+
 This program is free software; see /usr/share/common-licenses/GPL-2.

# Mozilla's certificates
Files: mozilla/*
Copyright: Mozilla Foundation
License: MPL-2.0
 The full text is in /usr/share/common-licenses/MPL-2.0'.

License: MIT
 A stand-alone licence paragraph names no files.
`;

const freeForm = `This is the Debian prepackaged version of the GNU C Library.

  On Debian systems, the complete text of the GNU Library General Public License can be found in
  \`/usr/share/common-licenses/LGPL-2.1'. See /usr/share/common-licenses/GPL.
`;

test("controlParagraphs reads fields, continuation lines and paragraphs and skips comment lines", () => {
  const [first, , third] = controlParagraphs(dep5);
  assert.equal(first?.get("format"), "https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/");
  assert.equal(third?.get("license"), "MPL-2.0\nThe full text is in /usr/share/common-licenses/MPL-2.0'.");
  assert.throws(() => controlParagraphs("Package: a\nnot a field\n"), /neither a field nor a continuation/);
});

test("installedDebianPackages lists the installed packages with their source package and version", () => {
  assert.deepEqual(installedDebianPackages(status), [
    { name: "libc6", version: "2.39-0ubuntu8.9", architecture: "amd64", source: "glibc", sourceVersion: "2.39-0ubuntu8.9", maintainer: "Ubuntu Developers <ubuntu-devel-discuss@lists.ubuntu.com>" },
    { name: "libgcc-s1", version: "14.2.0-4ubuntu2~24.04.1", architecture: "amd64", source: "gcc-14", sourceVersion: "14.2.0-4ubuntu2~24.04.1", maintainer: "" },
    { name: "tzdata", version: "2026c-0ubuntu0.24.04.1", architecture: "all", source: "tzdata", sourceVersion: "2026c-0ubuntu0.24.04.1", maintainer: "" },
  ]);
});

test("a machine-readable copyright file names its licences and the common licence texts it refers to", () => {
  assert.deepEqual(debianCopyright(dep5), { machineReadable: true, licences: ["GPL-2+", "MPL-2.0"], commonLicences: ["GPL-2", "MPL-2.0"] });
  assert.deepEqual(debianCopyright(freeForm), { machineReadable: false, licences: [], commonLicences: ["GPL", "LGPL-2.1"] });
});

test("Debian licence names become an SPDX expression only when every name maps to one", () => {
  assert.equal(debianLicenceExpression(debianCopyright(dep5)), "GPL-2.0-or-later AND MPL-2.0");
  assert.equal(debianLicenceExpression({ machineReadable: true, licences: ["Artistic or GPL-1+", "Apache-2.0"], commonLicences: [] }), "(Artistic-1.0-Perl OR GPL-1.0-or-later) AND Apache-2.0");
  assert.equal(debianLicenceExpression({ machineReadable: true, licences: ["GPL-3+ with GCC exception"], commonLicences: [] }), "GPL-3.0-or-later WITH GCC-exception-3.1");
  assert.equal(debianLicenceExpression({ machineReadable: true, licences: ["GPL-2", "public-domain"], commonLicences: [] }), "NOASSERTION");
  assert.equal(debianLicenceExpression(debianCopyright(freeForm)), "NOASSERTION");
  assert.equal(debianLicenceExpression(undefined), "NOASSERTION");
});

function files(entries: Record<string, string | { link: string }>): ImageFiles {
  const map = new Map<string, ImageFile>();
  for (const [path, value] of Object.entries(entries)) {
    map.set(path, typeof value === "string" ? { path, type: "0", linkName: "", content: new TextEncoder().encode(value) } : { path, type: "2", linkName: value.link, content: undefined });
  }
  return new ImageFiles(map);
}

test("a Debian package's entry points at its copyright file, and at the common licences only an image without them lacks", () => {
  const image = files({
    "var/lib/dpkg/status": status,
    "usr/share/doc/libc6/copyright": freeForm,
    "usr/share/doc/gcc-14-base/copyright": dep5,
    "usr/share/doc/libgcc-s1": { link: "gcc-14-base" },
  });
  const table = new TextTable();
  const [libc6, libgcc, tzdata] = debianComponents(image, { id: "ubuntu", version: "24.04", codename: "noble" }, table);
  assert.equal(libc6?.purl, "pkg:deb/ubuntu/libc6@2.39-0ubuntu8.9?arch=amd64&distro=ubuntu-24.04");
  assert.equal(libc6?.source, "https://launchpad.net/ubuntu/+source/glibc/2.39-0ubuntu8.9");
  assert.deepEqual(libc6?.inImage, ["/usr/share/doc/libc6/copyright", "/usr/share/doc/erp-ai-pro/COPYING"]);
  assert.equal(libc6?.texts.length, 1);
  assert.equal(libc6?.supplier, "Organization: Ubuntu Developers (ubuntu-devel-discuss@lists.ubuntu.com)");
  assert.deepEqual(libgcc?.inImage, ["/usr/share/doc/gcc-14-base/copyright"]);
  assert.equal(libgcc?.licence, "GPL-2.0-or-later AND MPL-2.0");
  assert.equal(tzdata?.detail, "source package tzdata 2026c-0ubuntu0.24.04.1; the image carries no copyright file for it");
  const withCommon = files({ "var/lib/dpkg/status": status, "usr/share/doc/libc6/copyright": freeForm, "usr/share/common-licenses/GPL": { link: "GPL-3" }, "usr/share/common-licenses/GPL-3": "GPL" });
  const [debianLibc6] = debianComponents(withCommon, { id: "debian", version: "12", codename: "bookworm" }, new TextTable());
  assert.deepEqual(debianLibc6?.texts, []);
  assert.deepEqual(debianLibc6?.inImage, ["/usr/share/doc/libc6/copyright", "/usr/share/common-licenses/GPL", "/usr/share/common-licenses/LGPL-2.1"]);
  assert.equal(debianLibc6?.source, "https://snapshot.debian.org/package/glibc/2.39-0ubuntu8.9/");
});

const apk = `C:Q1abc=
P:busybox
V:1.37.0-r30
A:x86_64
L:GPL-2.0-only
o:busybox
m:Sören Tempel <soeren+alpine@soeren-tempel.net>
c:1e823a60eb85606954b3a5af5f8e5bbd1ea680cf
F:bin
R:busybox

P:mailcap
V:2.1.54-r0
A:noarch
L:Public Domain
o:mailcap
c:0c8e332694755cf1b57465c56ae1f3664d341ab6

P:libidn2
V:2.3.8-r0
A:x86_64
L:GPL-2.0-or-later OR LGPL-3.0-or-later
o:libidn2
c:8240e09aa2fdd8f9979b2016be082273965aff0c
`;

test("Alpine packages carry their licence, origin and aports commit, and the texts of the licences the image lacks", () => {
  assert.equal(installedAlpinePackages(apk).length, 3);
  const [busybox, mailcap, libidn2] = alpineComponents(files({ "lib/apk/db/installed": apk }), "3.23.6", new TextTable());
  assert.equal(busybox?.purl, "pkg:apk/alpine/busybox@1.37.0-r30?arch=x86_64&distro=alpine-3.23.6");
  assert.equal(busybox?.source, "https://gitlab.alpinelinux.org/alpine/aports/-/tree/1e823a60eb85606954b3a5af5f8e5bbd1ea680cf/main/busybox");
  assert.equal(busybox?.supplier, "Person: Sören Tempel (soeren+alpine@soeren-tempel.net)");
  assert.equal(busybox?.texts.length, 1);
  assert.equal(mailcap?.licence, "Public Domain");
  assert.deepEqual(mailcap?.texts, []);
  assert.deepEqual(libidn2?.inImage, ["/usr/share/doc/erp-ai-pro/COPYING.LESSER", "/usr/share/doc/erp-ai-pro/COPYING"]);
  assert.throws(() => alpineComponents(files({ "lib/apk/db/installed": "P:x\nV:1\n" }), "3.23.6", new TextTable()), /names no aports commit for x/);
});
