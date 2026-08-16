#!/usr/bin/env node
/**
 * Discord server builder.
 *
 * Creates every role, category, channel, permission overwrite, forum tag,
 * pinned message and AutoMod rule in one pass.
 *
 * SAFE TO RE-RUN: anything that already exists by name is skipped, never
 * duplicated and never deleted. Nothing in this script deletes anything.
 *
 *   node setup-server.js            build it
 *   node setup-server.js --dry-run  print the plan, touch nothing
 *
 * See SETUP.md.
 */

'use strict';

// ── env (.env support without a dependency) ─────────────────────────────────
const fs = require('fs');
const path = require('path');
try {
  const envPath = path.join(__dirname, '.env');
  if (fs.existsSync(envPath)) {
    for (const line of fs.readFileSync(envPath, 'utf8').split('\n')) {
      const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$/);
      if (m && !process.env[m[1]]) {
        process.env[m[1]] = m[2].replace(/^["']|["']$/g, '');
      }
    }
  }
} catch (_) { /* .env is optional */ }

const {
  Client,
  GatewayIntentBits,
  ChannelType,
  PermissionFlagsBits: P,
  AutoModerationRuleEventType,
  AutoModerationRuleTriggerType,
  AutoModerationActionType,
  AutoModerationRuleKeywordPresetType,
  ForumLayoutType,
  GuildVerificationLevel,
  GuildExplicitContentFilter,
} = require('discord.js');

const COPY = require('./content.js');
const lib = require('./lib.js');

// ── CONFIG ──────────────────────────────────────────────────────────────────
const CONFIG = {
  token: process.env.DISCORD_TOKEN,
  guildId: process.env.GUILD_ID,
  projectName: process.env.PROJECT_NAME || 'Overworld',
  repoUrl: process.env.REPO_URL || 'https://github.com/ZealSM/monmmo',
};

const DRY = process.argv.includes('--dry-run');

// Reserved names for the two throwaway channels Discord demands before it will
// turn Community mode on. Deleted at the end of the run.
const TEMP_RULES = 'zz-setup-temp-rules';
const TEMP_UPDATES = 'zz-setup-temp-updates';

// ── ROLES ───────────────────────────────────────────────────────────────────
// Listed lowest-first. Discord creates new roles at the bottom of the list, so
// creating in this order produces the correct hierarchy.
const ROLES = [
  { key: 'pingPlaytest', name: 'playtest pings', color: '#4E5058', hoist: false, mentionable: true, perms: [] },
  { key: 'pingBuilds',   name: 'build pings',    color: '#4E5058', hoist: false, mentionable: true, perms: [] },
  { key: 'pingDevlog',   name: 'devlog pings',   color: '#4E5058', hoist: false, mentionable: true, perms: [] },

  { key: 'bugHunter', name: 'Bug Hunter', color: '#B06FD1', hoist: false, mentionable: false, perms: [],
    note: 'awarded for a confirmed, reproduced bug report' },

  { key: 'fieldTester', name: 'Field Tester', color: '#C9A227', hoist: true, mentionable: true, perms: [],
    note: 'closed-alpha access; granted, never requested' },

  { key: 'cartographer', name: 'Cartographer', color: '#5FA97F', hoist: true, mentionable: true,
    perms: [P.CreatePublicThreads, P.CreatePrivateThreads, P.EmbedLinks, P.AttachFiles, P.ManageThreads],
    note: 'contributors who have landed work' },

  { key: 'archivist', name: 'Archivist', color: '#4FA3D1', hoist: true, mentionable: true,
    perms: [
      P.ManageMessages, P.ManageThreads, P.ModerateMembers, P.KickMembers, P.BanMembers,
      P.ManageNicknames, P.ViewAuditLog, P.MuteMembers, P.DeafenMembers, P.MoveMembers,
      P.MentionEveryone, P.ManageEvents,
    ],
    note: 'moderators' },

  { key: 'operator', name: 'Operator', color: '#E8663D', hoist: true, mentionable: false,
    perms: [P.Administrator],
    note: 'you — full control' },

  { key: 'bots', name: 'Bots', color: '#7A7A7A', hoist: true, mentionable: false, perms: [] },
];

const STAFF = ['operator', 'archivist'];

// ── PERMISSION SHORTHANDS ───────────────────────────────────────────────────
// Applied to @everyone on a channel or category.
const VISIBLE_READONLY = {           // anyone can read, nobody can post
  deny: [P.SendMessages, P.CreatePublicThreads, P.CreatePrivateThreads, P.SendMessagesInThreads],
  allow: [P.ViewChannel, P.ReadMessageHistory, P.AddReactions],
};
const VISIBLE_READONLY_THREADS = {   // locked posts, open threads underneath
  deny: [P.SendMessages, P.CreatePublicThreads, P.CreatePrivateThreads],
  allow: [P.ViewChannel, P.ReadMessageHistory, P.AddReactions, P.SendMessagesInThreads],
};
const HIDDEN = { deny: [P.ViewChannel], allow: [] };

// ── CHANNEL TREE ────────────────────────────────────────────────────────────
// `gate` names a role key; the category is hidden from @everyone and shown to
// that role plus staff.
const TREE = [
  {
    name: '📍 START HERE',
    channels: [
      { key: 'welcome',       name: 'welcome',       type: 'text',         mode: 'readonly', topic: 'What this is and where to go next.' },
      { key: 'rules',         name: 'rules',         type: 'text',         mode: 'readonly', topic: 'Read rule 1. It is the one with no warning attached.' },
      { key: 'announcements', name: 'announcements', type: 'announcement', mode: 'readonly', topic: 'Project news. The only channel that pings you unprompted.' },
      { key: 'changelog',     name: 'changelog',     type: 'text',         mode: 'readonly', topic: 'Build-by-build changes, with commit and test count.' },
    ],
  },
  {
    name: '🌿 THE LOBBY',
    channels: [
      { key: 'general',       name: 'general',       type: 'text',  topic: 'Everything and anything.' },
      { key: 'introductions', name: 'introductions', type: 'text',  topic: 'Say hello. One message, optional.' },
      { key: 'screenshots',   name: 'screenshots',   type: 'text',  topic: 'The client, running. No asset dumps.' },
      { key: 'offTopic',      name: 'off-topic',     type: 'text',  topic: 'Not about the project.' },
      { key: 'vcLobby',       name: 'Lobby',         type: 'voice' },
    ],
  },
  {
    name: '🔧 THE WORKSHOP',
    channels: [
      { key: 'devlog',              name: 'devlog',              type: 'text', mode: 'readonly-threads', topic: 'Dev updates. Reply in the thread on each post.' },
      { key: 'milestones',          name: 'milestones',          type: 'text', mode: 'readonly-threads', topic: 'Long-form writeups. Read from the bottom.' },
      { key: 'engine-and-netcode',  name: 'engine-and-netcode',  type: 'text', topic: 'Movement, collision, prediction, the authoritative server, shared Core.' },
      { key: 'battle-engine',       name: 'battle-engine',       type: 'text', topic: 'Damage, type chart, stat stages, turn order, the LCG, move effects.' },
      { key: 'data-and-extraction', name: 'data-and-extraction', type: 'text', topic: 'Structures, offsets, tables, compression, scripts, flags. Data talk, not file talk.' },
      { key: 'suggestions',         name: 'suggestions',         type: 'text', topic: 'Ideas and "why is it like that". One thread per idea.' },
      { key: 'commits',             name: 'commits',             type: 'text', mode: 'readonly', topic: 'GitHub webhook target.' },
      { key: 'vcDev',               name: 'Workshop',            type: 'voice' },
    ],
  },
  {
    name: '🧑‍🔧 JOIN THE PROJECT',
    channels: [
      { key: 'open-roles',   name: 'open-roles',   type: 'text', mode: 'readonly', topic: 'What the project needs. All unpaid — nothing here is ever for sale.' },
      { key: 'apply',        name: 'apply',        type: 'text', topic: 'Post one line naming a role. Staff opens a private thread with you.' },
      { key: 'contributing', name: 'contributing', type: 'text', mode: 'readonly', topic: 'How work actually lands: the guardrails, the tests, the writeup.' },
    ],
  },
  {
    name: '🧪 THE TESTING GROUNDS',
    gate: 'fieldTester',
    channels: [
      { key: 'build-drops',           name: 'build-drops',           type: 'text',  mode: 'readonly', topic: 'Builds. Version, commit, test count, known breakage.' },
      { key: 'bug-reports',           name: 'bug-reports',           type: 'forum', topic: 'One bug per post. Steps to reproduce is the whole report.' },
      { key: 'playtest-coordination', name: 'playtest-coordination', type: 'text',  topic: 'Who is on, which build, what we are trying to break.' },
      { key: 'tester-lounge',         name: 'tester-lounge',         type: 'text',  topic: 'Tester off-topic.' },
      { key: 'vcPlaytest',            name: 'Playtest',              type: 'voice' },
    ],
  },
  {
    name: '🧭 THE FIELD GUIDE',
    channels: [
      { key: 'setup-help',   name: 'setup-help',   type: 'text', topic: 'Cannot get a build running. OS, build, exact error.' },
      { key: 'faq',          name: 'faq',          type: 'text', mode: 'readonly', topic: 'Answered before you ask.' },
      { key: 'known-issues', name: 'known-issues', type: 'text', mode: 'readonly', topic: 'Check here before reporting.' },
    ],
  },
  {
    name: '🗄️ THE BACK ROOM',
    gate: '__staff__',
    channels: [
      { key: 'staff-chat', name: 'staff-chat', type: 'text',  topic: 'Staff only.' },
      { key: 'triage',     name: 'triage',     type: 'text',  topic: 'Sorting bug reports before anyone spends an evening on one.' },
      { key: 'mod-log',    name: 'mod-log',    type: 'text',  topic: 'Moderation and AutoMod actions.' },
      { key: 'vcStaff',    name: 'Staff',      type: 'voice' },
    ],
  },
];

// ── FORUM TAGS ──────────────────────────────────────────────────────────────
// Mapped onto the actual solution layout so triage sorts itself.
const BUG_TAGS = [
  { name: 'needs repro', emoji: '❓', moderated: false },
  { name: 'confirmed',   emoji: '✅', moderated: true  },
  { name: 'fixed',       emoji: '🎉', moderated: true  },
  { name: 'duplicate',   emoji: '🔁', moderated: true  },
  { name: 'crash',       emoji: '💥', moderated: false },
  { name: 'desync',      emoji: '🔀', moderated: false },
  { name: 'Core/Battle', emoji: '⚔️', moderated: false },
  { name: 'Core/World',  emoji: '🗺️', moderated: false },
  { name: 'RomExtract',  emoji: '📦', moderated: false },
  { name: 'Client',      emoji: '🖥️', moderated: false },
  { name: 'Server',      emoji: '🛰️', moderated: false },
  { name: 'rendering',   emoji: '🎨', moderated: false },
  { name: 'UI',          emoji: '🔲', moderated: false },
  { name: 'save/persistence', emoji: '💾', moderated: false },
];

// ── AUTOMOD ─────────────────────────────────────────────────────────────────
// Rule 1, enforced by the machine so it does not depend on anyone being awake.
// Discord's keyword syntax only allows `*` at the START and/or END of an entry —
// a `*` in the middle is matched literally. Anything needing flexibility in the
// middle belongs in ROM_REGEX below.
const ROM_KEYWORDS = [
  '*rom download*', '*download the rom*', '*rom link*', '*link to the rom*',
  '*send me the rom*', '*dm me the rom*', '*dm for the rom*',
  '*where do i get the rom*', '*where can i get the rom*', '*where to get the rom*',
  '*.gba', '*.gbc', '*.nds',
  '*romsmania*', '*emuparadise*', '*coolrom*', '*vimms*', '*romulation*',
  '*edgeemu*', '*1337x*', '*rarbg*', '*nopaystation*', '*archive.org/download*',
];
// AutoMod uses Rust regex: no lookarounds, `(?i)` for case-insensitivity.
const ROM_REGEX = [
  '(?i)magnet:\\?xt=',
  '(?i)\\b\\w+\\.(gba|gbc|nds|z64|iso)\\b',
  '(?i)\\b(rom|iso)s?\\s*(dl|dwnld|download|links?|sites?|mirrors?)\\b',
  '(?i)\\b(where|how)\\b.{0,24}\\b(get|find|download|obtain)\\b.{0,24}\\brom\\b',
];

// ── helpers ─────────────────────────────────────────────────────────────────
const c = {
  ok:   (s) => `\x1b[32m${s}\x1b[0m`,
  skip: (s) => `\x1b[90m${s}\x1b[0m`,
  warn: (s) => `\x1b[33m${s}\x1b[0m`,
  err:  (s) => `\x1b[31m${s}\x1b[0m`,
  head: (s) => `\x1b[1m\x1b[36m${s}\x1b[0m`,
};
const log  = (...a) => console.log(...a);
const made = (what, n) => log(`  ${c.ok('created')}  ${what} ${n}`);
const kept = (what, n) => log(`  ${c.skip('exists ')}  ${what} ${n}`);

// Discord lowercases and hyphenates text/forum channel names but leaves voice
// names alone. Normalise both sides before comparing so re-runs match.
function slug(name, type) {
  if (type === ChannelType.GuildVoice || type === 'voice') return name;
  return name.toLowerCase().trim().replace(/\s+/g, '-');
}

const uniq = (arr) => [...new Set(arr)];

/**
 * Take a category's overwrites and return a channel-level copy that is
 * read-only for everyone except staff — while preserving whatever gate the
 * category already applied.
 */
function mergeReadonly(catOverwrites, mode, everyoneId, staffIds) {
  const RO = mode === 'readonly-threads' ? VISIBLE_READONLY_THREADS : VISIBLE_READONLY;
  const out = catOverwrites.map((o) => ({
    id: o.id,
    allow: uniq(o.allow || []),
    deny: uniq(o.deny || []),
  }));

  for (const o of out) {
    o.deny = uniq([...o.deny, ...RO.deny]);
    if (o.id === everyoneId) o.allow = uniq([...o.allow, ...RO.allow]);
    o.allow = o.allow.filter((p) => !o.deny.includes(p));
  }

  // Staff can always post in a locked channel.
  for (const id of staffIds) {
    const entry = out.find((o) => o.id === id) || (out.push({ id, allow: [], deny: [] }), out[out.length - 1]);
    entry.allow = uniq([
      ...entry.allow,
      P.ViewChannel, P.ReadMessageHistory, P.SendMessages,
      P.SendMessagesInThreads, P.CreatePublicThreads, P.ManageMessages,
    ]);
    entry.deny = entry.deny.filter((p) => !entry.allow.includes(p));
  }

  return out;
}

function fill(str) {
  return str
    .replace(/\{\{PROJECT\}\}/g, CONFIG.projectName)
    .replace(/\{\{REPO\}\}/g, CONFIG.repoUrl);
}

function preflight() {
  const problems = [];
  if (!CONFIG.token)   problems.push('DISCORD_TOKEN is not set.');
  if (!CONFIG.guildId) problems.push('GUILD_ID is not set.');
  if (CONFIG.guildId && !/^\d{17,20}$/.test(CONFIG.guildId)) {
    problems.push(`GUILD_ID "${CONFIG.guildId}" is not a Discord snowflake — it should be 17-20 digits.`);
  }
  // Copy length check, so a too-long message fails here and not halfway through.
  for (const [key, msgs] of Object.entries(COPY)) {
    msgs.forEach((m, i) => {
      const len = fill(m).length;
      if (len > 2000) problems.push(`content.js: ${key}[${i}] is ${len} chars — Discord's limit is 2000.`);
    });
  }
  // Every channel that has copy must exist in the tree, and vice versa is fine.
  const keys = new Set(TREE.flatMap((cat) => cat.channels.map((ch) => ch.key)));
  for (const key of Object.keys(COPY)) {
    if (!keys.has(key)) problems.push(`content.js has copy for "${key}" but no channel with that key exists.`);
  }
  if (problems.length) {
    log(c.err('\nCannot start:\n'));
    problems.forEach((p) => log(c.err('  • ' + p)));
    log('\nSee SETUP.md.\n');
    process.exit(1);
  }
}

// ── main ────────────────────────────────────────────────────────────────────
async function main() {
  preflight();

  const client = new Client({ intents: [GatewayIntentBits.Guilds] });
  await client.login(CONFIG.token);
  await lib.ready(client);

  const guild = await client.guilds.fetch(CONFIG.guildId).catch(() => null);
  if (!guild) {
    log(c.err(`\nThe bot is not in a server with id ${CONFIG.guildId}.`));
    log('Invite it first — the invite URL is in SETUP.md step 3.\n');
    process.exit(1);
  }
  await guild.roles.fetch();
  await guild.channels.fetch();

  const me = await guild.members.fetchMe();
  if (!me.permissions.has(P.Administrator)) {
    log(c.err('\nThe bot does not have Administrator in this server.'));
    log('Re-invite it with the URL in SETUP.md step 3, or give its role Administrator.\n');
    process.exit(1);
  }

  log(c.head(`\n${CONFIG.projectName} — building "${guild.name}"${DRY ? '  [DRY RUN]' : ''}\n`));

  // ---------------------------------------------------------------- roles
  log(c.head('Roles'));
  const R = {};
  for (const spec of ROLES) {
    const existing = guild.roles.cache.find((r) => r.name === spec.name);
    if (existing) { R[spec.key] = existing; kept('role', spec.name); continue; }
    if (DRY) { log(`  ${c.warn('would create')} role ${spec.name}`); continue; }
    R[spec.key] = await guild.roles.create({
      name: spec.name,
      color: spec.color,
      hoist: spec.hoist,
      mentionable: spec.mentionable,
      permissions: spec.perms,
      reason: 'server setup',
    });
    made('role', spec.name);
  }
  if (DRY) { log(c.warn('\nDry run — stopping before channels.\n')); process.exit(0); }

  const everyone = guild.roles.everyone;
  const staffIds = STAFF.map((k) => R[k]).filter(Boolean).map((r) => r.id);

  // ------------------------------------------------------------- community
  // Announcement and forum channels require the Community feature.
  let community = guild.features.includes('COMMUNITY');
  if (!community) {
    log(c.head('\nCommunity mode'));
    try {
      // Community mode needs a rules channel and a moderator-updates channel to
      // already exist. These two are scaffolding — deliberately named so they
      // cannot collide with a real channel, and deleted at the end of the run.
      const tmpRules = guild.channels.cache.find((ch) => ch.name === TEMP_RULES)
        || await guild.channels.create({ name: TEMP_RULES, type: ChannelType.GuildText, reason: 'community requirement' });
      const tmpUpdates = guild.channels.cache.find((ch) => ch.name === TEMP_UPDATES)
        || await guild.channels.create({ name: TEMP_UPDATES, type: ChannelType.GuildText, reason: 'community requirement' });
      await guild.edit({
        features: [...guild.features, 'COMMUNITY'],
        rulesChannel: tmpRules,
        publicUpdatesChannel: tmpUpdates,
        verificationLevel: GuildVerificationLevel.Low,
        explicitContentFilter: GuildExplicitContentFilter.AllMembers,
        reason: 'enable announcement + forum channels',
      });
      community = true;
      made('feature', 'COMMUNITY enabled');
    } catch (e) {
      log(`  ${c.warn('skipped')}  could not enable Community automatically (${e.message.slice(0, 90)})`);
      log(`  ${c.warn('       ')}  announcement/forum channels will be plain text channels.`);
      log(`  ${c.warn('       ')}  Turn it on in Server Settings → Enable Community, then re-run.`);
    }
  }

  const typeFor = (t) => {
    if (t === 'voice') return ChannelType.GuildVoice;
    if (t === 'announcement') return community ? ChannelType.GuildAnnouncement : ChannelType.GuildText;
    if (t === 'forum') return community ? ChannelType.GuildForum : ChannelType.GuildText;
    return ChannelType.GuildText;
  };

  // ------------------------------------------------------------- channels
  const CH = {};
  for (const cat of TREE) {
    log(c.head(`\n${cat.name}`));

    // Category-level overwrites carry the gate; channels inherit.
    const catOverwrites = [{ id: everyone.id, ...(cat.gate ? HIDDEN : { allow: [P.ViewChannel], deny: [] }) }];
    if (cat.gate) {
      const gateIds = cat.gate === '__staff__'
        ? staffIds
        : [R[cat.gate]?.id, ...staffIds].filter(Boolean);
      for (const id of new Set(gateIds)) {
        catOverwrites.push({ id, allow: [P.ViewChannel, P.ReadMessageHistory, P.SendMessages, P.SendMessagesInThreads, P.Connect, P.Speak] });
      }
    }

    let category = guild.channels.cache.find((ch) => ch.type === ChannelType.GuildCategory && ch.name === cat.name);
    if (category) {
      kept('category', cat.name);
      await category.permissionOverwrites.set(catOverwrites, 'server setup').catch(() => {});
    } else {
      category = await guild.channels.create({
        name: cat.name,
        type: ChannelType.GuildCategory,
        permissionOverwrites: catOverwrites,
        reason: 'server setup',
      });
      made('category', cat.name);
    }

    for (const spec of cat.channels) {
      const type = typeFor(spec.type);
      const wanted = slug(spec.name, type);
      let channel = guild.channels.cache.find(
        (ch) => ch.parentId === category.id && slug(ch.name, ch.type) === wanted
      ) || null;

      // A channel with its own overwrites does NOT inherit the category's, so a
      // read-only channel inside a gated category has to carry the gate itself.
      // Build from the category's overwrites and subtract, never from scratch.
      const overwrites = spec.mode ? mergeReadonly(catOverwrites, spec.mode, everyone.id, staffIds) : null;

      if (!channel) {
        const opts = { name: spec.name, type, parent: category.id, reason: 'server setup' };
        if (type !== ChannelType.GuildVoice && spec.topic) opts.topic = spec.topic;
        if (overwrites) opts.permissionOverwrites = overwrites;
        if (type === ChannelType.GuildForum) {
          opts.availableTags = BUG_TAGS.map((t) => ({ name: t.name, emoji: { name: t.emoji }, moderated: t.moderated }));
          opts.defaultForumLayout = ForumLayoutType.ListView;
          opts.defaultReactionEmoji = { name: '👀' };
        }
        channel = await guild.channels.create(opts);
        made(spec.type, `#${channel.name}`);
      } else {
        kept(spec.type, `#${channel.name}`);
        if (overwrites) {
          await channel.permissionOverwrites.set(overwrites, 'server setup').catch(() => {});
        } else if (cat.gate) {
          // Guarantee the gate on a pre-existing channel.
          await channel.lockPermissions().catch(() => {});
        }
      }
      CH[spec.key] = channel;
    }
  }

  // ---------------------------------------------------------------- copy
  log(c.head('\nPinned content'));
  for (const [key, messages] of Object.entries(COPY)) {
    const channel = CH[key];
    if (!channel) continue;
    if (channel.type === ChannelType.GuildForum) {
      const threads = await channel.threads.fetch().catch(() => null);
      const already = threads?.threads?.some((t) => t.name === 'READ THIS FIRST — how to file a bug');
      if (already) { kept('post', `#${channel.name}`); continue; }
      const post = await channel.threads.create({
        name: 'READ THIS FIRST — how to file a bug',
        message: { content: fill(messages[0]) },
        appliedTags: [],
      });
      await post.pin().catch(() => {});
      await post.setLocked(true).catch(() => {});
      made('post', `#${channel.name}`);
      continue;
    }
    if (await lib.pinnedCount(channel) > 0) { kept('pin', `#${channel.name}`); continue; }
    let first = true;
    for (const m of messages) {
      const sent = await channel.send(fill(m));
      if (first) { await sent.pin().catch(() => {}); first = false; }
    }
    made('pin', `#${channel.name}`);
  }

  // Record what the copy looked like, so `sync.js` can post only what changes
  // later instead of reposting all of it.
  {
    const state = lib.readState();
    state.pins = state.pins || {};
    for (const [key, messages] of Object.entries(COPY)) {
      if (!CH[key]) continue;
      state.pins[key] = { hash: lib.hash(messages.map(fill).join(' ')), at: new Date().toISOString() };
    }
    lib.writeState(state);
    log(`  ${c.ok('wrote  ')}  .sync-state.json (baseline for sync.js)`);
  }

  // -------------------------------------------------------------- automod
  log(c.head('\nAutoMod'));
  const alertChannel = CH['mod-log'];
  const rules = await guild.autoModerationRules.fetch().catch(() => null);
  const has = (name) => rules && rules.some((r) => r.name === name);

  const mkRule = async (name, body) => {
    if (has(name)) { kept('rule', name); return; }
    try {
      await guild.autoModerationRules.create({ name, enabled: true, reason: 'server setup', ...body });
      made('rule', name);
    } catch (e) {
      log(`  ${c.warn('skipped')}  ${name} — ${e.message.slice(0, 100)}`);
    }
  };

  const alertAction = alertChannel
    ? [{ type: AutoModerationActionType.SendAlertMessage, metadata: { channel: alertChannel.id } }]
    : [];

  await mkRule('Rule 1 — no ROM sharing', {
    eventType: AutoModerationRuleEventType.MessageSend,
    triggerType: AutoModerationRuleTriggerType.Keyword,
    triggerMetadata: { keywordFilter: ROM_KEYWORDS, regexPatterns: ROM_REGEX },
    actions: [
      {
        type: AutoModerationActionType.BlockMessage,
        metadata: {
          customMessage:
            'Blocked by rule 1. Never share, link, or request a ROM here — the ' +
            'legal footing of this project depends on it. Talking about the data ' +
            'is fine; the file is the line.',
        },
      },
      ...alertAction,
    ],
    exemptRoles: staffIds,
  });

  await mkRule('No slurs', {
    eventType: AutoModerationRuleEventType.MessageSend,
    triggerType: AutoModerationRuleTriggerType.KeywordPreset,
    triggerMetadata: { presets: [AutoModerationRuleKeywordPresetType.Slurs] },
    actions: [{ type: AutoModerationActionType.BlockMessage }, ...alertAction],
    exemptRoles: staffIds,
  });

  await mkRule('Mention spam', {
    eventType: AutoModerationRuleEventType.MessageSend,
    triggerType: AutoModerationRuleTriggerType.MentionSpam,
    triggerMetadata: { mentionTotalLimit: 6 },
    actions: [{ type: AutoModerationActionType.BlockMessage }, ...alertAction],
    exemptRoles: staffIds,
  });

  await mkRule('Spam', {
    eventType: AutoModerationRuleEventType.MessageSend,
    triggerType: AutoModerationRuleTriggerType.Spam,
    actions: [{ type: AutoModerationActionType.BlockMessage }, ...alertAction],
    exemptRoles: staffIds,
  });

  // ------------------------------------------------------------ finishing
  log(c.head('\nServer settings'));
  try {
    await guild.edit({
      systemChannel: CH.general ?? null,
      rulesChannel: CH.rules ?? null,
      publicUpdatesChannel: CH['mod-log'] ?? null,
      reason: 'server setup',
    });
    made('settings', 'rules / system / mod-update channels wired');
  } catch (e) {
    log(`  ${c.warn('skipped')}  ${e.message.slice(0, 100)}`);
  }

  // Remove the scaffolding channels community mode required. These can only be
  // ones this script created — the names are reserved for exactly that.
  for (const name of [TEMP_RULES, TEMP_UPDATES]) {
    const junk = guild.channels.cache.find((ch) => ch.name === name);
    if (junk) await junk.delete('scaffolding from community setup').catch(() => {});
  }

  log(c.head('\nDone.\n'));
  log('Next, by hand (two minutes, all in Server Settings):');
  log('  1. Roles → drag Operator and Archivist above the bot\'s own role.');
  log('  2. Give yourself Operator.');
  log('  3. Onboarding → require members to accept #rules before they can talk.');
  log('  4. #welcome → add reactions for the three ping roles, or use a role bot.');
  log('  5. #commits → Integrations → Webhooks → point GitHub at it.');
  log('');

  await client.destroy();
  process.exit(0);
}

// Exported so the checks in verify.js can exercise them without a live gateway.
module.exports = { mergeReadonly, slug, fill, ROLES, TREE, BUG_TAGS, ROM_KEYWORDS, ROM_REGEX, CONFIG };

if (require.main !== module) return;

main().catch((e) => {
  console.error(c.err('\nFailed: ' + (e?.message || e)));
  if (e?.code === 50013) console.error('That is a permissions error — the bot needs Administrator, and its role must be near the top of the role list.');
  if (e?.code === 'TokenInvalid') console.error('The token in DISCORD_TOKEN is not valid. Reset it in the Developer Portal and copy the whole thing.');
  console.error('');
  process.exit(1);
});
