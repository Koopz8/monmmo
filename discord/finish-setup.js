#!/usr/bin/env node
/**
 * The manual steps, done through the bot instead of by hand.
 *
 *   node finish-setup.js --dry-run
 *   node finish-setup.js
 *
 * 1. Moves Operator / Archivist / Cartographer / Field Tester as high as the
 *    bot is allowed to put them.
 * 2. Gives the server owner (you) the Operator role.
 * 3. Turns on Onboarding, with the rules as a required stop.
 * 4. Builds the notification-role picker into Onboarding, so the three ping
 *    roles are self-serve with no reaction-role bot at all.
 * 5. Creates the GitHub webhook in #commits and prints the URL.
 *
 * Safe to re-run. Everything here is idempotent.
 */
'use strict';

const { GuildOnboardingPromptType, GuildOnboardingMode, PermissionFlagsBits: P } = require('discord.js');
const lib = require('./lib.js');
const { c } = lib;

lib.loadEnv();
const DRY = process.argv.includes('--dry-run');

// Discord wants an id on every prompt and option, even brand new ones.
let seq = 0n;
const newId = () => (BigInt(Date.now()) * 1000n + seq++).toString();

const ROLE_ORDER = ['Operator', 'Archivist', 'Cartographer', 'Field Tester'];

// Discord requires a set of default channels, at least five of which @everyone
// can actually talk in, or it refuses to enable onboarding.
const DEFAULT_CHANNELS = [
  'welcome', 'rules', 'announcements', 'general', 'introductions', 'screenshots',
  'off-topic', 'suggestions', 'setup-help', 'faq', 'known-issues', 'devlog',
];

// Kept as plain names so verify.js can assert every one of them exists. A typo
// here would otherwise just silently drop an option.
const PROMPTS = [
  {
    title: 'What do you want to be notified about?',
    singleSelect: false,
    options: [
      { title: 'Devlog posts', description: 'Written updates when something gets built.', emoji: '📓', roles: ['devlog pings'], channels: ['devlog', 'milestones'] },
      // These two point at PUBLIC channels on purpose. #build-drops and
      // #playtest-coordination are behind the Field Tester gate, so linking
      // them here would hand a brand-new member a door they cannot open.
      { title: 'New builds', description: 'Only when a build actually drops.', emoji: '📦', roles: ['build pings'], channels: ['changelog'] },
      { title: 'Playtests', description: 'When a session is being organised.', emoji: '🎮', roles: ['playtest pings'], channels: ['announcements'] },
    ],
  },
  {
    title: 'What brings you here?',
    singleSelect: true,
    options: [
      { title: 'I want to play it', description: 'Point me at the state of things.', emoji: '🌿', roles: [], channels: ['faq', 'known-issues', 'setup-help', 'announcements'] },
      { title: 'I am here for the engineering', description: 'Offsets, netcode, damage formulas.', emoji: '🔧', roles: [], channels: ['engine-and-netcode', 'battle-engine', 'data-and-extraction'] },
      { title: 'Just watching', description: 'Keep me out of the weeds.', emoji: '👀', roles: [], channels: ['devlog', 'announcements'] },
    ],
  },
];

async function main() {
  const { client, guild } = await lib.connect();
  const me = await guild.members.fetchMe();

  const ch = (name) => guild.channels.cache.find((x) => x.name.toLowerCase() === name);
  const role = (name) => guild.roles.cache.find((r) => r.name === name);

  console.log(c.head(`\nFinishing "${guild.name}"${DRY ? '  [DRY RUN]' : ''}\n`));

  // ── 1. role hierarchy ─────────────────────────────────────────────────────
  console.log(c.head('Role order'));
  const ceiling = me.roles.highest.position;   // the bot cannot move anything to or above this
  const targets = ROLE_ORDER.map(role).filter(Boolean);

  if (ceiling <= targets.length) {
    console.log(`  ${c.warn('cannot')}   the bot's own role sits at position ${ceiling} — too low to lift anything above it.`);
    console.log(`  ${c.warn('      ')}   Server Settings → Roles, drag "${me.roles.highest.name}" to the top, then re-run.`);
  } else if (targets.every((r, i) => r.position === ceiling - 1 - i)) {
    console.log(`  ${c.skip('already')}  ${targets.map((r) => r.name).join(' > ')}`);
  } else if (!DRY) {
    try {
      await guild.roles.setPositions(targets.map((r, i) => ({ role: r.id, position: ceiling - 1 - i })));
      console.log(`  ${c.ok('moved  ')}  ${targets.map((r) => r.name).join(' > ')}  (just under ${me.roles.highest.name})`);
    } catch (e) {
      console.log(`  ${c.warn('failed ')}  ${e.message.slice(0, 110)}`);
    }
  } else {
    console.log(`  ${c.warn('would move')} ${targets.map((r) => r.name).join(' > ')} to positions ${ceiling - 1}..${ceiling - targets.length}`);
  }

  // ── 2. give the owner Operator ────────────────────────────────────────────
  console.log(c.head('\nOperator'));
  const operator = role('Operator');
  const owner = await guild.fetchOwner().catch(() => null);
  if (!operator || !owner) {
    console.log(`  ${c.warn('skipped')}  could not find the Operator role or the server owner.`);
  } else if (owner.roles.cache.has(operator.id)) {
    console.log(`  ${c.skip('already')}  ${owner.user.tag} has Operator`);
  } else if (!DRY) {
    try {
      await owner.roles.add(operator, 'server setup');
      console.log(`  ${c.ok('granted')}  Operator → ${owner.user.tag}`);
    } catch (e) {
      console.log(`  ${c.warn('failed ')}  ${e.message.slice(0, 110)} (the bot's role must sit above Operator)`);
    }
  } else {
    console.log(`  ${c.warn('would grant')} Operator → ${owner.user.tag}`);
  }

  // ── 3 + 4. onboarding, including the ping-role picker ─────────────────────
  console.log(c.head('\nOnboarding'));

  const defaults = DEFAULT_CHANNELS.map(ch).filter(Boolean);

  const prompts = PROMPTS.map((p) => ({
    id: newId(),
    title: p.title,
    type: GuildOnboardingPromptType.MultipleChoice,
    singleSelect: p.singleSelect,
    required: false,
    inOnboarding: true,
    options: p.options.map((o) => ({
      id: newId(),
      title: o.title,
      description: o.description,
      emoji: { name: o.emoji },
      roles: o.roles.map(role).filter(Boolean).map((r) => r.id),
      channels: o.channels.map(ch).filter(Boolean).map((x) => x.id),
    })),
  }));

  if (DRY) {
    console.log(`  ${c.warn('would enable')} onboarding with ${defaults.length} default channels and ${prompts.length} prompts`);
    for (const p of prompts) console.log(`    · ${p.title} — ${p.options.map((o) => o.title).join(', ')}`);
  } else {
    try {
      await guild.editOnboarding({
        enabled: true,
        mode: GuildOnboardingMode.OnboardingAdvanced,
        defaultChannels: defaults.map((x) => x.id),
        prompts,
        reason: 'server setup',
      });
      console.log(`  ${c.ok('enabled')}  onboarding — ${defaults.length} default channels, ${prompts.length} prompts`);
      console.log(`  ${c.ok('       ')}  the three ping roles are now self-serve; no reaction-role bot needed`);
    } catch (e) {
      console.log(`  ${c.warn('failed ')}  ${e.message.slice(0, 160)}`);
      console.log(`  ${c.warn('       ')}  Server Settings → Onboarding, and enable it by hand.`);
    }
  }

  // Rules acceptance is a separate, older feature from onboarding. Try it, but
  // do not pretend it is guaranteed — the endpoint is not officially documented.
  console.log(c.head('\nRules acceptance'));
  const rules = ch('rules');
  if (!rules) {
    console.log(`  ${c.warn('skipped')}  no #rules channel`);
  } else if (!DRY) {
    try {
      await client.rest.patch(`/guilds/${guild.id}/member-verification`, {
        body: {
          enabled: true,
          description: 'This server hosts a fan project that distributes nothing. Read rule 1 before you post.',
          form_fields: [{
            field_type: 'TERMS',
            label: 'Read and agree to the rules',
            required: true,
            values: [
              'I will never share, link, or request a ROM here — instant permanent ban.',
              'I will not post extracted assets or piracy links.',
              'I will be decent to people.',
            ],
          }],
        },
      });
      console.log(`  ${c.ok('enabled')}  new members must accept the rules before they can talk`);
    } catch (e) {
      console.log(`  ${c.warn('skipped')}  ${String(e.message).slice(0, 120)}`);
      console.log(`  ${c.warn('       ')}  Onboarding above already gates entry; this legacy screen is optional.`);
    }
  }

  // ── 5. the GitHub webhook ─────────────────────────────────────────────────
  console.log(c.head('\nGitHub webhook'));
  const commits = ch('commits');
  if (!commits) {
    console.log(`  ${c.warn('skipped')}  no #commits channel`);
  } else {
    const existing = await commits.fetchWebhooks().catch(() => null);
    const found = existing?.find((w) => w.name === 'GitHub');
    if (found) {
      console.log(`  ${c.skip('already')}  a "GitHub" webhook exists on #commits`);
      console.log(`  ${c.skip('       ')}  Channel Settings → Integrations → Webhooks to copy its URL`);
    } else if (!DRY) {
      try {
        const hook = await commits.createWebhook({ name: 'GitHub', reason: 'server setup' });
        console.log(`  ${c.ok('created')}  paste this into GitHub → Settings → Webhooks → Add webhook`);
        console.log(`\n    Payload URL:  ${hook.url}/github`);
        console.log(`    Content type: application/json\n`);
        console.log(c.warn('    Treat that URL like a password — anyone holding it can post as the bot.'));
      } catch (e) {
        console.log(`  ${c.warn('failed ')}  ${e.message.slice(0, 110)}`);
      }
    } else {
      console.log(`  ${c.warn('would create')} a "GitHub" webhook on #commits`);
    }
  }

  // ── what is left ──────────────────────────────────────────────────────────
  console.log(c.head('\nStill yours to do'));
  console.log('  · Paste the webhook URL into GitHub (nobody can do that but you).');
  console.log('  · Open the server in an incognito window and confirm THE TESTING');
  console.log('    GROUNDS and THE BACK ROOM are invisible without the roles.');
  console.log('');

  await client.destroy();
  process.exit(0);
}

module.exports = { ROLE_ORDER, DEFAULT_CHANNELS, PROMPTS };
if (require.main !== module) return;

main().catch((e) => {
  console.error(c.err('\n' + (e?.message || e)) + '\n');
  process.exit(1);
});
