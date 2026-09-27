import { createCn } from "cn/config";

export const typeRoles = ["title", "heading", "body", "caption", "eyebrow"] as const;

// The merge tables know only Tailwind's own scales: a named token such as text-title or px-gutter would otherwise be read
// as a colour or an unknown class, kept beside the utility it should replace, and win or lose by stylesheet order.
export const cn = createCn({
  extend: {
    theme: {
      spacing: ["gutter", "section", "header"],
      container: ["page"],
      ease: ["standard", "enter"],
    },
    classGroups: { "font-size": [{ text: [...typeRoles] }] },
  },
});
