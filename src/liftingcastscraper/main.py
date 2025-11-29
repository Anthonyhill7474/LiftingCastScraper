#!/usr/bin/env python3 
# ^ is not a comment, it's a shebang line for Unix-like OS to find the Python 3 interpreter
"""
Main entry point for the LiftingCast → OpenPowerlifting scraper pipeline.
"""
import os
import asyncio
import logging

import aiohttp

from .scraper.selenium_scraper import scrape_liftingcast_roster, get_driver
from .scraper.utils import save_html_report, slugify, clean_lifter_name
from .opl_ipf.lookup import try_fetch_openipf
from .reports.html_report import generate_html_report

logger = logging.getLogger(__name__)
logger.setLevel(logging.INFO)

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUTPUT_DIR = os.path.join(PROJECT_ROOT, "output") # output directory path which is a subdirectory named "output" in the base directory

async def run_pipeline(meet_url: str) -> None:
    logger.info("\n🔍 Fetching roster from: {%s}", meet_url)

    roster = await asyncio.to_thread(scrape_liftingcast_roster, meet_url)
    logger.info(" → Found {%d} athletes\n", len(roster))

    # Keeping this here for learning async, this iterates over the roster and fetches data sequentially, not properly utilising async. Nothing is run concurrently, and this basically does for each lifter: await try_fetch_openipf(), so it waits for each api fetch
    # people = []

    # for entry in roster:
    #     raw_label, liftingcast_url = entry
    #     lifter_name = clean_lifter_name(raw_label)

    #     print(f"🔎 Checking OpenIPF for: {lifter_name}")

    #     ipf_data = await try_fetch_openipf(lifter_name)

    #     if ipf_data:
    #         print(f" ✓ Found OpenIPF profile: {ipf_data['profile_url']}")
    #         opl_profile = ipf_data["profile_url"]
    #         summary = ipf_data["meet_history"]
    #     else:
    #         print(" ✗ No OpenIPF match found")
    #         opl_profile = None
    #         summary = None

    #     people.append(
    #         {
    #             "name": lifter_name,
    #             "liftingcast_href": liftingcast_url,
    #             "opl_profile": opl_profile,
    #             "opl_summary": summary,
    #         }
    #     )

    # Reuse a single shared aiohttp session
    async with aiohttp.ClientSession() as session:

        tasks = [
            try_fetch_openipf(clean_lifter_name(raw_name), session)
            for raw_name, _ in roster
        ]

        results = await asyncio.gather(*tasks)

    # Build final list
    people = []
    for (raw_name, liftingcast_url), ipf_data in zip(roster, results):
        name = clean_lifter_name(raw_name)

        if ipf_data:
            people.append({
                "name": name,
                "liftingcast_href": liftingcast_url,
                "opl_profile": ipf_data["profile_url"],
                "opl_summary": ipf_data["meet_history"],
            })
        else:
            people.append({
                "name": name,
                "liftingcast_href": liftingcast_url,
                "opl_profile": None,
                "opl_summary": None,
            })

    logger.info("📄 Building HTML report…")
    html = generate_html_report(people)

    os.makedirs(OUTPUT_DIR, exist_ok=True)
    filename = os.path.join(OUTPUT_DIR, f"report_{slugify(meet_url)}.html")
    save_html_report(html, filename)

    logger.info("✅ Report saved → %s", filename)


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="%(levelname)s:%(name)s:%(message)s",
    )

    example_url = "https://liftingcast.com/meets/mfnfcu3cri6q/roster"
    asyncio.run(run_pipeline(example_url)) # starts an event loop to run the async function
