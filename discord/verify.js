/**
 * Offline checks. No Discord connection, no token needed.
 *
 *   node verify.js
 *
 * Exercises the permission merge, the name matching, the AutoMod patterns and
 * the copy, so mistakes surface here rather than in a half-built server.
 */
'use strict';

process.env.DISCORD_TOKEN = process.env.DISCORD_TOKEN || 'offline';
process.env.GUILD_ID = process.env.GUILD_ID || '000000000000000000';

const { PermissionFlagsBits: P, ChannelType } = require('discord.js');
const { mergeReadonly, slug, fill, ROLES, TREE, BUG_TAGS, ROM_KEYWORDS, ROM_REGEX } = require('./setup-server.js');
const COPY = require('./content.js');

let pass = 0, fail = 0;
const t = (name, fn) => {
  try { fn(); console.log(`  \x1b[32mok\x1b[0m    ${name}`); pass++; }
  catch (e) { console.log(`  \x1b[31mFAIL\x1b[0m  ${name}\n        ${e.message}`); fail++; }
};
const assert = (cond, msg) => { if (!cond) throw new Error(msg); };

const EVERYONE = '1';
const TESTER = '2';
const OPERATOR = '3';
const ARCHIVIST = '4';
const STAFF = [OPERATOR, ARCHIVIST];

const publicCat = [{ id: EVERYONE, allow: [P.ViewChannel], deny: [] }];
const gatedCat = [
  { id: EVERYONE, allow: [], deny: [P.ViewChannel] },
  { id: TESTER, allow: [P.ViewChannel, P.ReadMessageHistory, P.SendMessages, P.SendMessagesInThreads, P.Connect, P.Speak] },
  { id: OPERATOR, allow: [P.ViewChannel, P.ReadMessageHistory, P.SendMessages, P.SendMessagesInThreads, P.Connect, P.Speak] },
  { id: ARCHIVIST, allow: [P.ViewChannel, P.ReadMessageHistory, P.SendMessages, P.SendMessagesInThreads, P.Connect, P.Speak] },
];
const find = (ow, id) => ow.find((o) => o.id === id) || { allow: [], deny: [] };

console.log('\nPermissions');

t('a locked public channel is readable by everyone but not writable', () => {
  const ow = mergeReadonly(publicCat, 'readonly', EVERYONE, STAFF);
  const e = find(ow, EVERYONE);
  assert(e.allow.includes(P.ViewChannel), '@everyone lost read access');
  assert(e.deny.includes(P.SendMessages), '@everyone can still post');
});

t('a locked channel in a gated category stays hidden from @everyone', () => {
  const ow = mergeReadonly(gatedCat, 'readonly', EVERYONE, STAFF);
  const e = find(ow, EVERYONE);
  assert(e.deny.includes(P.ViewChannel), 'THE GATE LEAKED — @everyone can see a tester channel');
  assert(!e.allow.includes(P.ViewChannel), 'ViewChannel is in allow and deny at once');
});

t('the gated role keeps read but loses write in a locked channel', () => {
  const ow = mergeReadonly(gatedCat, 'readonly', EVERYONE, STAFF);
  const g = find(ow, TESTER);
  assert(g.allow.includes(P.ViewChannel), 'Field Tester cannot see #build-drops');
  assert(g.deny.includes(P.SendMessages), 'Field Tester can post in a locked channel');
});

t('staff can always post in a locked channel', () => {
  for (const cat of [publicCat, gatedCat]) {
    const ow = mergeReadonly(cat, 'readonly', EVERYONE, STAFF);
    for (const id of STAFF) {
      const s = find(ow, id);
      assert(s.allow.includes(P.SendMessages), 'staff cannot post');
      assert(s.allow.includes(P.ViewChannel), 'staff cannot see');
      assert(!s.deny.includes(P.SendMessages), 'staff denied posting');
    }
  }
});

t('readonly-threads lets members reply in threads but not in the channel', () => {
  const ow = mergeReadonly(publicCat, 'readonly-threads', EVERYONE, STAFF);
  const e = find(ow, EVERYONE);
  assert(e.deny.includes(P.SendMessages), 'members can post directly in #devlog');
  assert(e.allow.includes(P.SendMessagesInThreads), 'members cannot reply in #devlog threads');
  assert(e.deny.includes(P.CreatePublicThreads), 'members can start threads in a locked channel');
});

t('no permission is ever in allow and deny at the same time', () => {
  for (const cat of [publicCat, gatedCat]) {
    for (const mode of ['readonly', 'readonly-threads']) {
      for (const o of mergeReadonly(cat, mode, EVERYONE, STAFF)) {
        const clash = o.allow.filter((p) => o.deny.includes(p));
        assert(clash.length === 0, `overwrite ${o.id} has ${clash.length} contradictory flag(s)`);
      }
    }
  }
});

t('merging does not mutate the category overwrites', () => {
  const before = JSON.stringify(gatedCat, (k, v) => (typeof v === 'bigint' ? v.toString() : v));
  mergeReadonly(gatedCat, 'readonly', EVERYONE, STAFF);
  const after = JSON.stringify(gatedCat, (k, v) => (typeof v === 'bigint' ? v.toString() : v));
  assert(before === after, 'the category overwrite objects were mutated — a second run would drift');
});

console.log('\nStructure');

t('every role key referenced by a gate exists', () => {
  const keys = new Set(ROLES.map((r) => r.key));
  for (const cat of TREE) {
    if (cat.gate && cat.gate !== '__staff__') assert(keys.has(cat.gate), `unknown gate role "${cat.gate}"`);
  }
});

t('channel keys are unique', () => {
  const seen = new Set();
  for (const cat of TREE) for (const ch of cat.channels) {
    assert(!seen.has(ch.key), `duplicate channel key "${ch.key}"`);
    seen.add(ch.key);
  }
});

t('every piece of copy has a channel to live in', () => {
  const keys = new Set(TREE.flatMap((c) => c.channels.map((ch) => ch.key)));
  for (const k of Object.keys(COPY)) assert(keys.has(k), `copy for "${k}" has nowhere to go`);
});

t('every non-voice channel has a topic', () => {
  for (const cat of TREE) for (const ch of cat.channels) {
    if (ch.type !== 'voice') assert(ch.topic, `#${ch.name} has no topic`);
  }
});

t('channel name matching survives Discord renaming', () => {
  assert(slug('build-drops', ChannelType.GuildText) === 'build-drops', 'text slug changed');
  assert(slug('Build Drops', ChannelType.GuildText) === 'build-drops', 'spaces not hyphenated');
  assert(slug('Playtest', ChannelType.GuildVoice) === 'Playtest', 'voice name was lowercased');
});

t('forum tags are within Discord limits', () => {
  assert(BUG_TAGS.length <= 20, `${BUG_TAGS.length} forum tags, max is 20`);
  for (const tag of BUG_TAGS) assert(tag.name.length <= 20, `tag "${tag.name}" is over 20 chars`);
});

console.log('\nAutoMod');

t('keyword entries only use * at the start or end', () => {
  for (const k of ROM_KEYWORDS) {
    const inner = k.slice(1, -1);
    assert(!inner.includes('*'), `"${k}" has a * in the middle — Discord matches that literally`);
    assert(k.length <= 60, `"${k}" is over Discord's 60-char keyword limit`);
  }
  assert(ROM_KEYWORDS.length <= 1000, 'too many keywords');
});

t('regex patterns compile and are under 260 chars', () => {
  for (const r of ROM_REGEX) {
    assert(r.length <= 260, `pattern is ${r.length} chars, limit is 260`);
    new RegExp(r.replace('(?i)', ''), 'i'); // throws if malformed
  }
});

t('the filter catches what it is meant to catch', () => {
  const hits = (s) => ROM_REGEX.some((r) => new RegExp(r.replace('(?i)', ''), 'i').test(s))
    || ROM_KEYWORDS.some((k) => s.toLowerCase().includes(k.replace(/^\*|\*$/g, '')));
  const mustBlock = [
    'magnet:?xt=urn:btih:abc123',
    'here you go firered.gba',
    'anyone got a rom link',
    'where can i download the rom',
    'how do i find a rom for this',
    'check emuparadise',
  ];
  for (const s of mustBlock) assert(hits(s), `NOT blocked: "${s}"`);
});

t('normal dev conversation is not blocked', () => {
  const hits = (s) => ROM_REGEX.some((r) => new RegExp(r.replace('(?i)', ''), 'i').test(s))
    || ROM_KEYWORDS.some((k) => s.toLowerCase().includes(k.replace(/^\*|\*$/g, '')));
  const mustPass = [
    'the ROM hash check rejects v1.1, offsets are build-specific',
    'metatile behaviour byte is at +0x2 in the header',
    'GBATEK says the LCG seed is stored there',
    'my .sav loaded fine after the fix',
    'physical vs special is decided by the type, not the move',
    'the extractor reads 1639 objects, 441 of them trainers',
    'where do I find the encounter tables in the repo',
  ];
  for (const s of mustPass) assert(!hits(s), `FALSE POSITIVE on: "${s}"`);
});

console.log('\nCopy');

t('every message fits inside Discord\'s 2000-character limit', () => {
  for (const [k, msgs] of Object.entries(COPY)) {
    msgs.forEach((m, i) => {
      const n = fill(m).length;
      assert(n <= 2000, `${k}[${i}] is ${n} chars`);
    });
  }
});

t('no placeholder survives substitution', () => {
  for (const [k, msgs] of Object.entries(COPY)) {
    msgs.forEach((m, i) => assert(!fill(m).includes('{{'), `${k}[${i}] still has an unreplaced {{placeholder}}`));
  }
});

t('every channel referenced in the copy actually exists', () => {
  const names = new Set(TREE.flatMap((c) => c.channels.map((ch) => slug(ch.name, ch.type === 'voice' ? ChannelType.GuildVoice : ChannelType.GuildText))));
  for (const [k, msgs] of Object.entries(COPY)) {
    for (const m of msgs) {
      for (const ref of m.match(/#[a-z0-9-]+/g) || []) {
        const n = ref.slice(1);
        assert(names.has(n), `${k} links to ${ref}, which is not a channel in the tree`);
      }
    }
  }
});

t('rule 1 is stated in the pinned (first) rules message', () => {
  assert(/rule 1|## 1\./i.test(COPY.rules[0]), 'the ROM rule is not in the message that gets pinned');
  assert(/permanent ban/i.test(COPY.rules[0]), 'the consequence is not stated up front');
});

// ── automation ──────────────────────────────────────────────────────────────
const lib = require('./lib.js');

console.log('\nMessage splitting');

t('short text stays as one message', () => {
  assert(lib.chunk('hello').length === 1, 'split a five-character message');
});

t('long text splits into pieces that all fit', () => {
  const long = Array.from({ length: 400 }, (_, i) => `line ${i} of some devlog prose`).join('\n');
  const parts = lib.chunk(long);
  assert(parts.length > 1, 'did not split');
  for (const p of parts) assert(p.length <= 2000, `piece is ${p.length} chars`);
  // Nothing may be silently dropped.
  const rejoined = parts.join('\n').replace(/\n+/g, '\n');
  assert(rejoined.includes('line 0 '), 'lost the start');
  assert(rejoined.includes('line 399 '), 'lost the end');
});

t('a code block is never left unterminated by a split', () => {
  const body = '```\n' + Array.from({ length: 300 }, (_, i) => `0x${i.toString(16).padStart(4, '0')}  some log line here`).join('\n') + '\n```';
  const parts = lib.chunk(body);
  assert(parts.length > 1, 'test needs a case that actually splits');
  for (const [i, p] of parts.entries()) {
    const fences = (p.match(/```/g) || []).length;
    assert(fences % 2 === 0, `piece ${i} has ${fences} fences — a code block is left hanging`);
  }
});

t('a single line longer than the limit is hard-split rather than dropped', () => {
  const parts = lib.chunk('x'.repeat(5000));
  assert(parts.length >= 3, 'did not split the long line');
  for (const p of parts) assert(p.length <= 2000, 'piece over the limit');
  assert(parts.join('').replace(/\n/g, '').length === 5000, 'characters were lost');
});

console.log('\nFront matter');

t('front matter is parsed and stripped from the body', () => {
  const { meta, body } = lib.frontmatter('---\nchannel: devlog\nping: build\nthread: true\n---\n\nhello there');
  assert(meta.channel === 'devlog', 'channel not read');
  assert(meta.ping === 'build', 'ping not read');
  assert(meta.thread === true, 'boolean not coerced');
  assert(body === 'hello there', `body was "${body}"`);
});

t('a file with no front matter is all body', () => {
  const { meta, body } = lib.frontmatter('just a note');
  assert(Object.keys(meta).length === 0, 'invented metadata');
  assert(body === 'just a note', 'mangled the body');
});

t('a horizontal rule in the body is not mistaken for front matter', () => {
  const { meta, body } = lib.frontmatter('some prose\n\n---\n\nmore prose');
  assert(Object.keys(meta).length === 0, 'read a mid-document rule as front matter');
  assert(body.includes('more prose'), 'ate the second half');
});

t('every post in posts/ names a real channel, or is skipped as documentation', () => {
  const fsx = require('fs');
  if (!fsx.existsSync('./posts')) return;
  const keys = new Set(TREE.flatMap((c) => c.channels.map((ch) => ch.key)));
  for (const f of fsx.readdirSync('./posts').filter((n) => n.endsWith('.md'))) {
    const { meta } = lib.frontmatter(fsx.readFileSync(`./posts/${f}`, 'utf8'));
    if (!meta.channel) continue;                    // documentation — post.js skips it
    assert(keys.has(meta.channel), `posts/${f} targets "${meta.channel}", which is not a channel`);
  }
});

t('a post with no channel is skipped rather than failing the workflow', () => {
  const src = require('fs').readFileSync('./post.js', 'utf8');
  assert(/no channel in its front matter/.test(src),
    'a stray markdown file in posts/ would red the build instead of being skipped');
});

t('the example post parses and names a real channel', () => {
  const fsx = require('fs');
  const p = './posts/EXAMPLE-2026-09-01-the-three-flags.md';
  if (!fsx.existsSync(p)) return;
  const { meta, body } = lib.frontmatter(fsx.readFileSync(p, 'utf8'));
  const keys = new Set(TREE.flatMap((c) => c.channels.map((ch) => ch.key)));
  assert(keys.has(meta.channel), `example post targets "${meta.channel}", which is not a channel`);
  assert(body.length > 50, 'example body is empty');
});

console.log('\nSync safety');

t('identical copy hashes identically, changed copy does not', () => {
  assert(lib.hash('abc') === lib.hash('abc'), 'hash is not stable — sync would repost every run');
  assert(lib.hash('abc') !== lib.hash('abd'), 'hash collides — sync would miss an edit');
});

t('the weekly post is honest about a quiet week', () => {
  const { build } = require('./weekly.js');
  const quiet = build({ commits: [], authors: 0, files: 0, added: 0, removed: 0 }, null, null);
  assert(/no commits/i.test(quiet), 'a quiet week produces a misleading post');
});

t('the weekly post flags a falling test count instead of burying it', () => {
  const { build } = require('./weekly.js');
  const out = build({ commits: ['abc\tdid a thing'], authors: 1, files: 3, added: 40, removed: 5 }, 1190, 1203);
  assert(out.includes('-13'), 'the delta is not shown');
  assert(/went \*\*down\*\*/.test(out), 'a dropping test count is not called out');
});

console.log('\nWholesale replace');

const syncSrc = require('fs').readFileSync('./sync.js', 'utf8');
const replaceList = (syncSrc.match(/REPLACE_CHANNELS \?\? '([^']*)'/) || [, ''])[1]
  .split(',').map((s) => s.trim()).filter(Boolean);

t('every replaced channel exists', () => {
  const keys = new Set(TREE.flatMap((cat) => cat.channels.map((ch) => ch.key)));
  assert(replaceList.length > 0, 'nothing is set to replace — was the list emptied by accident?');
  for (const k of replaceList) assert(keys.has(k), `REPLACE_WHOLESALE names "${k}", which is not a channel`);
});

t('replaced channels are locked, so the bot never posts anything else there', () => {
  const byKey = new Map(TREE.flatMap((cat) => cat.channels.map((ch) => [ch.key, ch])));
  for (const k of replaceList) {
    const spec = byKey.get(k);
    assert(spec, `no channel spec for "${k}"`);
    assert(spec.mode && spec.mode.startsWith('readonly'),
      `#${k} is NOT read-only. Wholesale replace deletes every message this bot has posted there — ` +
      `in an open channel that could delete something that was not copy.`);
  }
});

t('a channel that receives posts is never wholesale-replaced', () => {
  // devlog, milestones, changelog, build-drops and commits accumulate real
  // content from post.js, daily.js and webhooks. Deleting there loses history.
  for (const k of ['devlog', 'milestones', 'changelog', 'build-drops', 'commits', 'general']) {
    assert(!replaceList.includes(k), `#${k} accumulates posts — wholesale replace would delete them`);
  }
});

t('the wipe only ever targets the bot\'s own messages', () => {
  const fn = syncSrc.slice(syncSrc.indexOf('async function wipeOwnMessages'), syncSrc.indexOf('async function main'));
  assert(/author\.id === meId/.test(fn), 'the wipe does not filter on author id');
  assert(/WIPE_GUARD/.test(fn), 'the wipe has no upper bound');
  assert(/bulkDelete/.test(fn) && /m\.delete\(\)/.test(fn),
    'the wipe must handle both bulk delete and messages older than 14 days, which bulk delete refuses');
});

console.log('\nOnboarding');

const FIN = require('./finish-setup.js');
const channelNames = new Set(TREE.flatMap((cat) => cat.channels)
  .filter((ch) => ch.type !== 'voice')
  .map((ch) => ch.name.toLowerCase()));
const roleNames = new Set(ROLES.map((r) => r.name));

t('every role finish-setup reorders actually exists', () => {
  for (const n of FIN.ROLE_ORDER) assert(roleNames.has(n), `no role named "${n}"`);
});

t('every onboarding default channel exists', () => {
  for (const n of FIN.DEFAULT_CHANNELS) assert(channelNames.has(n), `no channel named "${n}"`);
});

t('onboarding offers enough channels members can post in', () => {
  const bySlug = new Map(TREE.flatMap((cat) => cat.channels.map((ch) => [ch.name.toLowerCase(), ch])));
  const writable = FIN.DEFAULT_CHANNELS.filter((n) => !bySlug.get(n)?.mode);
  assert(writable.length >= 5, `only ${writable.length} writable defaults — Discord requires at least 5 to enable onboarding`);
});

t('every channel and role named in an onboarding prompt exists', () => {
  for (const p of FIN.PROMPTS) {
    assert(p.options.length > 0 && p.options.length <= 50, `prompt "${p.title}" has ${p.options.length} options`);
    for (const o of p.options) {
      for (const n of o.channels) assert(channelNames.has(n), `prompt "${p.title}" → "${o.title}" points at missing channel "${n}"`);
      for (const n of o.roles) assert(roleNames.has(n), `prompt "${p.title}" → "${o.title}" grants missing role "${n}"`);
      assert(o.title.length <= 50, `option title "${o.title}" is over 50 chars`);
      assert(!o.description || o.description.length <= 100, `option "${o.title}" description over 100 chars`);
    }
  }
});

t('the ping roles are all reachable through onboarding', () => {
  const granted = new Set(FIN.PROMPTS.flatMap((p) => p.options.flatMap((o) => o.roles)));
  for (const n of ['devlog pings', 'build pings', 'playtest pings']) {
    assert(granted.has(n), `"${n}" cannot be self-assigned — members would need a reaction-role bot`);
  }
});

t('onboarding never hands out a privileged role', () => {
  const granted = new Set(FIN.PROMPTS.flatMap((p) => p.options.flatMap((o) => o.roles)));
  for (const n of ['Operator', 'Archivist', 'Cartographer', 'Field Tester']) {
    assert(!granted.has(n), `ONBOARDING GRANTS "${n}" — anyone joining could self-serve alpha or staff access`);
  }
});

t('onboarding does not advertise a gated channel to everyone', () => {
  const gated = new Set(TREE.filter((cat) => cat.gate).flatMap((cat) => cat.channels).map((ch) => ch.name.toLowerCase()));
  for (const n of FIN.DEFAULT_CHANNELS) {
    assert(!gated.has(n), `#${n} is behind a gate but is an onboarding default — new members would see a dead link`);
  }
  for (const p of FIN.PROMPTS) {
    for (const o of p.options) {
      for (const n of o.channels) {
        assert(!gated.has(n), `prompt option "${o.title}" points at gated #${n} — a door new members cannot open`);
      }
    }
  }
});

console.log('\nSubsystem routing');

const ROUTE = require('./route.js');

t('every routed channel exists and is not gated', () => {
  const gated = new Set(TREE.filter((cat) => cat.gate).flatMap((cat) => cat.channels).map((ch) => ch.name.toLowerCase()));
  for (const r of ROUTE.ROUTES) {
    assert(channelNames.has(r.channel), `route "${r.prefix}" targets missing channel #${r.channel}`);
    assert(!gated.has(r.channel), `route "${r.prefix}" targets gated #${r.channel}`);
  }
});

t('specific path prefixes win over general ones', () => {
  // src/Core/Battle/ must be matched before the catch-all src/Core/.
  const hit = ROUTE.classify(['src/Core/Battle/Damage.cs']);
  assert(hit.has('battle-engine'), 'a battle file did not reach #battle-engine');
  assert(!hit.has('engine-and-netcode'), 'a battle file also matched the general Core route');
});

t('one push touching two subsystems reaches both channels once', () => {
  const hit = ROUTE.classify([
    'src/Core/Battle/Damage.cs',
    'src/Core/Battle/TypeChart.cs',
    'src/RomExtract/Maps/MapLinkExtractor.cs',
  ]);
  assert(hit.size === 2, `expected 2 channels, got ${hit.size}`);
  assert(hit.get('battle-engine').files === 2, 'file count wrong');
  assert(hit.get('data-and-extraction').files === 1, 'file count wrong');
});

t('unrelated files route nowhere', () => {
  const hit = ROUTE.classify(['README.md', 'discord/content.js', '.github/workflows/x.yml']);
  assert(hit.size === 0, 'a non-source file was routed to a channel');
});

t('a falling test count is called out, not smoothed over', () => {
  const entry = { labels: new Set(['Core/Battle']), files: 2 };
  const down = ROUTE.message(entry, { count: 1190, prev: 1203, commits: [], compareUrl: '' });
  assert(down.includes('-13'), 'the delta is missing');
  assert(/went \*\*down\*\*/.test(down), 'a regression is not flagged');
});

t('the message admits the count is repo-wide, not per-subsystem', () => {
  const entry = { labels: new Set(['Core/Battle']), files: 1 };
  const up = ROUTE.message(entry, { count: 1211, prev: 1203, commits: [], compareUrl: '' });
  assert(/Repo-wide count/i.test(up), 'the message implies the delta belongs to this subsystem');
});

t('routed messages fit in one Discord message', () => {
  const entry = { labels: new Set(['Core/Battle', 'Core/World', 'Server']), files: 40 };
  const commits = Array.from({ length: 30 }, (_, i) => `abc${i}  a fairly long commit subject line number ${i}`);
  const out = ROUTE.message(entry, { count: 1211, prev: 1203, commits, compareUrl: 'https://github.com/a/b/compare/x...y' });
  assert(out.length <= 2000, `routed message is ${out.length} chars`);
});

console.log('\nDaily recap');

const DAILY = require('./daily.js');
const emptyDay = { commits: [], files: [], added: 0, removed: 0 };
const busyDay = { commits: [{ sha: 'a1', subject: 'x' }, { sha: 'a2', subject: 'y' }], files: ['src/Core/Battle/Damage.cs'], added: 40, removed: 5 };

t('a quiet day says so instead of posting an empty recap', () => {
  const out = DAILY.compose({ date: '2026-08-15', summary: '', data: emptyDay, tests: null, prevTests: null, next: null });
  assert(/No commits today/.test(out), 'a quiet day produces a blank or misleading post');
  assert(out.length < 300, 'a quiet day post is padded out');
});

t('a quiet day still shows what is next', () => {
  const out = DAILY.compose({ date: '2026-08-15', summary: '', data: emptyDay, tests: null, prevTests: null, next: '- the three flags' });
  assert(/the three flags/.test(out), 'the Next section vanished on a quiet day');
});

t('a falling test count is flagged in the recap too', () => {
  const out = DAILY.compose({ date: '2026-08-15', summary: 'stuff', data: busyDay, tests: 1190, prevTests: 1203, next: null });
  assert(/went \*\*down\*\*/.test(out), 'a regression is not called out');
  assert(out.includes('(-13)'), 'the delta is missing');
});

t('the fallback is disclosed, never passed off as a written summary', () => {
  const out = DAILY.compose({ date: '2026-08-15', summary: 'x', fellBack: true, data: busyDay, tests: null, prevTests: null, next: null });
  assert(/without the model/i.test(out), 'a verb-sorted list is presented as if it were written prose');
});

t('verb sorting buckets commits sensibly', () => {
  const cats = DAILY.categorise([
    { subject: 'Add STRENGTH boulder pushing' },
    { subject: 'Fixed the double HM01 on a second conversation' },
    { subject: 'People slide between squares instead of jumping' },
    { subject: 'Implement the lift key script chain' },
  ]);
  assert(cats.Added.length === 2, `Added got ${cats.Added.length}`);
  assert(cats.Fixed.length === 1, `Fixed got ${cats.Fixed.length}`);
  assert(cats.Changed.length === 1, `Changed got ${cats.Changed.length}`);
});

t('NEXT.md notes above the separator are not posted', () => {
  const body = DAILY.stripNextHeader('Instructions for me.\nDo not post this.\n\n---\n\n- the three flags\n');
  assert(!/Instructions/.test(body), 'the header leaked into the post');
  assert(/the three flags/.test(body), 'the actual content was lost');
});

t('a NEXT.md with no separator is posted whole', () => {
  const body = DAILY.stripNextHeader('- the three flags\n- the saffron guards\n');
  assert(/three flags/.test(body) && /saffron/.test(body), 'content was dropped');
});

t('a horizontal rule deep in the file is not treated as the separator', () => {
  const long = Array.from({ length: 25 }, (_, i) => `- item ${i}`).join('\n') + '\n---\n- after';
  const body = DAILY.stripNextHeader(long);
  assert(/item 0/.test(body), 'a rule on line 26 was treated as a header cut');
});

t('the prompt sends commit messages and paths, never file contents', () => {
  const p = DAILY.buildPrompt({
    bodies: [{ sha: 'a1', subject: 'Add STRENGTH', body: '' }],
    files: ['src/Core/Battle/Damage.cs'], added: 10, removed: 2, tests: 1211, prevTests: 1203,
  });
  assert(p.includes('src/Core/Battle/Damage.cs'), 'paths missing');
  assert(p.includes('Add STRENGTH'), 'commit subjects missing');
  assert(!/diff --git|^\+\+\+|^@@/m.test(p), 'the prompt carries diff content');
});

t('the prompt forbids inventing significance and hiding a test drop', () => {
  const p = DAILY.buildPrompt({ bodies: [], files: [], added: 0, removed: 0, tests: null, prevTests: null });
  assert(/Do NOT invent significance/i.test(p), 'nothing stops the model padding a slow day');
  assert(/test count fell/i.test(p), 'nothing stops the model smoothing over a regression');
});

console.log('\nWorkflows');

const fsw = require('fs');
const pathw = require('path');
const wfDir = fsw.existsSync('./workflows') ? './workflows'
  : fsw.existsSync('../.github/workflows') ? '../.github/workflows' : null;

t('every workflow loads state before running and saves it after', () => {
  if (!wfDir) return;                       // not laid out for checking here
  for (const f of fsw.readdirSync(wfDir).filter((n) => n.endsWith('.yml'))) {
    const y = fsw.readFileSync(pathw.join(wfDir, f), 'utf8');
    assert(y.includes('state.sh load'), `${f} never loads state — it would run against a stale file`);
    assert(y.includes('state.sh save'), `${f} never saves state — its work would be repeated next run`);
    const load = y.indexOf('state.sh load');
    const firstNode = y.search(/run:.*node \w+\.js/);
    if (firstNode !== -1) assert(load < firstNode, `${f} loads state AFTER running a script`);
  }
});

t('no workflow commits to the checked-out branch any more', () => {
  if (!wfDir) return;
  for (const f of fsw.readdirSync(wfDir).filter((n) => n.endsWith('.yml'))) {
    const y = fsw.readFileSync(pathw.join(wfDir, f), 'utf8');
    assert(!/git commit -m .*\[skip ci\]/.test(y), `${f} still commits back to main — that is the race we removed`);
    assert(!/git add \.sync-state\.json/.test(y), `${f} still stages state onto main`);
  }
});

t('state.sh never fails a run', () => {
  const sh = fsw.readFileSync('./state.sh', 'utf8');
  // Every error path must exit 0; a state problem is not a reason to go red.
  const badExits = (sh.match(/exit [1-9]/g) || []).filter((e) => e !== 'exit 2');
  assert(badExits.length === 0, `state.sh has non-zero exits: ${badExits.join(', ')}`);
  assert(sh.includes('commit-tree'), 'state.sh should use plumbing, not checkout');
  assert(!/git checkout|git stash/.test(sh), 'state.sh touches the working tree — it must not');
});

console.log(`\n${pass} passed, ${fail} failed\n`);
process.exit(fail ? 1 : 0);
