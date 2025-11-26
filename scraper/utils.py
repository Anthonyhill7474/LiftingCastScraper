"""
Scraper utility helpers.
All small functions shared between scrapers live here.
"""

import logging
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
    Returns a callable for WebDriverWait.
    """
    def _cond(driver):
        return EC.presence_of_all_elements_located((By.CSS_SELECTOR, selector))(driver)
    return _cond
