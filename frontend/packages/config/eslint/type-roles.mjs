export const typeRoles = ["title", "heading", "body", "caption", "eyebrow"];

const roleClass = `(^|[\\s:])text-(${typeRoles.join("|")})(\\s|$)`;

const componentClassName =
  'JSXOpeningElement:matches([name.name=/^[A-Z]/], [name.type="JSXMemberExpression"]) > JSXAttribute[name.name="className"]';

const message =
  "Put a type-role class (text-title, text-heading, text-body, text-caption, text-eyebrow) on a plain element: a generated primitive merges className with the default cn, which reads the role as a colour, so the primitive's own text size wins and its text colour can be dropped.";

export const typeRoleRestrictions = [
  { selector: `${componentClassName} Literal[value=/${roleClass}/]`, message },
  { selector: `${componentClassName} TemplateElement[value.raw=/${roleClass}/]`, message },
];
