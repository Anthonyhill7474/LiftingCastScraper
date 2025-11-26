from .fetcher import Page
import logging

logger = logging.getLogger(__name__)


def try_fetch_openipf(name: str):
    """
    Try fetching OpenIPF data using username guessing and URL fallback.
    Returns:
        {
            "profile_url": "...",
            "meet_history": [...]
        }
    or None if not found.
    """

    # Convert "Kiren Sandhu" → "kirensandhu"
    username_guess = name.lower().replace(" ", "")

    # FIRST ATTEMPT — username guess
    try:
        page = Page(username=username_guess)
        data = page.get_data()
        return {
            "profile_url": f"https://www.openipf.org/u/{username_guess}",
            "meet_history": data,
        }
    except Exception as e:
        logger.warning(f"OpenIPF username lookup failed for {name}: {e}")

    # SECOND ATTEMPT — direct URL
    try:
        url = f"https://www.openipf.org/u/{username_guess}"
        page = Page(url=url)
        data = page.get_data()
        return {
            "profile_url": url,
            "meet_history": data,
        }
    except Exception as e:
        logger.warning(f"OpenIPF URL lookup failed for {name}: {e}")

    return None  # No match
