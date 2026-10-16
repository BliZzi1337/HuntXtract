HuntXtract – Match tracker for Hunt: Showdown
=============================================
by Chris aka BliZzi1337 · Steam: https://steamcommunity.com/id/blizzi1337/
Twitch: https://www.twitch.tv/blizzi1337 · Discord: blizzi1337
Bugs and ideas: https://github.com/BliZzi1337/HuntXtract/issues

(Deutsche Anleitung: LIESMICH.txt)

What it does
------------
After every match HuntXtract reads your Mission Summary – straight from the screen or from
your OBS game capture (Bounty Collected, Monsters Killed, Hunters Killed, Bounty Token,
Hunter Progress, Hunt Dollars … – or whatever you mark), saves the values and shows
statistics for today, your history, each map and all time. For your stream there are
overlays you can design freely, and the program itself comes in several designs. It never
touches the game, and all data stays on your PC.


Installation
------------
1. Copy the "HuntXtract" folder to a fixed place, e.g. C:\Portable Stuff\HuntXtract
   (don't nest it too deeply, or the data ends up in %LOCALAPPDATA%\HuntXtract).
2. Start HuntXtract.exe. All you need is Windows 10/11 with the Edge WebView2 Runtime
   (always included in Windows 11). OBS (version 28 or newer) is only needed for the OBS
   variant and for the stream overlay.


Setup (once)
------------
On the first start a short tour takes you through all steps – at the very beginning you
choose the language (English or German; later under Settings -> Program -> Language).
If you don't need the tour, tick "I don't need help" and it won't come back. You can start
it again at any time under Settings -> Tour.

If you play Hunt in 16:9 from 1920 × 1080 up (Full HD, 2K, 4K) in English or German,
everything is set up already: HuntXtract notices this on the first start (resolution and
window mode from Hunt's game.cfg, the language from Hunt's log) and takes over a ready-made
setup – the frames for the Mission Summary, Team Details and map, with sample pictures.
Hunt draws its menus in the same places in every 16:9 resolution, so it fits everywhere.
You only pick the image source (step 1) and can play right away. If a frame doesn't sit
right for you, move it and save – from then on it is your own setup. You can load it at
any time with "Load ready-made setup" in step 6 (it replaces your frames and pictures, your
matches stay). For other aspect ratios or game languages, go through the steps below once.

1. Choose the image source (step 1). "Screen directly" is preselected. Two ways:
   a) Via OBS: in OBS go to Tools -> WebSocket Server Settings -> "Enable WebSocket server",
      and copy the password via "Show Connect Info".
   b) Screen directly: no OBS needed, HuntXtract reads from a monitor itself. Hunt should
      run in borderless window mode for this – in exclusive fullscreen the picture often
      stays black. Only Hunt's window is captured, and only while Hunt is in front. If
      another program is in front, the picture stays black and the top bar says "Hunt not
      in front" – your desktop and other programs are never read.
2. HuntXtract -> Setup, step 1: for OBS enter address 127.0.0.1, port 4455 and the
   password, click "Connect & load sources", pick your Hunt game capture, "Apply".
   For the screen there is nothing to pick – HuntXtract takes the monitor Hunt runs on.
3. After a match in Hunt, open the Mission Summary and wait until all numbers have
   settled. Then "Capture now" (or "Capture in 5 seconds" and switch back to the game).
   With "Screen directly", "Capture now" waits until you are back in the game.
4. Mark fields: first "Recognition" – ideally the heading "Mission Summary". Then drag a
   frame around the number of every value (without the label next to it). The zoom
   helps. "View example" shows a finished setup – roughly where the frames belong.
   You can rename fields, remove them or add your own. The type next to a field says how
   it is counted: "Number" is added up (kills, bounty), "Latest" is not – only the newest
   value counts, which suits the Hunter Level.
5. Team Details (optional): in Hunt switch to the "Team Details" tab, capture it and mark
   your teammates' names, their MMR stars and the match MMR there. For stars use the field
   type "Stars" and drag the frame around the row of stars only.
   Enter your Hunt name at the top: HuntXtract looks for the row with this name and tracks
   its stars as "Own MMR". If the field is empty, HuntXtract takes your Steam name by itself
   (that's the one Hunt shows). If you switch there after a match, HuntXtract adds the
   values to the same match.
6. Map (optional, step 5): at the start of a round, before the first clue, open the map
   with Tab – without zooming – and capture it, easiest with "Capture in 5 seconds". The
   frames for the legend, the map area and the map name are already set for 16:9 screens;
   "Test map" shows whether your yellow dot and the map name are recognised. More under
   "Trail".
7. "Test reading" – do the values look right? Then "Save setup".
   While the Mission Summary is open, the recognition meter should be clearly above the
   threshold, and clearly below it otherwise.


Good to know
------------
- With the OBS variant the game capture has to be visible in the active OBS scene,
  otherwise OBS doesn't capture the game at all. You don't have to be streaming.
- "Screen directly" doesn't need OBS at all. You can switch at any time in step 1; marked
  fields stay as long as the resolution stays the same.
- Nothing is saved while the setup page is open (the top bar then says "Paused – setup
  open"). To count, just switch to the overview.
- After the match, keep the Mission Summary open until all points are awarded: HuntXtract
  waits at least 12 seconds and reads once nothing has counted up for 3 seconds – then
  you hear the sound. If you close it earlier, it saves the last visible state with the
  note "please check the values".
- Meanwhile a small window above the game shows the countdown ("Reading in 9 s") and then
  "Match #… saved". You can click through the window, it doesn't take the focus from the
  game and it doesn't appear on stream. Position and on/off: Settings -> In-game display.
  If you can't see it in the game: run Hunt in borderless window mode instead of
  exclusive fullscreen.
- The Mission Summary doesn't say "Extracted" or "Died" as such. HuntXtract can still work
  it out: in the setup, tick "only when extracted" for fields that only a surviving hunter
  has (Hunter Level, Hunter Progress). If none of them is readable, HuntXtract enters
  "Died", otherwise "Extracted". You can always correct it with a click on the outcome –
  on the overview for the last match or in the match list.
- Hunter level 50: at the highest level the Mission Summary shows a symbol instead of the
  number. HuntXtract recognises it and enters 50 (for fields whose name contains "Level").
- Own MMR afterwards: if you enter your Hunt name later (or change it), HuntXtract fills in
  your own MMR for all earlier matches with Team Details that don't have it yet.
- Missed a match? Ctrl+Alt+K (can be changed in the settings) or "Read now" reads the
  current screen right away – even while Hunt is in the foreground. If you are on
  "Team Details", HuntXtract adds those values to the last match instead. That way you
  can also complete a match afterwards. With "Screen directly", "Read now" in the program
  waits up to 15 seconds until Hunt is in front again. If you briefly leave the game
  during the countdown, HuntXtract waits with reading until you are back.
- Cyrillic teammates: if a teammate's name is written in Cyrillic, the match list shows
  "Cyrillic" and the overview counts in how many matches that was the case. For this,
  Windows needs text recognition for Cyrillic script (Settings -> Time & language ->
  Add a language -> Russian, with the feature "Optical character recognition"). Without
  it such names come out unreadable and are not counted.
- Maps: the overview shows how often you played which map – today, in the last 30 days or
  all time. For each map you see its share of your matches, how often you extracted, your
  average Hunters Killed and Bounty Collected and when you were there last. The picture is
  the map from your last match there. The map comes from Hunt's own log or from the trail –
  even without a look at the map (Tab). Matches without a recognized map don't count here.
- Something read wrong? Click the match under "Matches" and correct it. At the top you see
  the screenshot of the Mission Summary and – if they were read – the one of the Team
  Details; switch between them with the tabs or the arrow keys.
- The same Mission Summary is never counted twice – not even if it is still open after a
  restart of HuntXtract. If it wasn't finished the first time (e.g. because Hunt was in
  the background) and HuntXtract reads it more completely later, it adds the missing
  values to the same match. If you delete a match while the summary is still open, it
  doesn't come back (unless you read it with the hotkey or "Read now").
- If a Hunt update changes the Mission Summary, simply do the setup again.
- If you switch Hunt between English and German, HuntXtract keeps recognising the pages by
  itself – as long as the recognition areas are those of the ready-made setup.


Trail
-----
Once the map is set up (setup, step 5), HuntXtract notes where your yellow dot is every
time you look at the Tab map – at most once a second. When the Mission Summary is saved,
the trail belongs to that match: in the match view the "Trail" tab shows the map with
your way as a marching ant trail (ring = start, dot = end: green if extracted, red if
died), and the match list shows the name of the map.
- Between two looks at the map the trail is joined with a straight line. The more often
  you press Tab, the more precise it gets.
- Only the map as Hunt opens it counts. If you zoomed in, that look is skipped. At the
  start of a round HuntXtract tells by the red border all around, and it remembers each
  map once while doing so. That way the later looks count too, when the clues have long
  moved the red border inwards. So look at the map once at the start of a round, before
  the first clue – just a single time for each map.
- While the match runs, the overview shows the trail live: the map from your last look
  with your way so far. It grows with every look at the map.
- The status at the top shows how many points the trail of the running match has.
- Hover over a point of the trail in the match view (or on the live map) and the picture
  shows the map from exactly that moment – with your teammates where they were then. For
  this HuntXtract keeps a small map picture for every point (about 40 KB).
- If the Steam overlay (Shift+Tab) lies over the map, that look doesn't count – otherwise a
  profile picture in the friends list could pass for your dot.
- Restarting HuntXtract in the middle of a match does not lose the trail.
- Crash or lost connection? Then no Mission Summary comes. "Cancel match" on the "Match in
  progress" card (overview) makes HuntXtract stop waiting for this match and drop its trail;
  the next match is tracked as usual. If you look at the map in that same match again after
  all, it is tracked on.


Hunt status
-----------
HuntXtract reads along what Hunt itself writes into its log (Game.log in the USER subfolder
of the Hunt folder, found through Steam). The top bar then shows where you are: "In the
menu", "In a match · Lawson Delta · 12 min", "Shooting Range" or "Mission Summary". The trail starts exactly
with the match, and the name of the map comes reliably from the log instead of the text
recognition. HuntXtract only reads, changes nothing and needs no internet. Without a Steam
installation of Hunt the display is simply missing, the rest works as before.


Design
------
Settings -> Design: six designs for the whole program – Hunt 1896, Blood Moon, Bayou,
Midnight, Parchment (light) and Modern (the plain design of the first version). Plus a
background picture from the official Hunt fan kit, how strongly it shows through, the
lettering (Western by default; "From design" uses the design's own) and an accent colour
of your own. The title bar and the countdown window in the
game follow the design.


Overlay for OBS
---------------
HuntXtract -> Overlay. Every overlay has its own short address
(http://127.0.0.1:4466/overlay?o=…): copy it and add it in OBS as a browser source.
The preview shows how big the source should at least be. HuntXtract has to be running.
Whatever you change in the program afterwards, OBS picks up by itself within about a
second – the address stays the same. You can set:
- Template: Hunt 1896, Modern, Glass, Text only, Wanted poster, Blood Moon, Bayou, Midnight
  (sets colours, font and frame – your values and the layout stay)
- Values & order: period, which values, their order (drag by the handle) and a name of
  your own for each value; title automatic, custom or none
- Layout: bar, card, grid (with number of columns) or one line, alignment, labels above
  or below the value, size and spacing
- Colours & background: background colour with opacity (0 % = fully transparent),
  background picture with its own opacity, colours for values, labels, accent,
  Extracted and Died
- Font & frame: five fonts, frame style, corners, shadow and text shadow
Several overlays at once are no problem, e.g. a bar for "Today" and a card for the last
match. The overlay uses the language of the program. Old addresses with "?scope=…" keep
working but don't pick up changes – replace them once with the new address.


Your data
---------
Everything is in the "Daten" subfolder:
  matches.json       your matches
  Matches\           a folder per day, in it one per match (e.g. "19-08 Match 34") with
                     stats.txt (all values to read) and the pictures: summary.jpg, team.jpg,
                     karte.jpg and karte-001.jpg … (the map at every point of the trail).
                     Pictures can be switched off (Settings -> Save screenshots).
  sicherungen\       daily copy of the matches (the last 14 days)
  geloescht.json     matches deleted in the last 24 hours (so they don't come back)
  kalibrierung.json  + referenz.png / referenz-team.png / referenz-karte.png: your setup
  laufspur.json      the trail of the match that is running (+ laufspur-bilder\: the map at
                     every point until the match is saved)
  kartenbilder.json  the maps HuntXtract remembers (for the trail)
  laufspur-verworfen.jpg  the last look at the map that didn't count (for troubleshooting)
  overlays.json      your overlays for OBS
  settings.json      settings (the OBS password is encrypted with your Windows account)
  protokoll.log      log file, in case something doesn't work (written in German)
To move, just take the whole folder along. Under "Matches" there is also a CSV export for
Excel.

Tidying up: Settings -> Data -> "Keep matches" – all, the last 30 or 90 days, the last year
or the last 100 to 1000 matches. HuntXtract tidies up older ones at the start and after every
match, either only their pictures (values and statistics stay) or the whole match. If a
change would delete something right away, HuntXtract says how much first and asks. Pictures
from before version 1.6.0 move into the folders once on the first start (HuntXtract backs up
the matches first).


Pictures and fonts
------------------
HuntXtract is a fan project and not affiliated with Crytek. The background pictures come
from the official Hunt: Showdown fan kit by Crytek. The fonts are free under the SIL Open
Font License (licence texts in the "Lizenzen" folder):
- Crimson Text – the body font of the official Hunt website
- EB Garamond – a free Garamond, close to the titles in the game
- Rye – a western font for the wanted-poster look
The original fonts of the game (among them Garamond Premier Pro, Latinaires Pro,
GT Sectra) are commercially licensed and may not be passed on.
