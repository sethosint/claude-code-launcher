const fs = require("fs");
const path = require("path");
const readline = require("readline");

const CONFIG_DIR = path.join(process.env.HOME || process.env.USERPROFILE || ".", ".completions-proxy");
const CONFIG_FILE = path.join(CONFIG_DIR, "config.json");

function loadConfig() {
  try {
    if (!fs.existsSync(CONFIG_FILE)) return null;
    const data = JSON.parse(fs.readFileSync(CONFIG_FILE, "utf8"));
    if (data.apiKey) return data;
  } catch {}
  return null;
}

function saveConfig(config) {
  if (!fs.existsSync(CONFIG_DIR)) fs.mkdirSync(CONFIG_DIR, { recursive: true });
  fs.writeFileSync(CONFIG_FILE, JSON.stringify(config, null, 2), "utf8");
}

async function promptApiKey(saasUrl) {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  return new Promise((resolve) => {
    console.log(`\n  Get your free API key at: \x1b[36m${saasUrl}/dashboard\x1b[0m\n`);
    rl.question("  Enter your API key: ", (answer) => {
      rl.close();
      const key = answer.trim();
      if (!key) {
        console.error("  \x1b[31mNo key provided. Exiting.\x1b[0m");
        process.exit(1);
      }
      if (!key.startsWith("sk-cp_")) {
        console.error("  \x1b[31mInvalid key format. Keys start with sk-cp_\x1b[0m");
        process.exit(1);
      }
      resolve(key);
    });
  });
}

module.exports = { loadConfig, saveConfig, promptApiKey, CONFIG_DIR, CONFIG_FILE };
