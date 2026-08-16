#!/usr/bin/env node
/**
 * Post a markdown file (or piped text) to a channel.
 *
 *   node post.js devlog posts/2026-09-01-flags.md
 *   node post.js announcements posts/alpha-wave-2.md --crosspost
 *   node post.js build-drops notes.md --ping build --thread "v0.9 feedback"
 *   echo "quick note" | node post.js general -
 *
 * Front matter in the file wins unless a flag overrides it:
 *
 *   ---
 *   channel: milestones
 *   title: Milestone 88 — what a flag costs
 *   ping: devlog
 *   thread: true
 *   pin: false
 *   crosspost: false
 *   ---
 *
 * `--once <id>` records the post under that id and refuses to send it twice.
 * That is what makes the GitHub workflows safe to re-run.
 */
'use strict';

const fs = require('fs');
const path = require('path');
const lib = require('./lib.js');
const { c } = lib;

lib.loadEnv();

const argv = process.argv.slice(2);
const flag = (name) => {
  const i = argv.indexOf(`--${name}`);
  if (i === -1) return undefined;
  const next = argv[i + 1];
  return next && !next.startsWith('--') ? next : true;
};
const positional = argv.filter((a, i) => {
  if (a.startsWith('--')) return false;
  const prev = argv[i - 1];
  return !(prev && prev.startsWith('--') && flag(prev.slice(2)) === a);
});

async function main() {
  let [channelKey, file] = positional;

  let raw;
  if (file === '-' || (!file && !process.stdin.isTTY)) {
    raw = fs.readFileSync(0, 'utf8');
  } else if (file) {
    if (!fs.existsSync(file)) throw new Error(`No such file: ${file}`);
    raw = fs.readFileSync(file, 'utf8');
  } else {
    console.log(`
Usage: node post.js <channel> <file.md> [options]

Options:
  --ping <devlog|build|playtest>   mention an opt-in role
  --thread "<title>"               open a discussion thread on the post
  --pin                            pin it
  --crosspost                      publish to servers following #announcements
  --once <id>                      never send this id twice
  --dry-run                        print what would be sent

Channels: use the key or the channel name, e.g. devlog, milestones,
build-drops, announcements, changelog, general.
`);
    process.exit(1);
  }

  const { meta, body } = lib.frontmatter(raw);
  channelKey = channelKey || meta.channel;

  // A markdown file in posts/ with no channel is documentation, not a post.
  // Skip it rather than failing the workflow — but only when no channel was
  // asked for on the command line, so a genuine typo still errors.
  if (!channelKey && !positional[0]) {
    console.log(c.skip(`skipped ${file || 'input'} — no channel in its front matter, so it is not a post`));
    process.exit(0);
  }
  if (!channelKey) throw new Error('No channel given, and none in the file front matter.');

  const title = flag('title') || meta.title
    || (file && file !== '-' ? path.basename(file, '.md').replace(/^\d{4}-\d{2}-\d{2}-/, '').replace(/-/g, ' ') : null);

  const ping = flag('ping') ?? meta.ping;
  const wantThread = flag('thread') ?? meta.thread;
  const pin = Boolean(flag('pin') ?? meta.pin);
  const crosspost = Boolean(flag('crosspost') ?? meta.crosspost);
  const onceId = flag('once');
  const dry = argv.includes('--dry-run');

  // Idempotency: the workflows re-run on every push, and a re-run must not
  // repost. The id is recorded only after Discord confirms the send.
  const state = lib.readState();
  state.posted = state.posted || {};
  if (onceId && state.posted[onceId]) {
    console.log(c.skip(`already posted "${onceId}" (${state.posted[onceId].at}) — nothing to do`));
    process.exit(0);
  }

  let text = body;
  if (title && !body.startsWith('#')) text = `## ${title}\n\n${body}`;

  if (dry) {
    console.log(c.head(`\n→ #${channelKey}${ping ? `  @${ping} pings` : ''}${pin ? '  [pin]' : ''}${crosspost ? '  [crosspost]' : ''}`));
    lib.chunk(text).forEach((p, i) => console.log(c.skip(`\n--- message ${i + 1} (${p.length} chars) ---\n`) + p));
    console.log('');
    process.exit(0);
  }

  const { client, guild } = await lib.connect();
  const channel = lib.resolveChannel(guild, channelKey);
  const role = ping ? lib.resolvePing(guild, ping) : null;

  const content = role ? `${text}\n\n<@&${role.id}>` : text;

  const threadTitle = wantThread === true ? (title || 'Discussion') : (wantThread || null);
  const sent = await lib.send(channel, content, {
    pin,
    crosspost,
    thread: threadTitle,
    pingRoleId: role?.id,
  });

  if (onceId) {
    state.posted[onceId] = { at: new Date().toISOString(), channel: channel.name, messageId: sent[0].id };
    lib.writeState(state);
  }

  console.log(c.ok(`posted`) + ` ${sent.length} message(s) to #${channel.name}${role ? ` (pinged ${role.name})` : ''}${threadTitle ? ` + thread` : ''}`);
  await client.destroy();
  process.exit(0);
}

main().catch((e) => {
  console.error(c.err('\n' + (e?.message || e)) + '\n');
  process.exit(1);
});
