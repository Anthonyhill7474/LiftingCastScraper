# src/liftingcastscraper/pipeline.py
import asyncio
from typing import List, Dict

import aiohttp

# from .scraper.selenium_scraper import scrape_liftingcast_roster # Old selenium scraper
from .scraper.playwright_scraper import scrape_liftingcast_roster
from .scraper.utils import clean_lifter_name, normalize_liftingcast_url, log_mem
from .opl_ipf.lookup import try_fetch_openipf


async def build_people(meet_url: str) -> List[Dict]:
    """Return the `people` structure for a meet URL."""
    log_mem("Start pipeline")
    meet_url = normalize_liftingcast_url(meet_url)
    
    # 1. Scrape the roster synchronously via playwright
    roster = await scrape_liftingcast_roster(meet_url)
    log_mem("After roster scrape")

    names: List[str] = []
    urls: List[str] = []
    tasks: List[asyncio.Future] = []

    # 2. Reuse ONE aiohttp session for all lifters
    async with aiohttp.ClientSession() as session:
        for raw_label, liftingcast_url in roster:
            lifter_name = clean_lifter_name(raw_label)
            names.append(lifter_name)
            urls.append(liftingcast_url)

            # NOTE: pass the session in here 👇
            tasks.append(try_fetch_openipf(lifter_name, session=session))

        # 3. Run all OpenIPF lookups concurrently
        results = await asyncio.gather(*tasks)
    log_mem("After OpenIPF lookups")
    # 4. Stitch everything back together in order
    people: List[Dict] = []
    for lifter_name, liftingcast_url, ipf_data in zip(names, urls, results):
        if ipf_data:
            people.append(
                {
                    "name": lifter_name,
                    "liftingcast_href": liftingcast_url,
                    "opl_profile": ipf_data["profile_url"],
                    "opl_summary": ipf_data["meet_history"],
                }
            )
        else:
            people.append(
                {
                    "name": lifter_name,
                    "liftingcast_href": liftingcast_url,
                    "opl_profile": None,
                    "opl_summary": None,
                }
            )
    log_mem("End pipeline")
    return people
