import { readFileSync, writeFileSync, readdirSync } from "fs";
import { fileURLToPath } from "url";
import { dirname, join } from "path";

const __dir = dirname(fileURLToPath(import.meta.url));
const root = join(__dir, "..");

const html    = readFileSync(join(root, "index.html"),        "utf8");
const css     = readFileSync(join(root, "styles.css"),        "utf8");
const favicon = readFileSync(join(root, "favicon.svg"),       "utf8");
const coreJs  = readFileSync(join(root, "src", "core.js"),   "utf8");
const configJs= readFileSync(join(root, "src", "config.js"), "utf8");
const gameJs  = readFileSync(join(root, "src", "game.js"),   "utf8");

function stripExports(src) {
  return src.replace(/^export (const |function |class )/gm, "$1");
}

function stripImports(src) {
  return src.replace(/^import\s[^;]+;\n?/gm, "");
}

const bundledJs = [
  stripExports(coreJs),
  stripExports(configJs),
  stripImports(gameJs),
].join("\n\n");

const faviconDataUrl =
  "data:image/svg+xml;base64," + Buffer.from(favicon).toString("base64");

let out = html;

out = out.replace(
  '<link rel="icon" href="./favicon.svg" type="image/svg+xml" />',
  `<link rel="icon" href="${faviconDataUrl}" type="image/svg+xml" />`
);

out = out.replace(
  '<link rel="stylesheet" href="./styles.css" />',
  `<style>\n${css}</style>`
);

out = out.replace(
  '<script type="module" src="./src/game.js"></script>',
  `<script>\n${bundledJs}\n</script>`
);

// Auto-increment version from existing builds
const existing = readdirSync(root)
  .map((f) => f.match(/^emberhold-v(\d+)\.html$/))
  .filter(Boolean)
  .map((m) => parseInt(m[1], 10));
const nextVersion = existing.length ? Math.max(...existing) + 1 : 1;

const outPath = join(root, `emberhold-v${nextVersion}.html`);
writeFileSync(outPath, out, "utf8");
console.log(`Bundled → ${outPath}`);
