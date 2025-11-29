import aiohttp
from .fetcher import Page
import logging

logger = logging.getLogger(__name__) # __name__ is the module name e.g. __name__ == "openipf.fetcher"


async def try_fetch_openipf(name: str, session: aiohttp.ClientSession) -> dict | None:
    """
    Try fetching OpenIPF data using username guessing and URL fallback.
    Returns:
        {
            "profile_url": "...",
            "meet_history": [...]
        }
    or None if not found.
    """

    if not name:
        raise ValueError("Name must be provided")
    
    guesses = generate_username_guesses(name)
    logger.info("Trying OpenIPF lookup for '%s' with %d username guesses", name, len(guesses))

    for guess in guesses:
        try:
            url = f"https://www.openipf.org/u/{guess}"
            page = Page(url=url)

            logger.info(" → Trying username guess: %s", guess)

            data = await page.get_data(session)  # async HTML fetch + parse

            logger.info(" ✓ Match found for %s → %s", name, url)

            return {
                "profile_url": url,
                "meet_history": data,
            }

        except Exception as e:
            logger.warning(" ✗ Guess '%s' failed (%s)", guess, e)

    logger.warning("No OpenIPF profile found for '%s'", name)

    # SECOND ATTEMPT — direct URL, currently redundant, but kept for future flexibility
    # try:
    #     url = f"https://www.openipf.org/u/{username_guess}"
    #     page = Page(url=url)
    #     data = page.get_data()
    #     return {
    #         "profile_url": url,
    #         "meet_history": data,
    #     }
    # except Exception as e:
    #     logger.warning(f"OpenIPF URL lookup failed for {name}: {e}")

    return None  # No match found


def generate_username_guesses(name: str):
    """
    Given a name, generate common username variants:
    """
    base = name.lower()

    # normalize pieces
    no_space = base.replace(" ", "")
    no_dash = no_space.replace("-", "")
    with_dashes = base.replace(" ", "-")

    guesses = [
        no_dash,       # most reliable, OPENIPF-style
        no_space,      
        with_dashes,   
    ]

    # Keep order, ensure uniqueness. Ensures no_dash is tried first
    seen = set()
    ordered = []
    for g in guesses:
        if g not in seen:
            ordered.append(g)
            seen.add(g)

    return ordered
