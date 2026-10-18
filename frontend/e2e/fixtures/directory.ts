import { expect, type APIRequestContext } from "@playwright/test";

export const directoryPeoplePath = "/api/__test/directory/people";

export type DirectoryPhoto = { readonly contentType: "image/jpeg" | "image/png"; readonly base64: string };

export type DirectoryAccountState = "enabled" | "disabled" | "deleted";

export type DirectoryPersonSeed = {
  readonly objectId: string;
  readonly displayName: string;
  readonly userPrincipalName: string;
  readonly mail?: string;
  readonly photo?: DirectoryPhoto;
  readonly accountState?: DirectoryAccountState;
};

// The end-to-end API host answers Microsoft Graph from its test directory, which holds only the people the specs add, so a
// spec adds every person it searches for, invites or shows the photo of; adding a person again replaces them, which is how a
// spec disables or deletes their account. The request carries no session: the route is the test host's own and takes no
// antiforgery token.
export async function addDirectoryPerson(
  request: APIRequestContext,
  person: DirectoryPersonSeed,
): Promise<void> {
  const response = await request.post(directoryPeoplePath, { data: person });
  expect(response.status(), `adding ${person.displayName} to the test directory`).toBe(204);
}
