# src/liftingcastscraper/scraper/playwright_scraper.py

from typing import List, Tuple
import logging

from playwright.async_api import async_playwright

from .utils import lifter_link_selector

logger = logging.getLogger(__name__)


async def scrape_liftingcast_roster(url: str, timeout_ms: int = 30000):
    logger.info(f"Loading {url}")

    async with async_playwright() as p:
        browser = await p.chromium.launch(headless=True)
        page = await browser.new_page()

        # LOAD PAGE — but don’t wait for network idle (it will never happen)
        await page.goto(url, wait_until="domcontentloaded", timeout=timeout_ms)

        # WAIT FOR LIFTERS
        await page.wait_for_selector('a[href*="/lifter/"]', timeout=timeout_ms)

        elements = await page.query_selector_all('a[href*="/lifter/"]')

        results = []
        for el in elements:
            name = (await el.inner_text()).strip()
            href = await el.get_attribute("href")
            if name and href:
                results.append((name, href))

        await browser.close()
        return results

