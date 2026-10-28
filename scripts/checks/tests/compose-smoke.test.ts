import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  adaptedConfigProblems,
  directoryProblem,
  forwardSecretAeadSuite,
  keyRing,
  proxyData,
  proxyLogProblems,
  tls12SuiteProblem,
  type AdaptedConfig,
} from '../compose-smoke.ts';
import type { TarEntry } from '../lib/image-contents.ts';

const keyRingFolder: TarEntry = { name: 'DataProtection-Keys/', type: '5', mode: 0o700, uid: 1654, gid: 1654 };

test('the key ring directory passes only as a directory owned by 1654:1654 with mode 0700', () => {
  assert.equal(directoryProblem(keyRing, keyRingFolder), undefined);
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
      directoryProblem(keyRing, { ...keyRingFolder, ...change }),
      `api image /home/app/.local/share/ERP-AI-Pro/DataProtection-Keys: expected a directory owned by 1654:1654 with mode 0700, ${message}`,
    );
  }
  assert.equal(
    directoryProblem(keyRing, { ...keyRingFolder, name: 'DataProtection-Keys/key-0.xml' }),
    'api image /home/app/.local/share/ERP-AI-Pro/DataProtection-Keys: expected the archive to start with "DataProtection-Keys/", got "DataProtection-Keys/key-0.xml"',
  );
});

test('the proxy data directory passes only as a directory owned by 10001:10001 with mode 0700', () => {
  const proxyFolder: TarEntry = { name: 'data/', type: '5', mode: 0o700, uid: 10001, gid: 10001 };
  assert.equal(directoryProblem(proxyData, proxyFolder), undefined);
  const refused: [Partial<TarEntry>, string][] = [
    [{ uid: 0, gid: 0, mode: 0o755 }, 'got a directory owned by 0:0 with mode 0755'],
    [{ mode: 0o1777 }, 'got a directory owned by 10001:10001 with mode 1777'],
    [{ uid: 1654 }, 'got a directory owned by 1654:10001 with mode 0700'],
  ];
  for (const [change, message] of refused) {
    assert.equal(directoryProblem(proxyData, { ...proxyFolder, ...change }), `proxy image /data: expected a directory owned by 10001:10001 with mode 0700, ${message}`);
  }
  assert.equal(directoryProblem(proxyData, { ...proxyFolder, name: 'caddy/' }), 'proxy image /data: expected the archive to start with "data/", got "caddy/"');
});

test('a TLS 1.2 suite passes only as ECDHE with AES-GCM or ChaCha20-Poly1305', () => {
  for (const suite of ['ECDHE-ECDSA-AES128-GCM-SHA256', 'ECDHE-ECDSA-AES256-GCM-SHA384', 'ECDHE-ECDSA-CHACHA20-POLY1305', 'ECDHE-RSA-AES128-GCM-SHA256', 'ECDHE-RSA-CHACHA20-POLY1305']) {
    assert.match(suite, forwardSecretAeadSuite);
  }
  for (const suite of ['AES128-GCM-SHA256', 'DHE-RSA-AES128-GCM-SHA256', 'ECDHE-ECDSA-AES128-SHA', 'ECDHE-RSA-AES256-SHA384', 'ECDHE-RSA-DES-CBC3-SHA', 'ECDHE-ECDSA-AES128-GCM-SHA256-X']) {
    assert.doesNotMatch(suite, forwardSecretAeadSuite);
  }
});

test('the accepted TLS 1.2 suites pass only when there is one and every one is forward-secret AEAD', () => {
  assert.equal(tls12SuiteProblem(['ECDHE-ECDSA-AES128-GCM-SHA256', 'ECDHE-ECDSA-CHACHA20-POLY1305']), undefined);
  assert.equal(tls12SuiteProblem([]), 'proxy TLS: no TLS 1.2 suite was accepted');
  assert.equal(
    tls12SuiteProblem(['ECDHE-ECDSA-AES128-GCM-SHA256', 'ECDHE-ECDSA-AES128-SHA', 'AES256-GCM-SHA384']),
    'proxy TLS: accepted TLS 1.2 suites that are not forward-secret AEAD: ECDHE-ECDSA-AES128-SHA, AES256-GCM-SHA384',
  );
});

const accessLine = (request: Record<string, unknown>, extra: Record<string, unknown> = {}): string =>
  `proxy-1  | ${JSON.stringify({ level: 'info', logger: 'http.log.access.log1', msg: 'handled request', request: { remote_ip: '172.28.0.1', method: 'GET', host: '127.0.0.1:8443', ...request }, status: 200, ...extra })}`;

test('the proxy log passes when its entries hold no query string, header or marker', () => {
  const log = [
    'proxy-1  | {"level":"info","logger":"tls.obtain","msg":"certificate obtained successfully","identifier":"127.0.0.1"}',
    accessLine({ uri: '/api/identity/users/directory' }),
    'proxy-1  | {"level":"error","logger":"http.log.error.log1","msg":"dial tcp: lookup api: i/o timeout","request":{"uri":"/api/platform/system-info"},"status":502}',
    '',
  ].join('\n');
  assert.deepEqual(proxyLogProblems(log, ['smokemarker']), []);
});

test('the proxy log fails on a marker, a query string, request or response headers, a line that is not JSON and a log without access entries', () => {
  assert.deepEqual(proxyLogProblems(accessLine({ uri: '/api/platform/attachments/1/content?link=smokemarker' }), ['smokemarker']), [
    'proxy log: holds the query value smokemarker',
    'proxy log: http.log.access.log1 holds the query string of /api/platform/attachments/1/content',
  ]);
  assert.deepEqual(proxyLogProblems(accessLine({ uri: '/login', headers: { 'User-Agent': ['x'] } }), []), ['proxy log: http.log.access.log1 holds request headers']);
  assert.deepEqual(proxyLogProblems(accessLine({ uri: '/login' }, { resp_headers: { Location: ['/'] } }), []), ['proxy log: http.log.access.log1 holds response headers']);
  assert.deepEqual(proxyLogProblems(`${accessLine({ uri: '/login' })}\nproxy-1  | panic: {broken`, []), ['proxy log: a line is not JSON: proxy-1  | panic: {broken']);
  assert.deepEqual(proxyLogProblems('proxy-1  | {"level":"error","logger":"http.log.error","request":{"uri":"/healthz?probe=1"}}', []), [
    'proxy log: http.log.error holds the query string of /healthz',
    'proxy log: holds no access entry',
  ]);
});

const seconds = (value: number): number => value * 1_000_000_000;

const upstream = (dial: string) => ({ handler: 'reverse_proxy', transport: { protocol: 'http', response_header_timeout: seconds(660), read_timeout: seconds(660) }, upstreams: [{ dial }] });

const adaptedConfig = (change: { server?: Record<string, number>; apiLimit?: number; webLimit?: number; upstreamTimeout?: number } = {}): AdaptedConfig => {
  const timeouts = { read_timeout: seconds(660), write_timeout: seconds(660), read_idle_timeout: seconds(60), write_idle_timeout: seconds(60), ...change.server };
  const api = { ...upstream('api:8080'), transport: { response_header_timeout: change.upstreamTimeout ?? seconds(660), read_timeout: seconds(660) } };
  return {
    apps: {
      http: {
        servers: {
          srv0: {
            ...timeouts,
            routes: [
              {
                match: [{ host: ['127.0.0.1'] }],
                handle: [
                  {
                    handler: 'subroute',
                    routes: [
                      { match: [{ path: ['/api/*'] }], handle: [{ handler: 'subroute', routes: [{ handle: [{ handler: 'request_body', max_size: change.apiLimit ?? 101 * 1024 * 1024 }, api] }] }] },
                      { handle: [{ handler: 'subroute', routes: [{ handle: [{ handler: 'request_body', max_size: change.webLimit ?? 2 * 1024 * 1024 }, upstream('web:3000')] }] }] },
                    ],
                  },
                ],
              },
            ],
          },
          srv1: { ...timeouts, routes: [{ handle: [{ handler: 'static_response' }] }] },
        },
      },
    },
  };
};

test('the adapted proxy configuration passes with ceilings above ten minutes, one-minute stall limits and body limits around the upstream limits', () => {
  assert.deepEqual(adaptedConfigProblems(adaptedConfig()), []);
});

test('the adapted proxy configuration fails on a short or missing ceiling, another stall limit, an upstream timeout of ten minutes or less and a body limit outside its range', () => {
  assert.deepEqual(adaptedConfigProblems({}), ['proxy configuration: holds no server']);
  assert.deepEqual(adaptedConfigProblems(adaptedConfig({ server: { write_timeout: seconds(600), read_timeout: 0 } })), [
    'proxy configuration: srv0 read_timeout is 0 s, not above 600 s',
    'proxy configuration: srv0 write_timeout is 600 s, not above 600 s',
    'proxy configuration: srv1 read_timeout is 0 s, not above 600 s',
    'proxy configuration: srv1 write_timeout is 600 s, not above 600 s',
  ]);
  assert.deepEqual(adaptedConfigProblems(adaptedConfig({ server: { read_idle_timeout: seconds(300) } })), [
    'proxy configuration: srv0 read_idle_timeout is 300 s, not 60 s',
    'proxy configuration: srv1 read_idle_timeout is 300 s, not 60 s',
  ]);
  assert.deepEqual(adaptedConfigProblems(adaptedConfig({ upstreamTimeout: seconds(600) })), ['proxy configuration: srv0 upstream response_header_timeout is 600 s, not above 600 s']);
  assert.deepEqual(adaptedConfigProblems(adaptedConfig({ apiLimit: 100 * 1024 * 1024 + 64 * 1024 })), ['proxy configuration: the request body limit on /api/* is 104923136 bytes, not above 104923136']);
  assert.deepEqual(adaptedConfigProblems(adaptedConfig({ webLimit: 10 * 1024 * 1024 })), ['proxy configuration: the request body limit outside /api/* is 10485760 bytes, not between 1048576 and 10485760']);
  assert.deepEqual(adaptedConfigProblems(adaptedConfig({ webLimit: 1024 * 1024 })), ['proxy configuration: the request body limit outside /api/* is 1048576 bytes, not between 1048576 and 10485760']);
});
