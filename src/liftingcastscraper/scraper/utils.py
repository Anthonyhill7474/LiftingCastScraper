"""
Scraper utility helpers.
All small functions shared between scrapers live here.
"""

import logging
import re #regular expressions
import os # for direectory creation and file operatinos
from selenium.webdriver.common.by import By
from selenium.webdriver.support import expected_conditions as EC

logger = logging.getLogger(__name__)


def lifter_link_selector() -> str:
    """
    CSS selector that matches lifter anchor elements on LiftingCast:
    find any <a> tag whose href contains '/lifter/'.
    """
    return 'a[href*="/lifter/"]'


def wait_for_lifters_condition(selector: str):
    """
    For Selenium expected_conditions usage: wait for presence of elements.
    Returns a callable function for WebDriverWait.
    """
    def _cond(driver):
        return EC.presence_of_all_elements_located((By.CSS_SELECTOR, selector))(driver)
    return _cond

def slugify(value: str) -> str:
    """
    Turn a URL or string into a filesystem-safe-ish slug.
    """
    # slugify("https://liftingcast.com/meet/2024 Nationals")  → "liftingcast-com-meet-2024-nationals"
    value = value.strip().lower()
    # strip scheme, e.g. http:// or https://
    value = re.sub(r"^https?://", "", value)
    # replace non-alnum with hyphens
    value = re.sub(r"[^a-z0-9]+", "-", value)
    value = value.strip("-")
    return value or "report"

def save_html_report(html: str, path: str):
    # Ensure the output directory exists
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)

    with open(path, "w", encoding="utf-8") as f:
        f.write(html)

    return path


def clean_lifter_name(raw_label: str) -> str:
    """
    Convert something like '105 - Anthony Hill' → 'Anthony Hill'.
    If there's no ' - ', just return the original string.
    """
    if " - " in raw_label:
        _, name = raw_label.split(" - ", 1)
        return name.strip()
    return raw_label.strip()