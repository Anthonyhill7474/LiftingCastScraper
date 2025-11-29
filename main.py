#!/usr/bin/env python3 
# ^ is not a comment, it's a shebang line for Unix-like OS to find the Python 3 interpreter
"""
Main entry point for the LiftingCast → OpenPowerlifting scraper pipeline.
"""
import os
import asyncio
import logging

from scraper.selenium_scraper import (
    scrape_liftingcast_roster,
    get_driver,
)

from scraper.utils import (
    save_html_report,
    slugify,
    clean_lifter_name,
)

from opl_ipf.lookup import(
    try_fetch_openipf,
)

from reports.html_report import generate_html_report

logger = logging.getLogger(__name__)
logger.setLevel(logging.INFO)

BASE_DIR = os.path.dirname(os.path.abspath(__file__)) # retrieves absolute pathway to current file
OUTPUT_DIR = os.path.join(BASE_DIR, "output") # output directory path which is a subdirectory named "output" in the base directory

async def run_pipeline(meet_url: str) -> None:
    logger.info("\n🔍 Fetching roster from: {%s}", meet_url)
    roster = scrape_liftingcast_roster(meet_url) # returns a list of tuples [('105 - Anthony Hill', 'https://www.openipf.org/u/anthonyhill'), ...]
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

    tasks = [] # stores coroutine objects for concurrent execution
    names = []
    urls = []

    for raw_label, liftingcast_url in roster:
        lifter_names = clean_lifter_name(raw_label)
        names.append(lifter_names)
        urls.append(liftingcast_url)
        tasks.append(try_fetch_openipf(lifter_names)) # creates coroutine object for each lifter, this does not run the function, only if it is awaited or run in an event loop

    results = await asyncio.gather(*tasks) # runs all the coroutines concurrently, returns list of results in same order as tasks
    # asyncio.gather(*tasks) is the same as asyncio.gather(task1, task2, task3, ...), .gather()requires multiple arguments as input, not a list
    
    people = []
    for lifter_name, liftingcast_url, ipf_data in zip(names, urls, results):
        if ipf_data:
            people.append({
                "name": lifter_name,
                "liftingcast_href": liftingcast_url,
                "opl_profile": ipf_data["profile_url"],
                "opl_summary": ipf_data["meet_history"],
            })
        else:
            people.append({
                "name": lifter_name,
                "liftingcast_href": liftingcast_url,
                "opl_profile": None,
                "opl_summary": None,
            })

    logger.info("\n📄 Building HTML report…")
    html = generate_html_report(people)

    filename = os.path.join(OUTPUT_DIR, f"report_{slugify(meet_url)}.html")
    save_html_report(html, filename)

    logger.info("✅ Report saved → {%s}", filename)


if __name__ == "__main__":
    example_url = "https://liftingcast.com/meets/mfnfcu3cri6q/roster"
    asyncio.run(run_pipeline(example_url)) # starts an event loop to run the async function
