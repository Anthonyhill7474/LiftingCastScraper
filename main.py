#!/usr/bin/env python3
"""
Main entry point for the LiftingCast → OpenPowerlifting scraper pipeline.
"""

from scraper.selenium_scraper import (
    scrape_liftingcast_roster,
    search_opl_for_name,
    scrape_opl_profile,
    build_driver,
)

from scraper.utils import slugify
from reports.html_report import build_html_report, save_html_report


def run_pipeline(meet_url: str):
    print(f"\n🔍 Fetching roster from: {meet_url}")
    names = scrape_liftingcast_roster(meet_url)
    print(f" → Found {len(names)} athletes\n")

    driver = build_driver(headless=True)
    opl_results = []

    for name in names:
        print(f"Searching OpenPowerlifting for: {name}")

        opl_url = search_opl_for_name(driver, name)

        if opl_url:
            print(f" ✓ Found OPL profile: {opl_url}")
            data = scrape_opl_profile(driver, opl_url)
        else:
            print(" ✗ No OPL match found")
            data = None

        opl_results.append({
            "name": name,
            "opl_url": opl_url,
            "data": data,
        })

    driver.quit()

    print("\n📄 Building HTML report…")
    html = build_html_report(opl_results)

    filename = f"report_{slugify(meet_url)}.html"
    save_html_report(html, filename)

    print(f"✅ Report saved → {filename}")


if __name__ == "__main__":
    example_url = "https://liftingcast.com/meets/mfnfcu3cri6q/roster"
    run_pipeline(example_url)
