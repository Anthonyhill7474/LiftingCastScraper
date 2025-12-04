# src/liftingcastscraper/server/main.py

import asyncio
import psutil
from datetime import datetime
from typing import List, Dict, Any

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

from liftingcastscraper.pipeline import build_people


app = FastAPI(title="LiftingCast → OpenPowerlifting API")

# Allow your Chrome extension or frontend to call the API
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], 
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

class ReportRequest(BaseModel):
    meet_url: str

class ReportResponse(BaseModel):
    meet_url: str
    generated_at: str
    people: List[Dict[str, Any]]


@app.post("/api/report", response_model=ReportResponse)
async def create_report(body: ReportRequest):
    if not body.meet_url:
        raise HTTPException(status_code=400, detail="meet_url is required")

    try:
        people = await build_people(body.meet_url)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

    return ReportResponse(
        meet_url=body.meet_url,
        generated_at=datetime.utcnow().isoformat() + "Z",
        people=people,
    )


@app.get("/healthz")
def health():
    return {"status": "ok"}

@app.get("/debug/memory")
def memory():
    return {"rss_mb": psutil.Process().memory_info().rss / 1e6}
