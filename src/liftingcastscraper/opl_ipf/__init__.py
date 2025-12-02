""" Retrieve OPL data """

from .fetcher import Page
from .lookup import try_fetch_openipf

__all__ = [
    "try_fetch_openipf",
    "generate_username_guesses",
    "Page",
]