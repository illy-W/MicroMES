export interface NavItem {
  id: string;
  displayName: string;
  iconName: string;
  route: string;
}

/** Sezione del menu: senza `label` le voci stanno in alto, non raggruppate. */
export interface NavSection {
  id: string;
  label?: string;
  items: NavItem[];
}
