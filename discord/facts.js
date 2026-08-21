#!/usr/bin/env node
/**
 * The numbers in the pinned copy, read out of the repo instead of typed in.
 *
 *   node facts.js              print what it found, write nothing
 *   node facts.js --write      write .facts.json
 *   node facts.js --check      exit 1 if a fact is missing or two sources disagree
 *
 * WHY THIS EXISTS
 *
 * Prose in content.js is written by hand and should be. Numbers are not: the
 * test count, how far the walk gets, how much of the script reads — those move
 * every few days, and every time they moved somebody had to remember to edit
 * four channels. They did not always remember. #welcome carried a test count
 * that was eleven hundred short for weeks.
 *
 * So the figures live here, are read from the repo's own notes, and go into the
 * copy as {{PLACEHOLDERS}}.
 *
 * THE RULE THIS FILE IS BUILT AROUND
 *
 * A number nothing computes cannot come back wrong, which is worse than a number
 * that is stale. So:
 *
 *   - every fact names the file and the line it was read from;
 *   - a fact that cannot be found is an ERROR, never a silent blank or a
 *     remembered default;
 *   - the test count is read from TWO places and they have to agree.
 *
 * That last one is not paranoia. This project lost two sessions to a status
 * block that was copied forward thirty-nine times without anybody re-running a
 * line of it, and the wrong number in it had been corrected — in a document
 * nobody reads first.
 *
 * SOURCES
 *
 *   claude/next-session-prompt.md   "Where the reading stands" and the floor
 *                                   table, both of which the project re-runs
 *                                   and rewrites wholesale rather than patching
 *   claude/milestone-<n>-*.md       the newest one, for its closing test count
 */
'use strict';

const fs = require('fs');
const path = require('path');

const argv = process.argv.slice(2);
const has = (f) => argv.includes(`--${f}`);
const WRITE = has('write');
const CHECK = has('check');

const c = {
  head: (s) => `\x1b[1m\x1b[36m${s}\x1b[0m`,
  ok:   (s) => `\x1b[32m${s}\x1b[0m`,
  err:  (s) => `\x1b[31m${s}\x1b[0m`,
  warn: (s) => `\x1b[33m${s}\x1b[0m`,
  skip: (s) => `\x1b[90m${s}\x1b[0m`,
};

/**
 * FACTS_ROOT out of .env, without pulling in lib.js.
 *
 * lib.js requires discord.js, and this file has to run in a checkout that has
 * never had `npm install` — which is exactly where the scheduled job and the
 * repo copy live. So the four lines are here rather than shared.
 */
function envFactsRoot() {
  try {
    const env = fs.readFileSync(path.join(__dirname, '.env'), 'utf8');
    const m = env.match(/^\s*FACTS_ROOT\s*=\s*(.+?)\s*$/m);
    return m ? m[1].replace(/^["']|["']$/g, '') : null;
  } catch (_) { return null; }
}

/**
 * The repo root — where claude/ lives.
 *
 * Normally discord/ sits inside the repo and '..' is right. A working copy kept
 * somewhere else needs telling: FACTS_ROOT in the environment, or in .env beside
 * this file so it only has to be said once.
 */
const ROOT = process.env.FACTS_ROOT || envFactsRoot() || path.resolve(__dirname, '..');
/**
 * Where the figures get written.
 *
 * FACTS_OUT exists so a test can run --write without destroying the real file.
 * It has to: verify.js runs this against a stand-in repo, and the first version
 * of that test wrote fixture figures straight over a good .facts.json — the
 * suite quietly corrupting the thing it was checking.
 */
const OUT = process.env.FACTS_OUT || path.join(__dirname, '.facts.json');

const PROMPT = path.join(ROOT, 'claude', 'next-session-prompt.md');

const problems = [];
const fail = (m) => { problems.push(m); return null; };

const comma = (n) => String(n).replace(/\B(?=(\d{3})+(?!\d))/g, ',');

function read(file) {
  try { return fs.readFileSync(file, 'utf8'); }
  catch (_) { return null; }
}

/**
 * Pull one figure out of a body of text.
 *
 * Returns { value, line } or null — and a null is recorded as a problem with the
 * name of what was being looked for, so the output can say WHICH fact went
 * missing rather than printing a blank.
 */
function grab(name, text, file, re, pick = (m) => m[1]) {
  if (text == null) return fail(
    `${name}: could not read ${file}\n` +
    `      Looked for the repo at ${ROOT}. If you are running from a copy of\n` +
    `      discord/ that lives outside the repo, point it at the real one:\n` +
    `      FACTS_ROOT=/path/to/monmmo node facts.js\n` +
    `      — or put FACTS_ROOT=/path/to/monmmo in .env beside this file.`);
  const m = text.match(re);
  if (!m) return fail(`${name}: no line in ${path.relative(ROOT, file)} matched ${re}`);
  const line = (text.slice(0, m.index).split('\n').length);
  const value = pick(m);
  if (value == null || value === '') return fail(`${name}: matched but produced nothing`);
  return { value, line, file: path.relative(ROOT, file) };
}

/** Every milestone document, newest first. */
function milestones() {
  const dir = path.join(ROOT, 'claude');
  let names;
  try { names = fs.readdirSync(dir); } catch (_) { return []; }
  return names
    .map((n) => ({ n, num: Number((n.match(/^milestone-(\d+)-/) || [])[1]) }))
    .filter((x) => Number.isFinite(x.num))
    .sort((a, b) => b.num - a.num)
    .map((x) => ({ file: path.join(dir, x.n), num: x.num }));
}

/** The newest milestone document, by the number in its filename. */
function newestMilestone() {
  return milestones()[0] || null;
}

/**
 * The most recent milestone that states a test count, and what it said.
 *
 * NOT every milestone states one — plenty close on a table of broken guards and
 * never mention the suite. So this walks back until it finds one, and reports
 * which it used. An older milestone naming a SMALLER number is normal and not a
 * problem; the counts only go up.
 */
function lastStatedTestCount() {
  for (const ms of milestones()) {
    const body = read(ms.file);
    if (!body) continue;
    const all = [...body.matchAll(/(\d{3,5})\s*(?:→|->)\s*(\d{3,5})\s+tests|(\d{3,5})\s+tests,\s*all green/g)];
    const last = all[all.length - 1];
    if (last) return { value: last[2] || last[3], num: ms.num, file: path.relative(ROOT, ms.file) };
  }
  return null;
}

function collect() {
  const prompt = read(PROMPT);
  const facts = {};
  const sources = {};

  const put = (key, got, format = (v) => v) => {
    if (!got) return;
    facts[key] = format(got.value);
    sources[key] = `${got.file}:${got.line}`;
  };

  // ── the test count, from two places ───────────────────────────────────────
  const promptTests = grab('TESTS (prompt)', prompt, PROMPT, /(\d{3,5})\s+tests green/);

  const ms = newestMilestone();
  if (!ms) fail('TESTS (milestone): no claude/milestone-<n>-*.md found');

  // The second source. Only ONE direction is a problem: a milestone claiming
  // MORE tests than the prompt means the prompt has been left behind, which is
  // the exact failure that cost this project two sessions. A milestone claiming
  // fewer is just an older milestone.
  const stated = lastStatedTestCount();
  if (promptTests && stated && Number(stated.value) > Number(promptTests.value)) {
    problems.push(
      `TESTS: the prompt says ${promptTests.value} but ${path.basename(stated.file)} ` +
      `already says ${stated.value}. The prompt is behind its own milestones. ` +
      `Re-run the suite and fix it — do not guess which is right.`
    );
  }
  put('TESTS', promptTests, comma);
  if (stated) sources.TESTS += `  (>= ${stated.value} from milestone ${stated.num})`;
  if (ms) facts.MILESTONE = String(ms.num);

  if (!prompt) {
    return { facts, sources };
  }

  // ── where the reading stands ──────────────────────────────────────────────
  const scripts = grab('SCRIPTS/MAPS/BLOCKS', prompt, PROMPT,
    /(\d+)\s+scripts on\s+(\d+)\s+maps, reaching\s+(\d+)\s+blocks/);
  if (scripts) {
    const m = prompt.match(/(\d+)\s+scripts on\s+(\d+)\s+maps, reaching\s+(\d+)\s+blocks/);
    put('SCRIPTS',    { ...scripts, value: m[1] }, comma);
    put('MAPS_TOTAL', { ...scripts, value: m[2] });
    put('BLOCKS',     { ...scripts, value: m[3] }, comma);
  }

  const blocks = prompt.match(/(\d+)\s+read to a proper end,\s+(\d+)\s+stopped/);
  if (!blocks) fail('BLOCKS_READ/STOPPED: no "N read to a proper end, N stopped" line');
  else {
    const line = prompt.slice(0, blocks.index).split('\n').length;
    const at = { line, file: path.relative(ROOT, PROMPT) };
    put('BLOCKS_READ',    { ...at, value: blocks[1] }, comma);
    put('BLOCKS_STOPPED', { ...at, value: blocks[2] });
  }

  const gates = prompt.match(/(\d+)\s+flags gate something;\s+(\d+)\s+are moved by a script somewhere;\s+(\d+)\s+are the code boundary/);
  if (!gates) fail('GATES: no "N flags gate something; N are moved by a script somewhere; N are the code boundary" line');
  else {
    const line = prompt.slice(0, gates.index).split('\n').length;
    const at = { line, file: path.relative(ROOT, PROMPT) };
    put('GATES_TOTAL',     { ...at, value: gates[1] });
    put('GATES_BY_SCRIPT', { ...at, value: gates[2] });
    put('GATES_BOUNDARY',  { ...at, value: gates[3] });
  }

  // ── the floor table: the widest run, and the floor ────────────────────────
  const widest = prompt.match(/--play --say-yes --boat --in-order\s+(\d+)\s*\/\s*(\d+)/);
  if (!widest) fail('MAPS_REACHED: no "--play --say-yes --boat --in-order  N / N" row in the floor table');
  else {
    const line = prompt.slice(0, widest.index).split('\n').length;
    const at = { line, file: path.relative(ROOT, PROMPT) };
    put('MAPS_REACHED', { ...at, value: widest[1] });
    put('FLAGS_SET',    { ...at, value: widest[2] });
  }

  const floor = prompt.match(/^--play\s{2,}(\d+)\s*\/\s*(\d+)/m);
  if (!floor) fail('MAPS_FLOOR: no bare "--play  N / N" row in the floor table');
  else {
    const line = prompt.slice(0, floor.index).split('\n').length;
    put('MAPS_FLOOR', { line, file: path.relative(ROOT, PROMPT), value: floor[1] });
  }

  return { facts, sources };
}

function main() {
  const { facts, sources } = collect();
  const keys = Object.keys(facts).sort();

  console.log(c.head('\nFacts read from the repo\n'));
  if (!keys.length) console.log(c.skip('  nothing — see the problems below'));
  for (const k of keys) {
    const from = sources[k] ? c.skip(`  ← ${sources[k]}`) : c.skip('  ← derived');
    console.log(`  ${c.ok(k.padEnd(16))} ${String(facts[k]).padEnd(8)}${from}`);
  }

  if (problems.length) {
    console.log(c.err(`\n${problems.length} problem(s):\n`));
    for (const p of problems) console.log(c.err(`  - ${p}`));
    console.log('');
  }

  // A missing fact is never written. Half a facts file is how a channel ends up
  // saying "**  tests**" and nobody notices for a fortnight.
  if (problems.length && (WRITE || CHECK)) {
    console.log(c.err('Nothing written. Fix the source, or the copy that quotes it.\n'));
    process.exit(1);
  }

  if (WRITE) {
    const prev = (() => { try { return JSON.parse(fs.readFileSync(OUT, 'utf8')); } catch (_) { return null; } })();
    const next = { facts, sources, at: new Date().toISOString() };
    const same = prev && JSON.stringify(prev.facts) === JSON.stringify(facts);
    fs.writeFileSync(OUT, JSON.stringify(next, null, 2) + '\n');
    console.log(same
      ? c.skip(`\n${path.basename(OUT)} rewritten — no figure changed.\n`)
      : c.head(`\n${path.basename(OUT)} written — ${prev ? 'figures moved' : 'first run'}.\n`));
    // 0 = unchanged, 10 = something moved. The workflow reads this.
    process.exit(same ? 0 : 10);
  }

  console.log(c.skip('\nNothing written. Use --write.\n'));
}

/** What the rest of the tooling calls. Returns {} if the file was never built. */
function load() {
  try { return JSON.parse(fs.readFileSync(OUT, 'utf8')).facts || {}; }
  catch (_) { return {}; }
}

module.exports = { load, collect, newestMilestone, OUT };

if (require.main === module) main();
