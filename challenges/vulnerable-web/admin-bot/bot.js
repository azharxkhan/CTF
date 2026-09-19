// CHALLENGE 6 — the "admin" bot.
//
// Simulates an admin who periodically reviews new comments in a real browser. It first
// establishes an admin session (a script-readable cookie holding the flag), then loads the
// board so any stored comment — including an attacker's XSS payload — executes in that
// authenticated context. The payload can then read the cookie and exfiltrate it.
//
// Deploy: `npm i playwright && npx playwright install chromium`, then `node bot.js`.
// Runs on a loop; on the CTF network it can't reach the internet, only the challenge origin.

import { chromium } from "playwright";

const BASE = process.env.C6_BASE || "http://localhost:5006";
const INTERVAL_MS = Number(process.env.C6_INTERVAL_MS || 15000);

async function reviewOnce(browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  // 1) Establish the admin session (sets the sensitive, script-readable cookie).
  await page.goto(`${BASE}/api/admin/session`);
  // 2) Review the board — stored comments render here; XSS payloads fire in this context.
  await page.goto(`${BASE}/board/`, { waitUntil: "networkidle" });
  await page.waitForTimeout(2000); // give onerror/handlers time to run
  await context.close();
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  console.log(`admin bot reviewing ${BASE}/board every ${INTERVAL_MS}ms`);
  for (;;) {
    try { await reviewOnce(browser); }
    catch (e) { console.error("review error:", e.message); }
    await new Promise((r) => setTimeout(r, INTERVAL_MS));
  }
}

main();
