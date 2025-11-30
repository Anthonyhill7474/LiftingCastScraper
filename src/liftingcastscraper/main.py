#!/usr/bin/env python3
"""
Manual testing entry point.
Run in VSCode terminal: python -m liftingcastscraper.main
"""

import os
import asyncio
import logging

from .pipeline import build_people
from .scraper.utils import save_html_report, slugify
from .reports.html_report import generate_html_report

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)

# /src/liftingcastscraper/ -> go up two levels into project root, then an output folder
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.join(BASE_DIR, "..", "..", "output")
os.makedirs(OUTPUT_DIR, exist_ok=True)


async def run_pipeline(meet_url: str) -> None:
    logger.info("Fetching roster from: %s", meet_url)
    people = await build_people(meet_url)

    logger.info("Building HTML report…")
    html = generate_html_report(people)

    filename = os.path.join(OUTPUT_DIR, f"report_{slugify(meet_url)}.html")
    save_html_report(html, filename)

    logger.info("Report saved → %s", filename)


if __name__ == "__main__":
    example_url = "https://liftingcast.com/meets/mfnfcu3cri6q/roster"
    asyncio.run(run_pipeline(example_url))
