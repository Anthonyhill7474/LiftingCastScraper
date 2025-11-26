"""Playwright async scraper for LiftingCast roster pages.


A synchronous wrapper is provided for convenience.
"""


import asyncio
from typing import List, Tuple
import logging
from playwright.async_api import async_playwright, TimeoutError as PWTimeoutError
from .utils import lifter_link_selector


logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)




async def scrape_with_playwright(url: str, headless: bool = True, timeout: int = 15000) -> List[Tuple[str, str]]:
    selector = lifter_link_selector()
    async with async_playwright() as pw:
        browser = await pw.chromium.launch(headless=headless)
        page = await browser.new_page()
        logger.info("Loading %s", url)
        await page.goto(url, wait_until="domcontentloaded")
        try:
            await page.wait_for_selector(selector, timeout=timeout)
        except PWTimeoutError:
            logger.warning("Timeout waiting for lifter selector")
        els = await page.query_selector_all(selector)
        results = []
        for el in els:
            text = (await el.inner_text()).strip()
            href = await el.get_attribute('href')
            if text:
                results.append((text, href))
        await browser.close()
        return results




def scrape_playwright_sync(url: str, headless: bool = True, timeout: int = 15000) -> List[Tuple[str, str]]:
    return asyncio.run(scrape_with_playwright(url, headless=headless, timeout=timeout))