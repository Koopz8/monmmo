#!/usr/bin/env node
/**
 * The weekly pulse: commits, churn, and the test count with its delta.
 *
 *   node weekly.js --dry-run
 *   node weekly.js                       (reads git in the current repo)
 *   TEST_COUNT=1214 node weekly.js       (workflow passes the real number in)
 *
 * Posts to #devlog. Never pings — a heartbeat that pings is a heartbeat people
 * mute. Runs from a git checkout; in CI that is the repo the workflow cloned.
 *
 * If the week was quiet it says so, plainly. A log that only appears in good
 * weeks is not a log.
 */
'use strict';

const { execSync } = require('child_process');
const lib = require('./lib.js');
const { c } = lib;

lib.loadEnv();

const DRY = process.argv.includes('--dry-run');
const DAYS = Number(process.env.WINDOW_DAYS || 7);

const git = (cmd, fallback = '') => {
  try {
    return execSync(`git ${cmd}`, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
  } catch (_) { return fallback; }
};

function stats() {
  const since = `--since="${DAYS} days ago"`;
  const log = git(`log ${since} --pretty=format:%h%x09%s`, '');
  const commits = log ? log.split('\n').filter(Boolean) : [];

  const authorsRaw = git(`log ${since} --pretty=format:%an`, '');
  const authors = new Set(authorsRaw ? authorsRaw.split('\n').filter(Boolean) : []);

  // Churn across the window, if there is a commit that far back to diff against.
  const base = git(`rev-list -1 --before="${DAYS} days ago" HEAD`, '');
  let files = 0, added = 0, removed = 0;
  if (base) {
    const numstat = git(`diff --numstat ${base} HEAD`, '');
    for (const line of numstat.split('\n').filter(Boolean)) {
      const [a, r] = line.split('\t');
      files++;
      added += Number(a) || 0;
      removed += Number(r) || 0;
    }
  }

  return { commits, authors: authors.size, files, added, removed };
}

function build(s, testCount, prevTests) {
  const date = new Date().toISOString().slice(0, 10);
  const L = [`## Week ending ${date}`, ''];

  if (!s.commits.length) {
    L.push('No commits this week. Nothing shipped, nothing broken.', '');
  } else {
    const bits = [`**${s.commits.length}** commit${s.commits.length === 1 ? '' : 's'}`];
    if (s.files) bits.push(`**${s.files}** file${s.files === 1 ? '' : 's'} touched`);
    if (s.added || s.removed) bits.push(`\`+${s.added} / -${s.removed}\``);
    L.push(bits.join(' · '), '');
  }

  if (testCount != null) {
    if (prevTests != null && testCount !== prevTests) {
      const d = testCount - prevTests;
      L.push(`**${testCount} tests** passing (${d > 0 ? '+' : ''}${d} since last week)`, '');
      if (d < 0) L.push(`> The test count went **down**. That is either a deletion worth explaining or a regression worth catching.`, '');
    } else {
      L.push(`**${testCount} tests** passing${prevTests != null ? ' (no change)' : ''}`, '');
    }
  }

  if (s.commits.length) {
    L.push('```');
    for (const line of s.commits.slice(0, 10)) L.push(line.replace(/\t/, '  '));
    if (s.commits.length > 10) L.push(`… and ${s.commits.length - 10} more`);
    L.push('```');
  }

  return L.join('\n');
}

async function main() {
  const s = stats();
  const state = lib.readState();
  const prev = state.weekly?.testCount ?? null;
  const testCount = process.env.TEST_COUNT ? Number(process.env.TEST_COUNT) : null;

  const text = build(s, testCount, prev);

  if (DRY) {
    console.log(c.head('\n→ #devlog\n'));
    console.log(text + '\n');
    process.exit(0);
  }

  const { client, guild } = await lib.connect();
  const channel = lib.resolveChannel(guild, 'devlog');
  const sent = await lib.send(channel, text, {});

  state.weekly = { testCount: testCount ?? prev, at: new Date().toISOString(), messageId: sent[0].id };
  lib.writeState(state);

  console.log(c.ok('posted') + ` weekly pulse to #${channel.name}`);
  await client.destroy();
  process.exit(0);
}

module.exports = { build };
if (require.main !== module) return;

main().catch((e) => {
  console.error(c.err('\n' + (e?.message || e)) + '\n');
  process.exit(1);
});
