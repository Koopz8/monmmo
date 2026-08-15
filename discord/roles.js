#!/usr/bin/env node
/**
 * Print the role hierarchy as Discord actually has it, and say plainly whether
 * it is correct. Read-only — changes nothing.
 *
 *   node roles.js
 *
 * "Missing Permissions" when reordering means a role sits at or above the bot's
 * own. That is sometimes a problem and sometimes already the goal, and the only
 * way to tell them apart is to look.
 */
'use strict';

const lib = require('./lib.js');
const { c } = lib;
lib.loadEnv();

const WANT = ['Operator', 'Archivist', 'Cartographer', 'Field Tester'];

async function main() {
  const { client, guild } = await lib.connect();
  const me = await guild.members.fetchMe();
  const botTop = me.roles.highest;

  const roles = [...guild.roles.cache.values()]
    .filter((r) => r.name !== '@everyone')
    .sort((a, b) => b.position - a.position);

  console.log(c.head(`\n"${guild.name}" — roles, highest first\n`));
  for (const r of roles) {
    const marks = [];
    if (r.id === botTop.id) marks.push(c.warn('← the bot'));
    if (WANT.includes(r.name)) marks.push(c.ok('staff/tester'));
    if (r.permissions.has('Administrator')) marks.push(c.err('ADMIN'));
    console.log(`  ${String(r.position).padStart(3)}  ${r.name.padEnd(20)} ${marks.join('  ')}`);
  }

  console.log(c.head('\nVerdict\n'));

  const found = WANT.map((n) => guild.roles.cache.find((r) => r.name === n)).filter(Boolean);
  const above = found.filter((r) => r.position > botTop.position);
  const below = found.filter((r) => r.position < botTop.position);

  // The thing that actually matters: can staff moderate everyone else?
  const memberRoles = roles.filter((r) => !WANT.includes(r.name) && r.id !== botTop.id && !r.managed);
  const topMember = memberRoles[0];
  const archivist = guild.roles.cache.find((r) => r.name === 'Archivist');

  if (archivist && topMember && archivist.position < topMember.position) {
    console.log(c.err(`  PROBLEM: Archivist (${archivist.position}) is below ${topMember.name} (${topMember.position}).`));
    console.log(`  Moderators cannot action anyone holding ${topMember.name}.`);
    console.log(`  Fix: Server Settings → Roles, drag Archivist above ${topMember.name}.\n`);
  } else {
    console.log(c.ok('  Staff outrank every member role. Moderation will work.\n'));
  }

  if (above.length) {
    console.log(`  ${above.map((r) => r.name).join(', ')} sit ABOVE the bot (${botTop.position}).`);
    console.log(`  That is why the reorder reported Missing Permissions, and it is fine —`);
    console.log(`  those roles are already as high as you wanted them.\n`);
  }
  if (below.length && above.length) {
    console.log(`  ${below.map((r) => r.name).join(', ')} sit below the bot and could still be lifted`);
    console.log(`  by dragging them in Server Settings → Roles.\n`);
  }

  await client.destroy();
  process.exit(0);
}

main().catch((e) => {
  console.error(c.err('\n' + (e?.message || e)) + '\n');
  process.exit(1);
});
