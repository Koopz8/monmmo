#!/usr/bin/env node
/**
 * Route a push into the topic channel it belongs to — but only when the test
 * count moved.
 *
 *   node route.js --dry-run
 *   TEST_COUNT=1211 BEFORE=<sha> AFTER=<sha> node route.js
 *
 * The rule: a refactor is not news. A push that changes how many tests pass is
 * something was either proven or broken, and that is worth saying out loud in
 * the channel where the people who care about it are reading.
 *
 * If the count is unchanged, or unknown, this posts nothing and exits 0.
 */
'use strict';

const { execSync } = require('child_process');
const lib = require('./lib.js');
const { c } = lib;

lib.loadEnv();
const DRY = process.argv.includes('--dry-run');

/**
 * Path prefix -> channel. First match wins, so order matters: put the specific
 * prefixes above the general ones.
 */
const ROUTES = [
  { prefix: 'src/Core/Battle/', channel: 'battle-engine', label: 'Core/Battle' },
  { prefix: 'src/Core/World/', channel: 'engine-and-netcode', label: 'Core/World' },
  { prefix: 'src/Server/', channel: 'engine-and-netcode', label: 'Server' },
  { prefix: 'src/Client/', channel: 'engine-and-netcode', label: 'Client' },
  { prefix: 'src/RomExtract/', channel: 'data-and-extraction', label: 'RomExtract' },
  { prefix: 'src/Core/', channel: 'engine-and-netcode', label: 'Core' },
];

const git = (cmd, fallback = '') => {
  try {
    return execSync(`git ${cmd}`, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
  } catch (_) { return fallback; }
};

/** Group the push's changed files into subsystems, and those into channels. */
function classify(files) {
  const byChannel = new Map();
  for (const f of files) {
    const hit = ROUTES.find((r) => f.startsWith(r.prefix));
    if (!hit) continue;
    if (!byChannel.has(hit.channel)) byChannel.set(hit.channel, { labels: new Set(), files: 0 });
    const entry = byChannel.get(hit.channel);
    entry.labels.add(hit.label);
    entry.files++;
  }
  return byChannel;
}

function message({ labels, files }, { count, prev, commits, compareUrl }) {
  const delta = count - prev;
  const L = [];
  L.push(`**${[...labels].join(', ')}** — ${files} file${files === 1 ? '' : 's'} changed`);
  L.push('');
  if (delta > 0) {
    L.push(`Tests **${prev} → ${count}** (+${delta}). Something new is proven.`);
  } else {
    L.push(`Tests **${prev} → ${count}** (${delta}).`);
    L.push('');
    L.push('> The count went **down**. Either tests were deleted on purpose, or something regressed. Worth a sentence either way.');
  }
  L.push('');
  L.push('*Repo-wide count — if this push touched more than one subsystem, the delta is not necessarily from this one.*');
  if (commits.length) {
    L.push('');
    L.push('```');
    for (const line of commits.slice(0, 6)) L.push(line);
    if (commits.length > 6) L.push(`… and ${commits.length - 6} more`);
    L.push('```');
  }
  if (compareUrl) { L.push(''); L.push(`<${compareUrl}>`); }
  return L.join('\n');
}

async function main() {
  const count = process.env.TEST_COUNT ? Number(process.env.TEST_COUNT) : null;
  const before = process.env.BEFORE || '';
  const after = process.env.AFTER || 'HEAD';

  const state = lib.readState();
  const prev = state.routing?.testCount ?? null;

  if (count == null || Number.isNaN(count)) {
    console.log(c.warn('No test count available — the test run probably failed. Posting nothing.'));
    process.exit(0);
  }

  // First run: record the baseline rather than announcing a delta from nothing.
  if (prev == null) {
    if (!DRY) { state.routing = { testCount: count, at: new Date().toISOString() }; lib.writeState(state); }
    console.log(c.skip(`Baseline recorded at ${count} tests. Nothing posted.`));
    process.exit(0);
  }

  if (count === prev) {
    console.log(c.skip(`Test count unchanged at ${count}. Nothing posted — a refactor is not news.`));
    process.exit(0);
  }

  const range = before && !/^0+$/.test(before) ? `${before}..${after}` : `${after}~1..${after}`;
  const files = git(`diff --name-only ${range}`, '').split('\n').filter(Boolean);
  // %x20 rather than a literal space: an unquoted space here would make the
  // shell treat "%s" as a separate argument and git would silently return
  // nothing, leaving every routed message without its commit list.
  const commits = git(`log --pretty=format:%h%x20%x20%s ${range}`, '').split('\n').filter(Boolean);
  const compareUrl = process.env.COMPARE_URL || '';

  const routed = classify(files);
  if (!routed.size) {
    console.log(c.skip(`Test count moved ${prev} → ${count}, but no routed path was touched. Nothing posted.`));
    if (!DRY) { state.routing = { testCount: count, at: new Date().toISOString() }; lib.writeState(state); }
    process.exit(0);
  }

  console.log(c.head(`\nTests ${prev} → ${count}, ${routed.size} channel(s)${DRY ? '  [DRY RUN]' : ''}\n`));

  if (DRY) {
    for (const [channel, entry] of routed) {
      console.log(c.head(`→ #${channel}`));
      console.log(message(entry, { count, prev, commits, compareUrl }) + '\n');
    }
    process.exit(0);
  }

  const { client, guild } = await lib.connect();
  for (const [channel, entry] of routed) {
    try {
      const target = lib.resolveChannel(guild, channel);
      await lib.send(target, message(entry, { count, prev, commits, compareUrl }), {});
      console.log(`  ${c.ok('posted ')}  #${target.name}`);
    } catch (e) {
      console.log(`  ${c.warn('skipped')}  ${channel} — ${e.message.slice(0, 90)}`);
    }
  }

  state.routing = { testCount: count, at: new Date().toISOString() };
  lib.writeState(state);

  await client.destroy();
  process.exit(0);
}

module.exports = { ROUTES, classify, message };
if (require.main !== module) return;

main().catch((e) => {
  console.error(c.err('\n' + (e?.message || e)) + '\n');
  process.exit(1);
});
