import type { AuthenticationProvider, RequestInformation } from "@microsoft/kiota-abstractions";
import {
  FetchRequestAdapter,
  KiotaClientFactory,
  UserAgentHandler,
} from "@microsoft/kiota-http-fetchlibrary";

import { createErpApiClient, type ErpApiClient } from "./generated/erpApiClient.js";

export type ErpApiFetch = (url: string, init: RequestInit) => Promise<Response>;

export interface ErpApiConnection {
  readonly baseUrl: string;
  readonly headers: Readonly<Record<string, string>>;
  readonly fetch: ErpApiFetch;
}

// An explicit chain leaves out the default retry and redirect handlers, so a failing API call fails once and at once.
export function connect(connection: ErpApiConnection): ErpApiClient {
  const httpClient = KiotaClientFactory.create(connection.fetch, [new UserAgentHandler()]);
  const adapter = new FetchRequestAdapter(
    new ForwardedHeaders(connection.headers),
    undefined,
    undefined,
    httpClient,
  );
  adapter.baseUrl = connection.baseUrl;

  return createErpApiClient(adapter);
}

class ForwardedHeaders implements AuthenticationProvider {
  public constructor(private readonly headers: Readonly<Record<string, string>>) {}

  public authenticateRequest(request: RequestInformation): Promise<void> {
    for (const [name, value] of Object.entries(this.headers)) {
      request.headers.tryAdd(name, value);
    }

    return Promise.resolve();
  }
}
