import { createClient } from '@supabase/supabase-js';
import { readFileSync, existsSync } from 'fs';
import { resolve, join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { writeTask } from './agentCall.js';

const __dirname = dirname(fileURLToPath(import.meta.url));

function findRalphShDir(startDir) {
  let dir = startDir;
  while (true) {
    if (existsSync(join(dir, 'ralph.sh'))) return dir;
    const parent = dirname(dir);
    if (parent === dir) return null;
    dir = parent;
  }
}

function findConfigPath(startDir) {
  let dir = startDir;
  while (true) {
    const candidate = join(dir, '.eventmodelers', 'config.json');
    if (existsSync(candidate)) return candidate;
    const parent = dirname(dir);
    if (parent === dir) throw new Error('No .eventmodelers/config.json found in current directory or any parent directory');
    dir = parent;
  }
}

function loadLocalConfig() {
  const configPath = findConfigPath(process.cwd());
  const raw = readFileSync(configPath, 'utf-8');
  const cfg = JSON.parse(raw);

  for (const key of ['token', 'organizationId', 'baseUrl']) {
    if (!cfg[key]) throw new Error(`Missing config field: ${key}`);
  }

  if (process.env.BASE_URL) cfg.baseUrl = process.env.BASE_URL;

  return cfg;
}

async function fetchPlatformConfig(local) {
  const res = await fetch(`${local.baseUrl}/api/config`, {
    headers: { 'x-token': local.token },
  });
  if (!res.ok) throw new Error(`Failed to fetch platform config: ${res.status} / ${res.statusText} / ${await res.text()}`);
  const remote = await res.json();
  return { ...local, ...remote };
}

async function getRealtimeToken(cfg) {
  const res = await fetch(`${cfg.baseUrl}/api/org/${cfg.organizationId}/prompts/realtime-token`, {
    headers: { 'x-token': cfg.token },
  });
  if (!res.ok) throw new Error(`Failed to get realtime token: ${res.status} / ${res.statusText} / ${await res.text()}`);
  const { token } = await res.json();
  return token;
}

async function fetchNextPrompt(cfg, jwtToken) {
  const res = await fetch(`${cfg.baseUrl}/api/org/${cfg.organizationId}/prompts/next`, {
    headers: { 'x-token': cfg.token, 'Authorization': `Bearer ${jwtToken}` },
  });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Failed to fetch prompt: ${res.status} / ${res.statusText} / ${await res.text()}`);
  return res.json();
}

async function drainQueue(cfg, jwtToken, claudeCwd) {
  const prompts = [];
  let prompt;

  while ((prompt = await fetchNextPrompt(cfg, jwtToken)) !== null) {
    console.log(`[agent] Queuing prompt "${prompt.prompt}" (board=${prompt.board_id}, priority=${prompt.priority})`);
    prompts.push(prompt);
  }

  if (prompts.length > 0) {
    await writeTask(prompts, claudeCwd);
  }
}

async function start() {
  const claudeCwd = process.argv[2] ?? findRalphShDir(process.cwd()) ?? resolve(process.cwd(), '.');

  const local = loadLocalConfig();
  const cfg = await fetchPlatformConfig(local);

  console.log(`[agent] Starting — org=${cfg.organizationId}, base=${cfg.baseUrl}, cwd=${claudeCwd}`);

  let realtimeToken = await getRealtimeToken(cfg);

  const supabase = createClient(cfg.supabaseUrl, cfg.supabaseAnonKey, {
    realtime: { params: { apikey: cfg.supabaseAnonKey } },
  });

  await supabase.realtime.setAuth(realtimeToken);

  supabase
    .channel(`org:${cfg.organizationId}`, { config: { private: true } })
    .on('broadcast', { event: 'prompt:created' }, async () => {
      console.log('[agent] New prompt received');
      await drainQueue(cfg, realtimeToken, claudeCwd).catch((err) => console.error('[agent] Queue drain error:', err));
    })
    .subscribe(async (status) => {
      await drainQueue(cfg, realtimeToken, claudeCwd).catch((err) => console.error('[agent] Queue drain error:', err));
      console.log(`[agent] Realtime channel status: ${status}`);
    });

  setInterval(async () => {
    try {
      realtimeToken = await getRealtimeToken(cfg);
      supabase.realtime.setAuth(realtimeToken);
      console.log('[agent] Realtime token refreshed');
    } catch (err) {
      console.error('[agent] Token refresh failed:', err);
    }
  }, 50 * 60 * 1000);

  const ping = async () => {
    try {
      const res = await fetch(`${cfg.baseUrl}/api/agent-alive`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${realtimeToken}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: cfg.token }),
      });
      if (!res.ok) console.error(`[agent] Ping failed: ${res.status}`);
    } catch (err) {
      console.error('[agent] Ping error:', err);
    }
  };

  await ping();
  setInterval(ping, 10 * 1000);
}

start().catch((err) => {
  console.error('[agent] Fatal:', err);
  process.exit(1);
});