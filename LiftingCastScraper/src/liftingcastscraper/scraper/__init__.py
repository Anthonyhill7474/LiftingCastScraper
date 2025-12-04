"""scraper package init"""


# from .selenium_scraper import scrape_liftingcast_roster, get_driver
from .playwright_scraper import scrape_liftingcast_roster
from .utils import lifter_link_selector, slugify, clean_lifter_name, normalize_liftingcast_url


__all__ = [
    "scrape_liftingcast_roster",
    "slugify",
    "clean_lifter_name",
    "normalize_liftingcast_url",
]