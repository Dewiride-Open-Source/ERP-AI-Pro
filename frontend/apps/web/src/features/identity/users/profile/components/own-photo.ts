import { apiBasePath } from "@/shared/api/base-path";

// The browser loads the photo itself, from the web origin, so it carries the session cookie and keeps the photo for that
// sign-in only; the API answers no content for a person the directory holds no photo of, which leaves the initials in place.
export const ownPhotoPath = `${apiBasePath}/identity/users/me/photo`;
