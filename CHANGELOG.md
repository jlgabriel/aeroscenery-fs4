# Changelog

A photoscenery-only fork of [AeroScenery](https://github.com/nickhod/aeroscenery) by Nick
Hoddinott, built on [@chrispriv](https://github.com/chrispriv)'s community mod j. This fork's own
version line starts at 1.2. Everything below that section is the history of AeroScenery and of the
community mods, kept as it was written.

---

## 2.0 — not yet released

The first public version of this fork. It converts in the app, with no IPACS tool at all, and it
can stop the photoscenery at a hand-drawn coastline.

### Added

- **A built-in `.ttc` converter.** The app writes Aerofly tiles itself: no GPU, no desktop session
  and no IPACS SDK. On a comparable workload it was about 40 times faster than GeoConvert. The
  output is checked byte for byte against an independent Python reference implementation of the
  format, and both have test suites. Tiles are stored rather than LZHAM-compressed; Aerofly FS 4
  renders them either way, and they take more disk.
- **A cut at the coastline.** *Draw Coast* on the Map tab traces the waterline. The converter stops
  the photoscenery a set distance out to sea from it (3 NM by default), along the shape of the
  coast, and writes masks so Aerofly's own sea shows beyond. A dropdown next to *Cut NM* sets which
  side of the line is land, so the cut works for a coast that faces any way. The setting is *Cut at
  the coastline* on the Converter tab. A grid square is cut only if it lies wholly inside the stretch of coast the
  line covers; the log names any square it did not cut, and why.
- **AeroSceneryConvert**, the same converter as a console program, for scripts.

### Changed

- **Downloads really run in parallel.** Each download thread used to hold a thread-pool thread while
  it waited, and above about 28 threads the pool starved. The pool is now sized for the setting.
  With Bing, 64 simultaneous downloads fetch a full grid square at zoom 17 in about four minutes.
- **Bing tiles come over https from the host Bing's own map site uses.** The imagery is the same,
  so tiles already on disk stay valid.
- **Each download thread has its own random seed**, so the threads no longer wait in step.
- **The Map Type button flips between satellite and a drawn map with one click.** The arrow still
  opens the full list, and each side remembers its own choice.
- **Installing a level 9 grid square mirrors the build.** Installed tiles that the new build no
  longer makes are removed, so a square converted again with a coastline cut does not keep its old
  sea. A smaller square shares its level 9 folder and still only adds. An empty build installs
  nothing and changes nothing.
- **Converting starts from an empty output folder**, for the same reason.
- **One `.tmc` per grid square.** There is no longer anything to split a square across.

### Fixed

- **Stop, then Start again, starts a clean run.** While a stopped run ends, the button reads
  *Stopping* and cannot start a second run. Stop also cancels the requests that are under way, so
  it takes effect at once. An error that ends a run is written to the log, and the window is ready
  for the next run.
- **A resumed download fills every hole.** A tile counts as done only when its image and its
  `.aero` file are both there. If only the `.aero` file is missing, it is written without a new
  download.
- **The download summary counts the tiles Bing has no imagery for.** Bing answers those with a
  valid response that says *no-tile*. They now appear as *with no imagery available*, and the
  warning about missing tiles appears when it should. Missing tiles show black in the finished
  scenery, and the warning now says so.
- **The log has one timing line per step.** Progress inside the conversion changes only the label
  on screen.

### Removed

- **IPACS GeoConvert**, with the GeoConvert Wrapper, the parallel strips and their memory model,
  the raw output and the SDK folder setting. The built-in converter replaces all of it.
- The *Geoconvert Raw Images* choice in *Delete Files*. A `-raw` folder left by an earlier version
  now goes with the TTC files.

An existing `settings.xml` keeps its values: the two settings that were renamed keep their names in
the file.

---

## 1.2

The first version of this fork. It narrows the app to one job — build photoscenery for Aerofly
FS 4. Most of the work was to remove the features that this job does not use, and then to make
the remaining path solid from download to install.

### Fixed

- **A failed download is no longer kept as an image tile.** The downloader now checks the HTTP
  status and the first bytes of the body before it saves a tile. On a test run over Easter Island
  (size 9, zoom 14, Google, 1122 tiles), 882 responses were "not found" pages, because Google has no
  imagery there at that zoom. Those pages were saved as `.jpg`, and the next run counted them as
  downloaded. Now 404 and 410 are told apart from failures worth another try (403 is not treated
  as permanent, because servers also use it to slow clients down), and each run reports how many
  tiles are missing.
- **Install Scenery runs as a pipeline action.** Its pipeline step is connected, and the installer
  logs how many `.ttc` files it copied and where.
- **The run waits for GeoConvert to finish.** The log records the elapsed time, the exit code and
  the number of `.ttc` files, and the actions report complete only when the conversion is.
- **Install works on a machine that has only FS 4.** The app looks for `Aerofly FS 4` first and
  `Aerofly FS 2` second. A configured path still has priority.

### Added

- **Scenery installs into the Aerofly add-on layout**:
  `Aerofly FS 4\addons\scenery\<package>\images\<grid square>\`. That is where add-on
  photoscenery lives — checked against five working packages: only `.ttc` files, any sub-folders
  under `images\`, and no manifest. Aerofly scans the tree recursively, so when each area has its
  own package, an install cannot overwrite another area.
- **A grid square can be GeoConverted in parallel strips.** GeoConvert spends 94% of a run on the
  deepest level, and the bottleneck is the GPU rather than the CPU: one process reached about 10 of
  28 cores, while four processes on quarters of the same square cut a level 11 run from 425 s to
  117 s. The square is cut along longitude only, because longitude is linear in the Aerofly grid
  and a strip boundary therefore lands exactly on a tile edge; latitude is not, and cutting that
  way was measured to make both halves emit the whole row of tiles across the cut.
- **The strip count is capped by what memory allows.** Source images grow fourfold with every zoom
  level, so a count that is comfortable at zoom 15 will page at zoom 16 — slower than the single
  process it replaced. The cap comes from installed memory and the number of tiles at the deepest
  level requested. Measured here: zoom 15 to level 12 allows four strips, zoom 16 to level 13
  allows two.
- **A clock on the run and on the current step**, on the progress tab and in the status bar, and
  each step logs its own duration. A run takes hours, and the clock shows a slow step from a
  stopped one.

### Changed

- **The downloaded grid squares are read from the working directory, not from a database.** Aerofly
  names a square after its level and the hex of its south west corner, so the folder name gives
  back the coordinates and there is nothing left to store.
- log4net 2.0.8 → 2.0.17 and Newtonsoft.Json 12.0.1 → 13.0.3, which clears a critical and a high
  advisory that restore reported on every build.
- The *AFS Working Scenery Folder* setting is now the package name, and is labelled *Scenery
  Package Name*.

### Removed

These features are outside the one job of this fork. They are all still in @chrispriv's community
mod j, for anyone who uses them.

- Moving map over UDP, and its map modes
- The TreesDetection integration and its mask sources
- Elevation data (USGS / OpenTopography) and the QGIS Processing Executor
- PowerShell script generation, and *Fix Missing Tiles*
- OSM data download
- Batch conversion of images to `.tcc` for Android
- FSCloudPort airports — the site is no longer online
- The check for a newer upstream version
- SQLite, with `System.Data.SQLite` and `Dapper`, and with them the two native `SQLite.Interop.dll`
  that had to sit in per-architecture sub-folders
- *Reset Square*, which cleared a database row, and the tool that renamed grid squares saved under
  an earlier naming scheme — it read those names from the database
- Six unused NuGet packages
- The Aerofly FS 2 SDK tools are no longer part of the download. GeoConvert is obtained
  separately; see the README.

---

## [1.1.3-mod.j] – Community Mod j
**Maintainer:** chrispriv
**Based on:** AeroScenery 1.1.3-beta

### Added
- Moving map functionality for Aerofly FS2 / FS4 using UDP data streaming
- Modes: *Map fixed*, *Flight tracing*, *Hide working tiles*
- Automatic IP address detection
- Interpolation for smoother moving-map movement
- Enhanced *Search Tile / Location* function with geocoding via OpenStreetMap data

---

## [1.1.3-mod.i] – Community Mod i

### Added
- Tooltips and hints to improve usability for beginners
- Configurable working scenery name for easier installation
- Support for downloading elevation data with **10 m resolution** (USGS via OpenTopography)
- Trees Presets selection (works with TreesDetection App 1.0 Beta)
- Batch conversion of raw images to `.tcc` files for **Android (Mobile)** platform

---

## [1.1.3-mod.h] – Community Mod h

### Fixed
- Switzerland Geoportals
- LINZ (New Zealand)
- Here WeGo map source (now requires API key)

### Changed
- Google Satellite switched to secured HTTPS
- Manual removal of deprecated Google URL template required

### Added
- PowerShell script for downloading 30 m elevation GeoTIFF data from OpenTopography
- QGIS Processing Executor integration (coastline peak fixing, GeoTIFF decompression)
- Separate action selections for OSM and elevation data downloads
- Maximum altitude setting for TreesDetection
- Improved handling of missing **and empty** image tiles

---

## [1.1.3-mod.g] – Community Mod g

### Added
- Integrated *TreesDetection App* as optional processing step
- Configurable number of simultaneous downloads (1–8, recommended: 6)
- Tile search by FS2 grid coordinates (e.g. `8500_a500`)
- Toolbar buttons:
  - Open AFS2 User Folder
  - Open Scenery Editor (FS2 Cultivation Editor by Nabeel)

---

## [1.1.3-mod.f] – Community Mod f

### Added
- PowerShell script for manual OSM data download via Overpass API
- Boundary box copy to clipboard for AFS2 Editor
- Additional Google Earth (Web) map option
- Clipboard helpers for coordinates and grid square names

### Changed
- Optimized tile download logic to fetch only missing image tiles

---

## [1.1.3-mod.e] – Community Mod e

### Added
- Mapbox map source (requires access token)
- Enhanced PowerShell script to download missing image tiles only

### Changed
- Increased retry attempts for tile downloads
- Improved reliability for HTTPS ArcGIS sources

---

## [1.1.3-mod.d] – Community Mod d

### Fixed
- ArcGIS source working again
  *(Known issue: some tiles may still be missing)*

---

## [1.1.3-mod.c] – Community Mod c

### Added
- PowerShell script generation for manual bulk image tile downloads

---

## [1.1.3-mod.b] – Community Mod b

### Added
- Carto DB Light map source for masking purposes

---

## [1.1.3-mod.a] – Community Mod a

### Fixed
- fscloudport airports due to URL and HTTPS changes

---

# Original AeroScenery Releases (Upstream)

## [1.1.3]
- Temporary fix for Here WeGo maps version change
- USGS source working again
- Added GuleSider orthophoto source
- Option to disable confirmation message for concurrent GeoConvert runs

## [1.1.2]
- Added support for Here WeGo maps orthophoto source

## [1.1.1]
- Added retry for USGS tiles on non-success HTTP responses

## [1.1.0]
- Added support for many additional orthophoto sources
- Fixed culture-specific decimal separator issue

## [1.0.2]
- Fixed issues with hex folder names
- GeoConvert wrapper enabled by default

## [1.0.1]
- Fixed null registry entry issues
- Fixed missing sample image installation

## [1.0.0]
- Switched from registry-based to XML-based configuration
- Added Install Scenery toolbar button
- Multiple fixes and enhancements to grid handling and GeoConvert integration

## [0.6]
- Improved GeoConvert handling and UI stability
- Multiple fixes related to culture handling and tile processing
