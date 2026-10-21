/** Rettangolo di una barra (coordinate dell'area righe). */
export interface Box {
  x: number;
  y: number;
  w: number;
  h: number;
}
export interface Point {
  x: number;
  y: number;
}
export interface Route {
  points: Point[];
  /** Attributo `d` del path SVG. */
  d: string;
  /** `true` se il successore inizia prima della fine del predecessore (percorso aggirato). */
  backwards: boolean;
}

/** Sporgenza orizzontale prima/dopo il primo tratto verticale. */
export const ELBOW = 8;
/** Lunghezza dello spezzone per un predecessore fuori intervallo. */
export const STUB = 18;

const toPath = (pts: Point[]): string => pts.map((p, i) => `${i ? 'L' : 'M'}${round(p.x)} ${round(p.y)}`).join(' ');
const round = (n: number): number => Math.round(n * 10) / 10;

/**
 * Connettore ortogonale dalla fine (centro destro) del predecessore all'inizio (centro sinistro) del successore.
 * Normale: destra di 8 px, verticale fino alla linea centrale del successore, orizzontale fino alla sua sinistra.
 * Se non c'è spazio (successore a meno di 16 px dalla fine, o prima), il tratto verticale passa nell'interstizio sopra/sotto la barra
 * predecessore e arriva da sinistra al successore.
 */
export function routeConnector(pred: Box, succ: Box): Route {
  const px = pred.x + pred.w;
  const py = pred.y + pred.h / 2;
  const sx = succ.x;
  const sy = succ.y + succ.h / 2;
  const backwards = sx < px;
  let points: Point[];
  if (sx - px >= ELBOW * 2) {
    const x1 = px + ELBOW;
    points = py === sy ? [{ x: px, y: py }, { x: sx, y: sy }] : [{ x: px, y: py }, { x: x1, y: py }, { x: x1, y: sy }, { x: sx, y: sy }];
  } else {
    const ym = sy < py ? pred.y - 2 : pred.y + pred.h + 2;
    points = [{ x: px, y: py }, { x: px + ELBOW, y: py }, { x: px + ELBOW, y: ym }, { x: sx - ELBOW, y: ym }, { x: sx - ELBOW, y: sy }, { x: sx, y: sy }];
  }
  return { points, d: toPath(points), backwards };
}

/** Spezzone con freccia sul bordo sinistro del successore, per un predecessore fuori intervallo. */
export function stubRoute(succ: Box): Route {
  const sy = succ.y + succ.h / 2;
  const points = [{ x: succ.x - STUB, y: sy }, { x: succ.x, y: sy }];
  return { points, d: toPath(points), backwards: false };
}

/** Il predecessore non è disegnabile: assente, o interamente prima dell'inizio della finestra. */
export const isOffRange = (pred: Box | undefined): boolean => !pred || pred.x + pred.w <= 0;
