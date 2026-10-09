// SidebarProvider of @dewiride/erp-ui writes this cookie whenever the person opens or collapses the sidebar. Its constant
// lives in a client module, which hands a server component a reference instead of the string, so the name is repeated.
export const sidebarStateCookieName = "sidebar_state";

export function sidebarStartsOpen(cookieValue: string | undefined): boolean {
  return cookieValue !== "false";
}
