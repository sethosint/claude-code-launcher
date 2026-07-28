#!/usr/bin/env node
/**
 * patch-claude.js — Patches Claude Code's model picker to show all proxy models.
 *
 * Claude Code's /model picker is hardcoded to show Default, Opus, Opus 1M, Haiku
 * + only the LAST-used custom model. This script injects all supported proxy models
 * into the picker so users can switch freely without memorising model IDs.
 *
 * Run automatically on every proxy launch (idempotent — safe to run repeatedly).
 * If Claude Code is updated, the patch is removed and must re-apply (handled).
 */

const fs = require("fs");
const path = require("path");
const { execSync } = require("child_process");

const PATCH_MARKER = "/*_CPP_*/";

// ── Models to inject into the picker ────────────────────────────────────────
const EXTRA_MODELS = [
  // Anthropic
  { value: "claude-sonnet-4.6", label: "Claude Sonnet 4.6", description: "Anthropic newest versatile" },
  // OpenAI
  { value: "gpt-5.2", label: "GPT-5.2", description: "OpenAI flagship model" },
  { value: "gpt-5.1", label: "GPT-5.1", description: "OpenAI versatile model" },
  { value: "gpt-5-mini", label: "GPT-5 Mini", description: "OpenAI fast & free tier" },
  { value: "gpt-4o", label: "GPT-4o", description: "OpenAI multimodal" },
  { value: "gpt-4.1", label: "GPT-4.1", description: "OpenAI coding model" },
  // Google
  { value: "gemini-3.1-pro-preview", label: "Gemini 3.1 Pro", description: "Google latest" },
  { value: "gemini-2.5-pro", label: "Gemini 2.5 Pro", description: "Google reasoning" },
  { value: "gemini-3-pro-preview", label: "Gemini 3 Pro", description: "Google next-gen" },
  { value: "gemini-3-flash-preview", label: "Gemini 3 Flash", description: "Google fast" },
  // xAI
  { value: "grok-code-fast-1", label: "Grok Code", description: "xAI coding model" },
  // Microsoft
  { value: "oswe-vscode-prime", label: "Raptor Mini", description: "Microsoft code model" },
];

// ── Locate cli.js ───────────────────────────────────────────────────────────

function findCliJs() {
  const candidates = [];
  const isWin = process.platform === "win32";
  const home = process.env.HOME || process.env.USERPROFILE || "";

  if (isWin) {
    // Windows: npm global install
    if (process.env.APPDATA) {
      candidates.push(path.join(process.env.APPDATA, "npm", "node_modules", "@anthropic-ai", "claude-code", "cli.js"));
    }
  } else {
    // macOS / Linux common paths
    candidates.push(
      path.join("/usr", "local", "lib", "node_modules", "@anthropic-ai", "claude-code", "cli.js"),
      path.join(home, ".npm-global", "lib", "node_modules", "@anthropic-ai", "claude-code", "cli.js"),
      path.join(home, ".local", "lib", "node_modules", "@anthropic-ai", "claude-code", "cli.js"),
    );

    // NixOS paths
    candidates.push(
      path.join(home, ".nix-profile", "lib", "node_modules", "@anthropic-ai", "claude-code", "cli.js"),
      path.join(home, ".local", "state", "nix", "profile", "lib", "node_modules", "@anthropic-ai", "claude-code", "cli.js"),
    );

    // NixOS: resolve symlink from nix-profile bin/claude → actual cli.js
    try {
      const claudeBin = path.join(home, ".nix-profile", "bin", "claude");
      if (fs.existsSync(claudeBin)) {
        const resolved = fs.realpathSync(claudeBin);
        const pkgRoot = path.resolve(path.dirname(resolved), "..", "lib", "node_modules", "@anthropic-ai", "claude-code", "cli.js");
        if (fs.existsSync(pkgRoot)) candidates.push(pkgRoot);
      }
    } catch {}
  }

  // Resolve via `where` (Windows) or `which` (Unix) and follow symlinks
  try {
    const cmd = isWin ? "where claude" : "which claude";
    const whichClaude = execSync(cmd, { encoding: "utf8", timeout: 5000, stdio: ["pipe", "pipe", "ignore"] }).trim().split("\n")[0];
    if (whichClaude) {
      try {
        const resolved = fs.realpathSync(whichClaude);
        const pkgRoot = path.resolve(path.dirname(resolved), "..", "lib", "node_modules", "@anthropic-ai", "claude-code", "cli.js");
        if (fs.existsSync(pkgRoot)) candidates.push(pkgRoot);
      } catch {}
      if (isWin) {
        // Windows: claude binary might be in node_modules/.bin/
        const winPkg = path.resolve(path.dirname(whichClaude), "..", "node_modules", "@anthropic-ai", "claude-code", "cli.js");
        if (fs.existsSync(winPkg)) candidates.push(winPkg);
      }
    }
  } catch {}

  // npm root -g fallback (works on all platforms)
  try {
    const npmRoot = execSync("npm root -g", { encoding: "utf8", timeout: 5000, stdio: ["pipe", "pipe", "ignore"] }).trim();
    if (npmRoot) candidates.push(path.join(npmRoot, "@anthropic-ai", "claude-code", "cli.js"));
  } catch {}

  for (const c of candidates) {
    try { if (fs.existsSync(c)) return c; } catch {}
  }
  return null;
}

// ── Remove existing patch (restore from backup if available) ────────────────

function unpatch(cliPath) {
  const bakPath = cliPath + ".bak";
  if (fs.existsSync(bakPath)) {
    fs.copyFileSync(bakPath, cliPath);
    return true;
  }
  return false;
}

// ── NixOS / read-only support ────────────────────────────────────────────────

const PATCHED_DIR = path.join(process.env.HOME || ".", ".copilot-claude-proxy", "patched");

/**
 * Check if a file is writable. On NixOS, nix store files are read-only.
 */
function isWritable(filePath) {
  try {
    fs.accessSync(filePath, fs.constants.W_OK);
    return true;
  } catch {
    return false;
  }
}

/**
 * For NixOS/read-only installs: copy cli.js to a writable location, patch there,
 * and create a wrapper script in ~/.local/bin/claude that uses the patched copy.
 */
function patchReadOnly(cliPath, silent) {
  const log = silent ? () => {} : console.log;

  // Ensure patched directory exists
  if (!fs.existsSync(PATCHED_DIR)) fs.mkdirSync(PATCHED_DIR, { recursive: true });

  const patchedCliPath = path.join(PATCHED_DIR, "cli.js");
  const versionFile = path.join(PATCHED_DIR, "source-version.txt");

  // Track the original file's size+mtime to detect Claude Code updates
  let originalSig = "";
  try {
    const st = fs.statSync(cliPath);
    originalSig = `${st.size}:${st.mtimeMs}`;
  } catch {}

  // Check if already patched copy exists and is up to date
  if (fs.existsSync(patchedCliPath)) {
    let savedSig = "";
    try { savedSig = fs.readFileSync(versionFile, "utf8").trim(); } catch {}

    if (savedSig === originalSig) {
      const patchedContent = fs.readFileSync(patchedCliPath, "utf8");
      const expectedModels = JSON.stringify(EXTRA_MODELS);
      if (patchedContent.includes(PATCH_MARKER) && patchedContent.includes(expectedModels) && patchedContent.includes('_l.includes("oswe")')) {
        log("  [patch] Already patched (writable copy) ✓");
        return patchedCliPath;
      }
      if (patchedContent.includes(PATCH_MARKER)) {
        log("  [patch] Patch outdated — re-patching writable copy…");
      }
    } else {
      log("  [patch] Claude Code was updated — re-patching…");
    }
  }

  // Copy the original cli.js to writable location
  log(`  [patch] Read-only cli.js detected — copying to writable location…`);
  fs.copyFileSync(cliPath, patchedCliPath);
  try { fs.chmodSync(patchedCliPath, 0o755); } catch {}

  // Now patch the writable copy
  if (patchFile(patchedCliPath, silent)) {
    // Save version signature so we can detect Claude Code updates
    try { fs.writeFileSync(versionFile, originalSig, "utf8"); } catch {}
    // Create a wrapper script so `claude` uses the patched version
    createWrapperScript(cliPath, patchedCliPath, silent);
    return patchedCliPath;
  }

  return null;
}

/**
 * Create a wrapper script that runs the patched cli.js with the correct
 * Node.js and module paths. Creates bash script on Unix, .cmd on Windows.
 */
function createWrapperScript(originalCliPath, patchedCliPath, silent) {
  const log = silent ? () => {} : console.log;
  const home = process.env.HOME || process.env.USERPROFILE || ".";
  const isWin = process.platform === "win32";

  // Determine wrapper directory
  let wrapperDir;
  if (isWin) {
    // Windows: put next to the proxy or in APPDATA
    wrapperDir = path.join(process.env.APPDATA || home, "copilot-claude-proxy", "bin");
  } else {
    wrapperDir = path.join(home, ".local", "bin");
  }

  if (!fs.existsSync(wrapperDir)) fs.mkdirSync(wrapperDir, { recursive: true });

  // Find the node binary
  let nodeBin = "node";
  try {
    const cmd = isWin ? "where node" : "which node";
    nodeBin = execSync(cmd, { encoding: "utf8", timeout: 5000, stdio: ["pipe", "pipe", "ignore"] }).trim().split("\n")[0];
  } catch {}

  // Find the original package's node_modules for require() resolution
  const originalPkgDir = path.dirname(originalCliPath);

  if (isWin) {
    // Windows .cmd wrapper
    const wrapperPath = path.join(wrapperDir, "claude-patched.cmd");
    const wrapperContent = `@echo off
REM Auto-generated by copilot-claude-proxy
set "NODE_PATH=${originalPkgDir}\\node_modules;${path.dirname(originalPkgDir)};%NODE_PATH%"
"${nodeBin}" "${patchedCliPath}" %*
`;
    try {
      fs.writeFileSync(wrapperPath, wrapperContent);
      log(`  [patch] Created wrapper: ${wrapperPath}`);
    } catch (e) {
      log(`  [patch] WARNING: could not create wrapper: ${e.message}`);
    }
  } else {
    // Unix bash wrapper
    const wrapperPath = path.join(wrapperDir, "claude-patched");
    const wrapperContent = `#!/usr/bin/env bash
# Auto-generated by copilot-claude-proxy — wraps Claude Code with patched model picker
# Original: ${originalCliPath}
# Patched:  ${patchedCliPath}
export NODE_PATH="${originalPkgDir}/node_modules:${path.dirname(originalPkgDir)}\${NODE_PATH:+:\$NODE_PATH}"
exec "${nodeBin}" "${patchedCliPath}" "$@"
`;
    try {
      fs.writeFileSync(wrapperPath, wrapperContent, { mode: 0o755 });
      log(`  [patch] Created wrapper: ${wrapperPath}`);
    } catch (e) {
      log(`  [patch] WARNING: could not create wrapper: ${e.message}`);
    }
  }
}

// ── Apply patch to a specific file ──────────────────────────────────────────

function patchFile(cliPath, silent) {
  const log = silent ? () => {} : console.log;
  let content = fs.readFileSync(cliPath, "utf8");

  const modelsJson = JSON.stringify(EXTRA_MODELS);

  // Already patched — check if models AND context window are up to date
  if (content.includes(PATCH_MARKER)) {
    if (content.includes(modelsJson) && content.includes('_l.includes("oswe")')) {
      log("  [patch] Already patched ✓");
      return true;
    }
    // Something changed — restore from backup and re-patch
    log("  [patch] Patch outdated — re-patching…");
    unpatch(cliPath);
    content = fs.readFileSync(cliPath, "utf8");
  }

  // If previously patched (old marker), restore clean version first
  if (content.includes("/*_CPP_*/") || content.includes("/* COPILOT_PROXY_PATCHED */")) {
    unpatch(cliPath);
    content = fs.readFileSync(cliPath, "utf8");
  }

  // ── Find the model-list function ──────────────────────────────────────────
  // Strategy: find the function that builds the model picker list.
  // We look for the pattern where the custom model is pushed and the list
  // is returned through a filter function. We inject our models just before
  // the filter call so they appear in the picker.
  //
  // New pattern (2025+):
  //   return q.push({value:K,label:K,description:"Custom model"}),hF8(q)
  // Old pattern:
  //   description:"Custom model"});return q}

  // Try new pattern first: comma-operator style with filter function
  // e.g. description:"Custom model"}),FILTER(ARRAY)}
  const newTailRe = /description:"Custom model"\}\),(\w+)\((\w+)\)\}/;
  const newTailMatch = content.match(newTailRe);

  if (newTailMatch) {
    const filterFunc = newTailMatch[1];
    const arrayVar = newTailMatch[2];
    const tailIndex = content.indexOf(newTailMatch[0]);

    // Backup original (only for writable files)
    const bakPath = cliPath + ".bak";
    try { fs.copyFileSync(cliPath, bakPath); } catch {}

    // Inject our models right before the filter call
    // Original: ...description:"Custom model"}),hF8(q)}
    // Patched:  ...description:"Custom model"}),/*_CPP_*/[...].forEach(function(_m){q.push(_m)}),hF8(q)}
    const originalTail = `${newTailMatch[0]}`;
    const patchedTail = `description:"Custom model"}),${PATCH_MARKER}${modelsJson}.forEach(function(_m){${arrayVar}.push(_m)}),${filterFunc}(${arrayVar})}`;
    content = content.substring(0, tailIndex) + patchedTail + content.substring(tailIndex + originalTail.length);
  } else {
    // Try old pattern: description:"Custom model"});return VAR}
    const oldTailRe = /description:"Custom model"\}\);return (\w+)\}/;
    const oldTailMatch = content.match(oldTailRe);
    if (!oldTailMatch) {
      log("  [patch] WARNING: model-list function pattern not found (Claude Code may have updated)");
      return false;
    }
    const retVar = oldTailMatch[1];
    const tailIndex = content.indexOf(oldTailMatch[0]);

    const searchRegion = content.substring(Math.max(0, tailIndex - 3000), tailIndex);
    const letRe = new RegExp("let " + retVar + "=(\\w+)\\(A\\),");
    const letMatch = searchRegion.match(letRe);
    if (!letMatch) {
      log("  [patch] WARNING: variable init pattern not found");
      return false;
    }

    const initFunc = letMatch[1];
    const letFull = `let ${retVar}=${initFunc}(A),`;
    const letIndex = content.lastIndexOf(letFull, tailIndex);
    if (letIndex === -1) {
      log("  [patch] WARNING: could not locate let declaration");
      return false;
    }

    const semiIndex = content.indexOf(";", letIndex);
    if (semiIndex === -1 || semiIndex > tailIndex) {
      log("  [patch] WARNING: could not find semicolon after let");
      return false;
    }

    // Backup original (only for writable files)
    const bakPath = cliPath + ".bak";
    try { fs.copyFileSync(cliPath, bakPath); } catch {}

    const injection = `${PATCH_MARKER}${modelsJson}.forEach(function(_m){${retVar}.push(_m)});`;
    content = content.substring(0, semiIndex + 1) + injection + content.substring(semiIndex + 1);
  }

  // ── Patch context window per model ──────────────────────────────────────
  const emOld = 'function eM(A,q){if(sb(A)||q?.includes(WV6)&&eSq(A))return 1e6;return oSq}';
  const emNew = 'function eM(A,q){if(sb(A)||q?.includes(WV6)&&eSq(A))return 1e6;var _l=A.toLowerCase();if(_l.includes("gpt-4o"))return 64000;if(_l.includes("oswe")||_l.includes("raptor"))return 200000;return 128000}';
  if (content.includes(emOld)) {
    content = content.replace(emOld, emNew);
    log("  [patch] Patched context window limits per model ✓");
  } else if (!content.includes('_l.includes("oswe")')) {
    log("  [patch] WARNING: context window function not found — autocompact may trigger late");
  }

  try {
    fs.writeFileSync(cliPath, content, "utf8");
    log(`  [patch] Patched model picker — added ${EXTRA_MODELS.length} custom models ✓`);
    return true;
  } catch (e) {
    log(`  [patch] WARNING: could not write: ${e.message}`);
    return false;
  }
}

// ── Apply patch ─────────────────────────────────────────────────────────────

function patch(silent) {
  const log = silent ? () => {} : console.log;
  const cliPath = findCliJs();
  if (!cliPath) {
    log("  [patch] Claude Code cli.js not found — skipping");
    return false;
  }

  // Check if cli.js is writable (NixOS nix store is read-only)
  if (!isWritable(cliPath)) {
    log(`  [patch] cli.js is read-only: ${cliPath}`);
    const patchedPath = patchReadOnly(cliPath, silent);
    return !!patchedPath;
  }

  return patchFile(cliPath, silent);
}

// ── CLI entry ───────────────────────────────────────────────────────────────
if (require.main === module) {
  const ok = patch(false);
  process.exit(ok ? 0 : 1);
}

module.exports = { patch, patchFile, patchReadOnly, findCliJs, EXTRA_MODELS, PATCH_MARKER };
