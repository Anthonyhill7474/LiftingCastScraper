"""
Server package for the LiftingCast → OpenIPF scraper.

Exposes the FastAPI application instance for use with uvicorn:

    uvicorn liftingcastscraper.server:app --reload
"""

from .main import app