# The Red Frontier

**A Junior Astronaut Mission Trainer for kids aged 7–12, built for the NASA Space Apps Challenge 2026.**
Challenge: *Build a Junior Astronaut Mission Trainer.*

▶ **Play in your browser:** https://theredfrontier.pages.dev (no download, no account; works best with a keyboard and mouse)

---

## What is it?

The Red Frontier program will send the first crew of six people to Mars. Before they launch, one junior astronaut — you — must prove that a person can live at **Meridiani Base**, the spot where NASA's Opportunity rover landed in 2004.

You run the base in first person with HAB, the friendly habitat computer. Every stage is based on **something that really happened on Mars**, and every stage you finish adds a lesson to the **Crew Handbook** for the crew.

Nobody ever dies in this game. If things go wrong, HAB calls a *safe abort* and the debrief explains what happened and what the real NASA team did.

## How to play

| | |
|---|---|
| **Walk** | W A S D |
| **Turn** | Arrow keys (or hold the right mouse button) |
| **Use things** | Click them (or press E) |
| **Sleep / end the day** | The bed, or the *End the day* button |

1. **Start each day with the NASA forecast.** It uses real NASA data where we have it.
2. **Do today's missions** from the list on the right. A yellow arrow shows the way.
3. **Watch your air.** Inside the base the Air maker keeps you breathing. Outside, your air drops **every second**, so plan short trips and walk back through the airlock in time.
4. **Choose what to switch on.** Life support, heater, radio, plant lights and the science lab all use battery power. No choice is free.

## What's in the game

- **Story mode:** *First Sols* (a step-by-step tutorial) plus 8 stages based on real events:
  - **The Storm:** Opportunity, June 2018 dust storm
  - **Lucky Wind:** Spirit, March 2005, when wind cleaned its solar panels
  - **Cold Night:** Spirit's last winter, 2010
  - **Stuck:** Opportunity at Purgatory Dune (2005) and Spirit at Troy (2009)
  - **Wheel Trouble:** Curiosity's damaged wheels, 2013
  - **Ice Hunters:** the Phoenix lander finds water ice, 2008
  - **Silence:** solar conjunction, when the Sun blocks the radio
  - **Memory Glitch:** Opportunity's flash-memory "amnesia", 2014–2015
- **The Storm** has a real-time emergency: a dust storm hits while you collect ice. Do you leave the ice and run, or stay and risk it? Then you have to survive the night on battery power alone.
- **Daily missions:** NASA forecast, rock samples, status reports to Earth, weather station readings, rover repairs, and optional (risky!) trips to buried ice that becomes drinking water and oxygen.
- **Rock collection:** every rock you pick up rolls a rarity (Common to Legendary). There are 12 real Mars finds, from hematite "blueberries" to the Cheyava Falls "leopard spots". Each one has its real story on the home screen.
- **Freeplay:** endless random missions, credits and an upgrade shop.
- **Your spacesuit:** suit colors, visors and mission patches, earned by finishing stages, collecting rare rocks and drilling ice. You can try on locked items.
- **Three levels:** Easy, Medium and Hard.

## Real NASA data (and honest labels)

Every number in the game is labelled **Real · Live**, **Real · Archived (with date)**, or **Simulated**. Simulated numbers are never shown as real.

| Data | Used for |
|---|---|
| **NASA DONKI** (space weather, via api.nasa.gov) | Checked **live** when the game starts: solar flares, CMEs and radiation storms of the last 30 days, shown with dates in every forecast. If NASA doesn't answer, the game says so and uses a saved snapshot. |
| **Opportunity 2018 power data** | The real power and dust readings (June 3, 4, 6 and 10, 2018) behind *The Storm*. |
| **Mars date** | Today's Perseverance mission sol and the Mars season, calculated from today's real date (Mars24 method from NASA GISS). |
| **NASA Image and Video Library** | Real photos in briefings, debriefs and the Crew Handbook, with titles, dates and credits. |
| **NASA 3D Resources** | The Perseverance rover and the astronaut suit models (public domain; NASA logos removed because our Mission Control is fictional). |
| **Rover mission history** | The real events behind each stage, the rock collection, and the debriefs. |

The full list, with what each dataset is used for, is on the in-game **NASA science** page (`Assets/_Game/Resources/Data/sources.json`).

*Mission Control and the Red Frontier program are fictional. This project is not affiliated with or endorsed by NASA.*

## Built with

- **Unity 6** (6000.6.9f1), Universal Render Pipeline, WebGL 2
- **Unity Input System**, **glTFast** + **Draco** (for the NASA 3D models)
- No paid assets: the terrain, textures, habitat, solar panels, antenna, airlock and UI are generated in code by editor tools.

## Project layout

```
Assets/_Game/
  Scripts/Missions/   MissionRunner (the shared engine), DayPlanner, FreeplayGenerator
  Scripts/Sim/        SimEngine, HabitatState (battery, air, food)
  Scripts/UI/         PlaytestUI (HUD, cutscenes), MenuUI (title, map, suit, rocks)
  Scripts/World/      Airlock, rover, ice sites, weather station, sample sites
  Scripts/Data/       NasaLive (live DONKI + Mars date), RockCollection
  Scripts/Editor/     MarsSceneBuilder, NasaModelSetup, WebGLBuild
  Resources/Missions/ One JSON file per stage (missions are data, not code)
  Resources/Data/     Sources, rocks, glossary, DONKI snapshot, NASA config
web/                  The WebGL build that Cloudflare Pages serves
```

## Build it yourself

1. Open this folder in **Unity 6000.6.9f1** (Unity Hub → Add project).
2. Menu **Mission Trainer → Build Mars Scene (3D)** (only if `Assets/_Game/Scenes/Mars.unity` is missing).
3. Menu **Mission Trainer → Use NASA 3D Models**.
4. Menu **Mission Trainer → Build WebGL (itch.io)**. This builds to `Builds/WebGL` and copies the game to `web/`.

Optional: put your own free NASA API key (from https://api.nasa.gov) in `Assets/_Game/Resources/Data/nasa_config.json`. The shared `DEMO_KEY` is often rate-limited. Anything in a WebGL build can be read by players, so only use a free NASA key.

## Deploying to Cloudflare Pages

The repo already contains the built game in `web/`, so Cloudflare doesn't need to build anything:

- **Workers & Pages → Create → Pages → Connect to Git →** this repository
- **Project name:** `theredfrontier` (this gives the address theredfrontier.pages.dev)
- **Framework preset:** None · **Build command:** *(leave empty)* · **Build output directory:** `web`

Every push to `main` redeploys. Cloudflare Pages allows at most 25 MiB per file; the build script warns if a file is bigger.

## AI disclosure

This project was made with substantial help from **generative AI**, as the Space Apps rules ask us to disclose:

- **Claude Code** (Anthropic's AI coding assistant) wrote most of the C# code, the Unity editor tools that generate the 3D scene, textures and meshes, the mission data files, and much of the in-game text (HAB's lines, debriefs, rock facts, the Crew Handbook). It also play-tested the WebGL build in a browser and fixed bugs it found.
- **The team** came up with the concept and design, decided what to build, directed every change, play-tested the game, and reported what to fix.
- **No AI-generated images, video or audio** are used. The photos and 3D models of real hardware are NASA's own public-domain assets. The game's textures and shapes are made procedurally by code, not by an image generator.
- Facts about real missions were written with AI help from the sources listed above. If you spot a mistake, please open an issue.
- The NASA data shown in the game comes from NASA, not from AI. Live data is fetched from NASA's API, and saved data is labelled with its date.

## Credits

- NASA, NASA/JPL-Caltech, NASA CCMC (DONKI), NASA GISS (Mars24), University of Arizona (Phoenix), MIT (MOXIE), SwRI (RAD)
- NASA 3D Resources: https://github.com/nasa/NASA-3D-Resources
- NASA Image and Video Library: https://images.nasa.gov
