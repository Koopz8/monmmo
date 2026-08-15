#!/usr/bin/env node
/**
 * The daily recap for #devlog.
 *
 *   node daily.js --dry-run       build it, print it, send nothing
 *   node daily.js                 post it
 *   node daily.js --since 3.days  cover a different window
 *
 * Reads the day's commits and diffstat, asks Claude to group them into Added /
 * Fixed / Changed in plain prose, appends what is coming from NEXT.md, and
 * posts one message to #devlog. No ping — a daily ping is a muted channel.
 *
 * ONLY commit messages, file paths and line counts are sent to the API. No file
 * contents, no diffs, and nothing that has ever touched a cartridge.
 *
 * If ANTHROPIC_API_KEY is missing or the call fails, it falls back to sorting
 * commits by verb and says so in the output rather than posting nothing.
 */
'use strict';

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const lib = require('./lib.js');
const { c } = lib;

lib.loadEnv();

const argv = process.argv.slice(2);
const DRY = argv.includes('--dry-run');
const arg = (n) => { const i = argv.indexOf(`--${n}`); return i === -1 ? null : argv[i + 1]; };
const SINCE = arg('since') || '1.day';

const API = 'https://api.anthropic.com/v1';
const FALLBACK_MODEL = 'claude-sonnet-4-5';

const git = (cmd, fallback = '') => {
  try {
    return execSync(`git ${cmd}`, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
  } catch (_) { return fallback; }
};

// ── gather ──────────────────────────────────────────────────────────────────
function gather(since) {
  const range = `--since="${since} ago"`;
  const commits = (git(`log ${range} --pretty=format:%h%x09%s`, '') || '')
    .split('\n').filter(Boolean)
    .map((l) => { const [sha, ...rest] = l.split('\t'); return { sha, subject: rest.join('\t') }; });

  const bodies = (git(`log ${range} --pretty=format:%h%x1e%s%x1e%b%x1f`, '') || '')
    .split('\x1f').map((s) => s.trim()).filter(Boolean)
    .map((chunk) => { const [sha, subject, body] = chunk.split('\x1e'); return { sha, subject, body: (body || '').trim() }; });

  const base = git(`rev-list -1 --before="${since} ago" HEAD`, '');
  let files = [], added = 0, removed = 0;
  if (base) {
    for (const line of git(`diff --numstat ${base} HEAD`, '').split('\n').filter(Boolean)) {
      const [a, r, f] = line.split('\t');
      files.push(f);
      added += Number(a) || 0;
      removed += Number(r) || 0;
    }
  }
  return { commits, bodies, files, added, removed, base };
}

/**
 * Everything after a standalone `---` in the first 20 lines is the postable
 * part. That lets NEXT.md carry notes-to-self at the top without them ending up
 * in the channel. No separator means the whole file is posted.
 */
function stripNextHeader(raw) {
  const lines = String(raw).replace(/\r\n/g, '\n').split('\n');
  const cut = lines.findIndex((l, i) => i < 20 && l.trim() === '---');
  return (cut === -1 ? lines : lines.slice(cut + 1)).join('\n').trim();
}

/** Read the "what's next" file from wherever the user actually put it. */
function readNext() {
  const candidates = ['NEXT.md', '../NEXT.md', 'discord/NEXT.md', '../discord/NEXT.md', 'docs/NEXT.md', '../docs/NEXT.md'];
  for (const rel of candidates) {
    const p = path.resolve(process.cwd(), rel);
    try {
      if (fs.existsSync(p)) {
        const text = stripNextHeader(fs.readFileSync(p, 'utf8'));
        if (text) return { path: rel, text };
      }
    } catch (_) { /* keep looking */ }
  }
  return null;
}

// ── the fallback summariser ─────────────────────────────────────────────────
const ADD_RE = /^(add|added|adds|implement|implemented|introduce|support|create|new|build)\b/i;
const FIX_RE = /^(fix|fixed|fixes|correct|corrects|repair|resolve|stop|prevent|handle)\b/i;

function categorise(commits) {
  const out = { Added: [], Fixed: [], Changed: [] };
  for (const cm of commits) {
    const s = cm.subject;
    if (ADD_RE.test(s)) out.Added.push(s);
    else if (FIX_RE.test(s)) out.Fixed.push(s);
    else out.Changed.push(s);
  }
  return out;
}

// ── the model ───────────────────────────────────────────────────────────────
/**
 * Ask the API which models exist rather than hardcoding an id that will be
 * renamed out from under this script. Newest first, prefer a Sonnet.
 */
async function pickModel(key) {
  if (process.env.ANTHROPIC_MODEL) return process.env.ANTHROPIC_MODEL;
  try {
    const res = await fetch(`${API}/models?limit=40`, {
      headers: { 'x-api-key': key, 'anthropic-version': '2023-06-01' },
    });
    if (!res.ok) return FALLBACK_MODEL;
    const json = await res.json();
    const ids = (json.data || []).map((m) => m.id);
    return ids.find((id) => /sonnet/i.test(id)) || ids[0] || FALLBACK_MODEL;
  } catch (_) { return FALLBACK_MODEL; }
}

function buildPrompt({ bodies, files, added, removed, tests, prevTests }) {
  const list = bodies.map((b) => `- ${b.sha} ${b.subject}${b.body ? `\n    ${b.body.split('\n').slice(0, 4).join('\n    ')}` : ''}`).join('\n');
  const paths = [...new Set(files)].slice(0, 60).join('\n');
  const testLine = tests != null
    ? `Test count: ${prevTests != null ? `${prevTests} -> ${tests}` : tests}`
    : 'Test count: unavailable';

  return `You are writing the daily developer log for an open-source game engine project.

Here is everything that happened today.

COMMITS
${list || '(none)'}

FILES TOUCHED (${files.length} total, +${added}/-${removed} lines)
${paths || '(none)'}

${testLine}

Write a recap with exactly these sections, omitting any section that has nothing real in it:

**Added** — new capability that did not exist before
**Fixed** — things that were broken and now are not
**Changed** — refactors, renames, behaviour changes, removals

Rules:
- Bullet points. One line each. Past tense.
- Write for someone who follows the project but did not read the diff.
- Say what changed and why it matters, not which file moved.
- Do NOT invent significance. If the day was small, say so in one line and stop.
- Never claim something was fixed unless a commit says it was.
- If the test count fell, say so plainly and do not explain it away.
- No preamble, no sign-off, no "Overall". Sections only.
- Under 200 words total. Plain markdown. No headings above bold labels.`;
}

async function summarise(key, model, ctx) {
  const res = await fetch(`${API}/messages`, {
    method: 'POST',
    headers: {
      'x-api-key': key,
      'anthropic-version': '2023-06-01',
      'content-type': 'application/json',
    },
    body: JSON.stringify({
      model,
      max_tokens: 900,
      messages: [{ role: 'user', content: buildPrompt(ctx) }],
    }),
  });
  if (!res.ok) throw new Error(`API ${res.status}: ${(await res.text()).slice(0, 200)}`);
  const json = await res.json();
  const text = (json.content || []).filter((b) => b.type === 'text').map((b) => b.text).join('\n').trim();
  if (!text) throw new Error('API returned no text');
  return text;
}

// ── compose ─────────────────────────────────────────────────────────────────
function compose({ date, summary, fellBack, data, tests, prevTests, next, compareUrl }) {
  const L = [`## Daily recap — ${date}`, ''];

  if (!data.commits.length) {
    L.push('No commits today.');
    if (next) { L.push('', '**Next**', '', next.trim()); }
    return L.join('\n');
  }

  L.push(summary.trim(), '');

  const bits = [`**${data.commits.length}** commit${data.commits.length === 1 ? '' : 's'}`];
  if (data.files.length) bits.push(`**${data.files.length}** files`);
  if (data.added || data.removed) bits.push(`\`+${data.added} / -${data.removed}\``);
  if (tests != null) {
    const d = prevTests != null ? tests - prevTests : null;
    bits.push(d != null && d !== 0 ? `**${tests}** tests (${d > 0 ? '+' : ''}${d})` : `**${tests}** tests`);
  }
  L.push(bits.join(' · '));

  if (tests != null && prevTests != null && tests < prevTests) {
    L.push('', '> The test count went **down** today. Deletion or regression — worth a sentence.');
  }

  if (next) L.push('', '**Next**', '', next.trim());
  if (compareUrl) L.push('', `<${compareUrl}>`);
  if (fellBack) L.push('', '-# Summary generated without the model — commits sorted by verb.');

  return L.join('\n');
}

// ── main ────────────────────────────────────────────────────────────────────
async function main() {
  const date = (process.env.RECAP_DATE || new Date().toISOString().slice(0, 10));
  const data = gather(SINCE);

  const state = lib.readState();
  const prevTests = state.daily?.testCount ?? null;
  const tests = process.env.TEST_COUNT ? Number(process.env.TEST_COUNT) : null;

  const nextFile = readNext();
  let next = nextFile?.text || null;
  if (next && next.length > 700) next = next.slice(0, 700).replace(/\n[^\n]*$/, '') + '\n…';

  let summary = '';
  let fellBack = false;

  if (data.commits.length) {
    const key = process.env.ANTHROPIC_API_KEY;
    if (key) {
      try {
        const model = await pickModel(key);
        console.log(c.skip(`  model: ${model}`));
        summary = await summarise(key, model, { ...data, tests, prevTests });
      } catch (e) {
        console.log(c.warn(`  model call failed (${String(e.message).slice(0, 120)}) — falling back`));
        fellBack = true;
      }
    } else {
      console.log(c.warn('  ANTHROPIC_API_KEY not set — falling back to verb sorting'));
      fellBack = true;
    }

    if (fellBack) {
      const cats = categorise(data.commits);
      summary = Object.entries(cats)
        .filter(([, v]) => v.length)
        .map(([k, v]) => `**${k}**\n` + v.map((s) => `- ${s}`).join('\n'))
        .join('\n\n');
    }
  }

  const text = compose({
    date, summary, fellBack, data, tests, prevTests, next,
    compareUrl: process.env.COMPARE_URL || '',
  });

  if (DRY) {
    console.log(c.head('\n→ #devlog\n'));
    console.log(text + '\n');
    if (nextFile) console.log(c.skip(`(Next section read from ${nextFile.path})`));
    else console.log(c.warn('(No NEXT.md found — the Next section was omitted)'));
    process.exit(0);
  }

  const { client, guild } = await lib.connect();
  const channel = lib.resolveChannel(guild, 'devlog');
  const sent = await lib.send(channel, text, {});

  state.daily = { testCount: tests ?? prevTests, at: new Date().toISOString(), messageId: sent[0].id };
  lib.writeState(state);

  console.log(c.ok('posted') + ` daily recap to #${channel.name}`);
  await client.destroy();
  process.exit(0);
}

module.exports = { categorise, compose, buildPrompt, readNext, stripNextHeader };
if (require.main !== module) return;

main().catch((e) => {
  console.error(c.err('\n' + (e?.message || e)) + '\n');
  process.exit(1);
});
