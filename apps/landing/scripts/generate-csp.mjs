// Writes the nginx CSP snippet for the prerendered landing.
// Usage: node generate-csp.mjs <browser-dist-dir> <output-file>
//
// The prerendered HTML contains a few inline scripts (Angular hydration and event replay). The policy allows
// them by SHA-256 hash instead of 'unsafe-inline'. The policy is Report-Only: violations show in the browser
// console and nothing is blocked. Switch the header name to `Content-Security-Policy` to enforce it.
import { createHash } from 'node:crypto';
import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const [dir, out] = process.argv.slice(2);
if (!dir || !out) {
  console.error('Usage: node generate-csp.mjs <browser-dist-dir> <output-file>');
  process.exit(1);
}

const htmlFiles = (d) =>
  readdirSync(d).flatMap((name) => {
    const path = join(d, name);
    if (statSync(path).isDirectory()) return name === 'assets' ? [] : htmlFiles(path);
    return name.endsWith('.html') ? [path] : [];
  });

const hashes = new Set();
for (const file of htmlFiles(dir)) {
  const html = readFileSync(file, 'utf8');
  for (const match of html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/g)) {
    const [, attrs, body] = match;
    if (/\bsrc\s*=/.test(attrs) || !body.trim()) continue;
    const type = /\btype\s*=\s*["']([^"']+)["']/.exec(attrs)?.[1];
    if (type && type !== 'text/javascript' && type !== 'module') continue; // data blocks are not executed
    hashes.add(`'sha256-${createHash('sha256').update(body, 'utf8').digest('base64')}'`);
  }
}

const policy = [
  "default-src 'self'",
  `script-src 'self' ${[...hashes].join(' ')}`.trim(),
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data:",
  "font-src 'self'",
  "connect-src 'self'",
  "object-src 'none'",
  "base-uri 'self'",
  "frame-ancestors 'none'"
].join('; ');

writeFileSync(out, `add_header Content-Security-Policy-Report-Only "${policy}" always;\n`);
console.log(`CSP written to ${out} (${hashes.size} inline script hashes)`);
