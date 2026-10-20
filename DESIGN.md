# Design

> Pointer file for Impeccable. The source of truth is [doc/structure.md](doc/structure.md) §UI Styling Conventions. The visual system already exists in code: preserve it, do not redefine palette, fonts or radii here.

## Authority

The incumbent implementation is the design authority:

- Components: spartan-ng helm in `libs/ui/*` (import `@spartan-ng/helm/*`).
- Tokens: CSS custom properties exposed as Tailwind semantic classes (`bg-primary`, `text-muted-foreground`, `bg-muted`, `border`).
- Base typography: global styles of `apps/web`.

## Priority order

1. spartan-ng components from `libs/ui/*`.
2. Semantic token classes.
3. Global base styles. Do not re-apply them inline.
4. Tailwind layout utilities only (`flex`, `grid`, `gap-*`, `p-*`, `w-*`...).

## Hard rules

- No hardcoded color classes and no `dark:` variants.
- Add missing helm components with `npx nx g @spartan-ng/cli:ui <name> --no-interactive`, never by hand.
- No new UI libraries, design system, fonts or palette unless explicitly requested.

## Allowed Impeccable commands

`critique`, `audit`, `polish`, `adapt`, `harden`, `typeset`, `clarify`, `layout`. Avoid `bolder`, `overdrive`, `delight` and redesign/rebrand flows unless asked.
