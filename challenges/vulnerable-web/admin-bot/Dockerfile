# Challenge 6 admin bot: headless Chromium (preinstalled in the Playwright image) that
# reviews the board on a timer so stored XSS payloads fire in the admin's context.
FROM mcr.microsoft.com/playwright:v1.49.0-jammy
WORKDIR /bot
COPY package.json ./
RUN npm install
COPY bot.js ./
CMD ["node", "bot.js"]
