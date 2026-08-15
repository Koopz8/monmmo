/**
 * Shared plumbing for post.js, sync.js and weekly.js.
 * No side effects on require — safe to import from anywhere.
 */
'use strict';

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

// ── .env (no dependency) ────────────────────────────────────────────────────
function loadEnv(dir = __dirname) {
  try {
    const p = path.join(dir, '.env');
    if (!fs.existsSync(p)) return;
    for (const line of fs.readFileSync(p, 'utf8').split('\n')) {
      const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$/);
      if (m && !process.env[m[1]]) process.env[m[1]] = m[2].replace(/^["']|["']$/g, '');
    }
  } catch (_) { /* optional */ }
}

// ── console ─────────────────────────────────────────────────────────────────
const c = {
  ok:   (s) => `\x1b[32m${s}\x1b[0m`,
  skip: (s) => `\x1b[90m${s}\x1b[0m`,
  warn: (s) => `\x1b[33m${s}\x1b[0m`,
  err:  (s) => `\x1b[31m${s}\x1b[0m`,
  head: (s) => `\x1b[1m\x1b[36m${s}\x1b[0m`,
};

const DISCORD_LIMIT = 2000;

/**
 * Split markdown into Discord-sized messages.
 *
 * Never splits inside a fenced code block: if a fence is open when the chunk
 * fills up, the chunk is closed with ``` and the next one reopens with the same
 * fence, so a long log paste survives intact instead of turning into garbage
 * halfway down.
 */
function chunk(text, limit = DISCORD_LIMIT) {
  const lines = String(text).replace(/\r\n/g, '\n').split('\n');
  const out = [];
  let buf = '';
  let fence = null; // the opening fence line, e.g. "```json"

  const flush = () => {
    if (!buf.trim()) { buf = ''; return; }
    out.push(fence ? buf.replace(/\n+$/, '') + '\n```' : buf.replace(/\n+$/, ''));
    buf = fence ? fence + '\n' : '';
  };

  for (const rawLine of lines) {
    // A single line longer than the limit has to be hard-split.
    let pieces = [rawLine];
    if (rawLine.length > limit - 10) {
      pieces = rawLine.match(new RegExp(`.{1,${limit - 10}}`, 'g')) || [rawLine];
    }

    for (const line of pieces) {
      if (buf.length + line.length + 1 > limit - (fence ? 4 : 0)) flush();
      buf += line + '\n';
      const f = line.match(/^\s*(```|~~~)/);
      if (f) fence = fence ? null : line.trim();
    }
  }
  if (buf.trim()) out.push(fence ? buf.replace(/\n+$/, '') + '\n```' : buf.replace(/\n+$/, ''));
  return out.length ? out : [''];
}

/**
 * Parse `--- key: value --- body` front matter. Deliberately tiny: string,
 * boolean and comma-list values only, no YAML dependency.
 */
function frontmatter(raw) {
  const text = String(raw).replace(/^﻿/, '').replace(/\r\n/g, '\n');
  const m = text.match(/^---\n([\s\S]*?)\n---\n?/);
  if (!m) return { meta: {}, body: text.trim() };
  const meta = {};
  for (const line of m[1].split('\n')) {
    const kv = line.match(/^\s*([A-Za-z_][\w-]*)\s*:\s*(.*)\s*$/);
    if (!kv) continue;
    let v = kv[2].trim().replace(/^["']|["']$/g, '');
    if (v === 'true') v = true;
    else if (v === 'false') v = false;
    else if (v.includes(',')) v = v.split(',').map((s) => s.trim()).filter(Boolean);
    meta[kv[1]] = v;
  }
  return { meta, body: text.slice(m[0].length).trim() };
}

const hash = (s) => crypto.createHash('sha1').update(String(s)).digest('hex').slice(0, 12);

// ── state (what has already been posted) ────────────────────────────────────
const STATE_PATH = path.join(__dirname, '.sync-state.json');
const readState = () => {
  try { return JSON.parse(fs.readFileSync(STATE_PATH, 'utf8')); } catch (_) { return {}; }
};
const writeState = (s) => {
  try { fs.writeFileSync(STATE_PATH, JSON.stringify(s, null, 2) + '\n'); } catch (_) {}
};

// ── discord ─────────────────────────────────────────────────────────────────
/**
 * Wait for the gateway. `Events.ClientReady` is 'ready' on older discord.js and
 * 'clientReady' from 14.19 on, so reading it from the library keeps this quiet
 * on both instead of registering a deprecated listener.
 */
async function ready(client) {
  const { Events } = require('discord.js');
  if (client.isReady()) return;
  await new Promise((r) => client.once(Events.ClientReady, r));
}

/**
 * Count pinned messages across the fetchPinned -> fetchPins rename. The new
 * method returns a different shape, so normalise rather than trusting `.size`
 * — reading it wrong would mean "no pins" and a duplicate post.
 */
async function pinnedCount(channel) {
  const mgr = channel.messages;
  if (typeof mgr.fetchPins === 'function') {
    try {
      const res = await mgr.fetchPins();
      if (res == null) return 0;
      if (typeof res.size === 'number') return res.size;           // Collection
      if (Array.isArray(res.items)) return res.items.length;       // { items, hasMore }
      if (Array.isArray(res)) return res.length;
      return 0;
    } catch (_) { /* fall through to the old method */ }
  }
  const old = await mgr.fetchPinned().catch(() => null);
  return old?.size ?? 0;
}

async function connect() {
  const { Client, GatewayIntentBits } = require('discord.js');
  const token = process.env.DISCORD_TOKEN;
  const guildId = process.env.GUILD_ID;
  if (!token) throw new Error('DISCORD_TOKEN is not set.');
  if (!guildId) throw new Error('GUILD_ID is not set.');

  const client = new Client({ intents: [GatewayIntentBits.Guilds] });
  await client.login(token);
  await ready(client);

  const guild = await client.guilds.fetch(guildId).catch(() => null);
  if (!guild) throw new Error(`The bot is not in a server with id ${guildId}.`);
  await guild.channels.fetch();
  await guild.roles.fetch();
  return { client, guild };
}

/** Look a channel up by the key used in the TREE, falling back to its raw name. */
function resolveChannel(guild, key) {
  const { TREE } = require('./setup-server.js');
  const spec = TREE.flatMap((cat) => cat.channels).find((ch) => ch.key === key);
  const wanted = (spec ? spec.name : key).toLowerCase().replace(/\s+/g, '-');
  const found = guild.channels.cache.find(
    (ch) => ch.name.toLowerCase() === wanted && ch.isTextBased?.()
  );
  if (!found) {
    const known = TREE.flatMap((cat) => cat.channels).filter((ch) => ch.type !== 'voice').map((ch) => ch.key);
    throw new Error(`No channel for "${key}". Known keys: ${known.join(', ')}`);
  }
  return found;
}

/** Resolve a ping role by its short name ("devlog" -> "devlog pings"). */
function resolvePing(guild, name) {
  if (!name) return null;
  const wanted = String(name).toLowerCase().replace(/\s*pings?$/, '');
  const role = guild.roles.cache.find((r) => r.name.toLowerCase() === `${wanted} pings`)
    || guild.roles.cache.find((r) => r.name.toLowerCase() === wanted);
  if (!role) throw new Error(`No role matching ping "${name}". Try: devlog, build, playtest.`);
  return role;
}

/**
 * Send one logical post: chunked, optionally pinned, optionally threaded,
 * optionally crossposted to servers following an announcement channel.
 */
async function send(channel, text, opts = {}) {
  const parts = chunk(text);
  const sent = [];
  for (const part of parts) {
    const msg = await channel.send({
      content: part,
      allowedMentions: { roles: opts.pingRoleId ? [opts.pingRoleId] : [], parse: [] },
    });
    sent.push(msg);
  }
  const first = sent[0];
  if (opts.pin) await first.pin().catch(() => {});
  if (opts.crosspost && channel.type === 5 /* GuildAnnouncement */) {
    for (const m of sent) await m.crosspost().catch(() => {});
  }
  if (opts.thread) {
    await first.startThread({
      name: String(opts.thread).slice(0, 100),
      autoArchiveDuration: 10080,
    }).catch(() => {});
  }
  return sent;
}

module.exports = {
  loadEnv, c, chunk, frontmatter, hash,
  readState, writeState, STATE_PATH,
  connect, ready, pinnedCount, resolveChannel, resolvePing, send,
  DISCORD_LIMIT,
};
