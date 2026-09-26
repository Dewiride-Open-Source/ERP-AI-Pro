import { createCn } from "cn/config";

// The type roles are font sizes, but the default merge tables read an unknown text-* class as a colour: text-title would
// stay beside text-base and lose to it, and drop a text colour instead.
export const cn = createCn({
  extend: { classGroups: { "font-size": [{ text: ["title", "heading", "body", "caption", "eyebrow"] }] } },
});
