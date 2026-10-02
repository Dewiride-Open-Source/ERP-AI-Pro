import assert from 'node:assert/strict';
import { test } from 'node:test';
import { keyRingDirectoryProblem, readFirstTarEntry, type TarEntry } from '../compose-smoke.ts';

type HeaderFields = { name: string; type: string; mode: number; uid: number; gid: number; prefix?: string; magic?: string; modeField?: string };

const ustarArchive = (fields: HeaderFields): Uint8Array => {
  const archive = new Uint8Array(512 * 3);
  const write = (offset: number, value: string) => archive.set(new TextEncoder().encode(value), offset);
  const octal = (offset: number, length: number, value: number) => write(offset, `${value.toString(8).padStart(length - 1, '0')}\0`);
  write(0, fields.name);
  if (fields.modeField === undefined) octal(100, 8, fields.mode);
  else write(100, fields.modeField);
  octal(108, 8, fields.uid);
  octal(116, 8, fields.gid);
  octal(124, 12, 0);
  octal(136, 12, 0);
  write(156, fields.type);
  write(257, fields.magic ?? 'ustar\u000000');
  if (fields.prefix) write(345, fields.prefix);
  return archive;
};

const keyRingFolder: TarEntry = { name: 'DataProtection-Keys/', type: '5', mode: 0o700, uid: 1654, gid: 1654 };

test('readFirstTarEntry reads the name, type, mode and owner of the first ustar header', () => {
  assert.deepEqual(readFirstTarEntry(ustarArchive(keyRingFolder)), keyRingFolder);
  assert.deepEqual(readFirstTarEntry(ustarArchive({ ...keyRingFolder, magic: 'ustar  \0' })), keyRingFolder);
  assert.deepEqual(readFirstTarEntry(ustarArchive({ ...keyRingFolder, modeField: '   700 \0' })), keyRingFolder);
});

test('readFirstTarEntry joins the ustar prefix to the name', () => {
  assert.equal(readFirstTarEntry(ustarArchive({ ...keyRingFolder, prefix: 'home/app/.local/share/ERP-AI-Pro' })).name, 'home/app/.local/share/ERP-AI-Pro/DataProtection-Keys/');
});

test('readFirstTarEntry refuses a stream that is shorter than a header, not ustar, or holds a non-octal number', () => {
  assert.throws(() => readFirstTarEntry(new Uint8Array(100)), /holds 100 bytes, less than one tar header/);
  assert.throws(() => readFirstTarEntry(ustarArchive({ ...keyRingFolder, magic: '\0\0\0\0\0\0' })), /does not start with a ustar header/);
  assert.throws(() => readFirstTarEntry(ustarArchive({ ...keyRingFolder, modeField: '0000789\0' })), /field at byte 100 is not an octal number \("0000789"\)/);
  assert.throws(() => readFirstTarEntry(ustarArchive({ ...keyRingFolder, modeField: '\0' })), /field at byte 100 is not an octal number \(""\)/);
});

test('the key ring directory passes only as a directory owned by 1654:1654 with mode 0700', () => {
  assert.equal(keyRingDirectoryProblem(keyRingFolder), undefined);
  const refused: [Partial<TarEntry>, string][] = [
    [{ uid: 0, gid: 0, mode: 0o755 }, 'got a directory owned by 0:0 with mode 0755'],
    [{ gid: 0 }, 'got a directory owned by 1654:0 with mode 0700'],
    [{ mode: 0o750 }, 'got a directory owned by 1654:1654 with mode 0750'],
    [{ mode: 0o2700 }, 'got a directory owned by 1654:1654 with mode 2700'],
    [{ type: '0' }, 'got a file owned by 1654:1654 with mode 0700'],
    [{ type: 'x' }, "got a tar entry of type 'x' owned by 1654:1654 with mode 0700"],
  ];
  for (const [change, message] of refused) {
    assert.equal(
      keyRingDirectoryProblem({ ...keyRingFolder, ...change }),
      `api image /home/app/.local/share/ERP-AI-Pro/DataProtection-Keys: expected a directory owned by 1654:1654 with mode 0700, ${message}`,
    );
  }
  assert.equal(
    keyRingDirectoryProblem({ ...keyRingFolder, name: 'DataProtection-Keys/key-0.xml' }),
    'api image /home/app/.local/share/ERP-AI-Pro/DataProtection-Keys: expected the archive to start with "DataProtection-Keys/", got "DataProtection-Keys/key-0.xml"',
  );
});
