'use strict';

// Languages of the interface. The texts in app.js are written in German; EN holds the English
// version of each of them, looked up by the exact German text. Texts with values in them are
// templates: t('Match #{id} gespeichert', { id: 7 }) → "Match #7 saved".

// Flags as small pictures: the Union Jack (drawn without clip paths, so several copies on one
// page do not get in each other's way) and the German tricolour.
const LANGUAGES = [
  {
    id: 'en',
    name: 'English',
    flag: '<svg viewBox="0 0 60 30" preserveAspectRatio="xMidYMid slice">' +
      '<path fill="#012169" d="M0 0h60v30H0z"/>' +
      '<path stroke="#fff" stroke-width="6" d="M0 0l60 30M60 0L0 30"/>' +
      '<path fill="#c8102e" d="M0 0l30 15-.9 1.8-30-15zM30 15l30 15 .9-1.8-30-15zM30 15L60 0l-.9-1.8-30 15zM0 30l30-15 .9 1.8-30 15z"/>' +
      '<path stroke="#fff" stroke-width="10" d="M30 0v30M0 15h60"/>' +
      '<path stroke="#c8102e" stroke-width="6" d="M30 0v30M0 15h60"/></svg>',
  },
  {
    id: 'de',
    name: 'Deutsch',
    flag: '<svg viewBox="0 0 5 3" preserveAspectRatio="none">' +
      '<path fill="#000" d="M0 0h5v1H0z"/><path fill="#d00" d="M0 1h5v1H0z"/><path fill="#ffce00" d="M0 2h5v1H0z"/></svg>',
  },
];

// missing: German texts shown in English without a translation – for the tests.
const I18N = { lang: 'en', missing: new Set() };

// Attributes h() shows in the chosen language, like the text inside an element.
const TRANSLATED_PROPS = new Set(['title', 'placeholder', 'alt', 'aria-label']);

// Numbers and dates: German as in Germany; English as the system writes it if Windows runs in
// English (1,234 · 09/25/2026 in the US), otherwise British (1,234 · 25/09/2026).
function localeTag() {
  if (I18N.lang === 'de') return 'de-DE';
  const system = navigator.language || '';
  return /^en\b/i.test(system) ? system : 'en-GB';
}

// Words that read the same in both languages – no translation needed, nothing missing.
const SAME = new Set([
  'Matches', 'Overlay', 'Overlay 1', 'Kills', 'Bounty', 'Team', 'Screenshot', 'Status', 'Text', 'Port', 'Name',
  'Position', 'Design', 'Test', 'Test:', 'optional', 'ZOOM', 'Browser', 'Hunt', 'Hunt 1896', 'Modern', 'Western',
  'Bayou', 'Crimson', 'Showdown', 'Mission Summary', 'Team Details', 'Extracted', 'Died', 'Dead', 'Extraction Rate',
  'Extracted/Died', 'Match #{id}', 'Ø', 'Match MMR', 'MMR', 'Start', 'Position', 'Live',
]);

// Texts t() has already put into the language – not to be looked up again.
const DONE = new Set();

function tr(text) {
  if (I18N.lang === 'de' || typeof text !== 'string') return text;
  const hit = EN[text];
  if (hit !== undefined) return hit;
  if (!SAME.has(text) && !DONE.has(text) && !EN_VALUES.has(text) && /[A-Za-zÄÖÜäöüß]{2}/.test(text)) I18N.missing.add(text);
  return text;
}

// The text in the chosen language with {name} filled in from vars.
function t(text, vars) {
  let out = tr(text);
  if (vars) out = out.replace(/\{(\w+)\}/g, (m, key) => (key in vars ? String(vars[key]) : m));
  if (I18N.lang !== 'de') {
    if (DONE.size > 400) DONE.clear();
    DONE.add(out);
  }
  return out;
}

const EN = {
  // ------------------------------------------------------------ page frame (index.html)
  'Steam-Profil von Chris aka BliZzi1337 öffnen': 'Open the Steam profile of Chris aka BliZzi1337',
  'Übersicht': 'Overview',
  'Einrichtung': 'Setup',
  'Einstellungen': 'Settings',
  'Liest den aktuellen Bildschirm sofort aus – die Mission Summary oder, wenn sie zu sehen ist, die Team Details':
    'Reads the current screen right away – the Mission Summary or, if they are showing, the Team Details',
  'Jetzt auslesen': 'Read now',
  'bis zum Auslesen': 'until reading',

  // ------------------------------------------------------------ general
  'gerade eben': 'just now',
  'vor {n} Min.': '{n} min ago',
  'vor {n} Std.': '{n} h ago',
  'gestern': 'yesterday',
  'vor {n} Tagen': '{n} days ago',
  'Unbekannt': 'Unknown',
  'Unbekannter Fehler': 'Unknown error',
  'Nur im HuntXtract-Programm verfügbar': 'Only available inside the HuntXtract program',
  'Ausgang?': 'Outcome?',
  'Klicken: Extracted → Died → Unbekannt': 'Click: Extracted → Died → Unknown',
  'Abbrechen': 'Cancel',
  'Schließen': 'Close',
  'Speichern': 'Save',
  'Löschen': 'Delete',
  'Übernehmen': 'Apply',
  'Öffnen': 'Open',
  'Ändern': 'Change',
  'Kopieren': 'Copy',
  'Vorschau': 'Preview',
  'Weiter': 'Next',
  'Zurück': 'Back',
  'Überspringen': 'Skip',
  'Vorheriges Bild (←)': 'Previous picture (←)',
  'Nächstes Bild (→)': 'Next picture (→)',
  'Beispiel: so liegen die Rahmen': 'Example: where the frames go',
  'Eine fertige Einrichtung bei 1920 × 1080. Bei dir liegen die Rahmen an ungefähr denselben Stellen – je nach Auflösung leicht versetzt. Gestrichelt ist die Wiedererkennung, die Überschrift der Seite. Die Namen anderer Spieler sind unkenntlich gemacht.':
    'A finished setup at 1920 × 1080. On your screen the frames sit in about the same places – slightly shifted depending on the resolution. The dashed one is the recognition area, the heading of the page. The names of other players are pixelated.',
  'Lese den aktuellen Bildschirm aus …': 'Reading the current screen …',
  'HuntXtract konnte nicht starten': 'HuntXtract could not start',

  // ------------------------------------------------------------ status
  'OBS verbunden': 'OBS connected',
  'Verbinde mit OBS …': 'Connecting to OBS …',
  'OBS nicht erreichbar': 'OBS not reachable',
  'OBS-Passwort falsch': 'Wrong OBS password',
  'Keine Quelle gewählt': 'No source selected',
  'Quelle fehlt in OBS': 'Source missing in OBS',
  'Quelle liefert kein Bild': 'Source shows no picture',
  'Wartet auf Mission Summary': 'Waiting for Mission Summary',
  'Pausiert – Einrichtung offen': 'Paused – setup open',
  'Mission Summary erkannt': 'Mission Summary detected',
  'Liest aus …': 'Reading …',
  'Match gespeichert': 'Match saved',
  'Nicht eingerichtet': 'Not set up',
  'Erkennung aus': 'Detection off',
  'Bildschirm wird gelesen': 'Reading the screen',
  'Bildschirm nicht lesbar': 'Screen not readable',
  'Erkennung wartet auf ein Bild': 'Detection is waiting for a picture',
  'Erkennung wartet auf OBS': 'Detection is waiting for OBS',
  ' · Ähnlichkeit {n} %': ' · similarity {n} %',
  'Einrichtung nicht abgeschlossen': 'Setup not finished',
  'Die Punkte werden noch vergeben – HuntXtract liest aus, sobald sich nichts mehr ändert.':
    'Points are still being awarded – HuntXtract reads once nothing changes any more.',
  'Bitte die Mission Summary offen lassen, bis das Match gespeichert ist.': 'Please keep the Mission Summary open until the match is saved.',
  'Mission Summary erkannt · {n} s': 'Mission Summary detected · {n} s',
  'Lese Werte aus …': 'Reading values …',
  'Gleich fertig.': 'Almost done.',

  // ------------------------------------------------------------ overview
  'Noch nicht eingerichtet': 'Not set up yet',
  'Zeig HuntXtract einmal deine Mission Summary – danach zählt es jedes Match automatisch.':
    'Show HuntXtract your Mission Summary once – after that it counts every match automatically.',
  'Einrichtung starten': 'Start setup',
  'Heute': 'Today',
  'Gestern': 'Yesterday',
  'seit {time} Uhr': 'since {time}',
  '{time} Uhr': '{time}',
  '{date}, {time} Uhr': '{date}, {time}',
  'Match heute': 'match today',
  'Matches heute': 'matches today',
  'Setz den Ausgang bei deinen Matches, dann steht hier deine Extraction Rate.': 'Set the outcome of your matches and your extraction rate shows up here.',
  'Sobald ein Match gespeichert ist, steht hier deine Bilanz des Tages.': 'As soon as a match is saved, your record for the day shows up here.',
  'Summen heute': 'Totals today',
  'Ø {n} pro Match': 'Ø {n} per match',
  'Kyrillische Mitspieler': 'Cyrillic teammates',
  'in {hits} von {total} Matches · {pct} %': 'in {hits} of {total} matches · {pct} %',
  'Heute noch kein Match – hier erscheinen Kills, Bounty, Hunt Dollars und der Rest, sobald eins gespeichert ist.':
    'No match yet today – kills, bounty, Hunt Dollars and the rest show up here once one is saved.',
  'Aktueller Stand': 'Current standing',
  'noch nicht gelesen': 'not read yet',
  'zuletzt gelesen': 'last reading',
  'Ø heute {n} ★': 'Ø today {n} ★',
  'Team-MMR': 'Team MMR',
  'Ø aller Zeilen heute': 'Ø of all rows today',
  'Ø aller Zeilen bisher': 'Ø of all rows so far',
  'Letztes Match': 'Last match',
  'Noch kein Match gespeichert': 'No match saved yet',
  'Sobald in Hunt die Mission Summary erscheint, landet das Match hier. Lass sie offen, bis alle Punkte vergeben sind – nach etwa 15–20 Sekunden kommt der Ton.':
    'As soon as the Mission Summary appears in Hunt, the match lands here. Keep it open until all points are awarded – the sound comes after about 15–20 seconds.',
  'Ausgang': 'Outcome',
  'Kein Screenshot': 'No screenshot',
  'Werte prüfen': 'Check values',
  'manuell ausgelesen': 'read by hand',
  'Letzte 10 Matches': 'Last 10 matches',
  'Noch keine Daten.': 'No data yet.',
  'Bestwert': 'Best',
  'Verlauf': 'History',
  'letzte 20 Matches': 'last 20 matches',
  'Sobald Matches gespeichert sind, siehst du hier den Verlauf.': 'Once matches are saved, you see their history here.',
  '{n} Match': '{n} match',
  '{n} Matches': '{n} matches',
  'Team-MMR Ø {n} ★': 'Team MMR Ø {n} ★',
  'Kyrillische Mitspieler {hits} von {total}': 'Cyrillic teammates {hits} of {total}',
  'Gesamt': 'All time',

  // ------------------------------------------------------------ matches
  '7 Tage': '7 days',
  '30 Tage': '30 days',
  'Alle': 'All',
  'Steht nicht in der Mission Summary – der Hunter ist gestorben': 'Not on the Mission Summary – the hunter died',
  'Nicht erkannt – zum Korrigieren anklicken': 'Not recognised – click to correct',
  'Mindestens ein Mitspieler hat einen kyrillisch geschriebenen Namen': 'At least one teammate has a name written in Cyrillic',
  'kyrillisch': 'Cyrillic',
  'manuell': 'by hand',
  'bearbeitet': 'edited',
  'Notiz': 'Note',
  'Screenshot ansehen': 'View screenshot',
  'CSV exportieren': 'Export CSV',
  'Keine Matches in diesem Zeitraum': 'No matches in this period',
  'Wähle oben einen längeren Zeitraum.': 'Pick a longer period above.',
  'Sobald die Mission Summary erkannt wird, erscheinen die Matches hier.': 'Matches show up here as soon as the Mission Summary is detected.',
  'Ein Klick auf eine Zeile öffnet die Werte zum Prüfen, ein Klick auf den Ausgang schaltet zwischen Extracted, Died und Unbekannt um.':
    'Click a row to check its values; click the outcome to switch between Extracted, Died and Unknown.',
  'Exportiert: {path}': 'Exported: {path}',
  'Fortschritt': 'Progress',
  'Belohnung': 'Rewards',
  'Weitere Werte': 'Other values',
  'du': 'you',
  'gelesen: „{raw}“': 'read: “{raw}”',
  '1 Stern': '1 star',
  '{n} Sterne': '{n} stars',
  'keine Sterne': 'no stars',
  'Symbol – Level 50': 'symbol – level 50',
  'z. B. Trio mit …, Clutch am Boss': 'e.g. trio with …, clutch at the boss',
  'Kein Screenshot gespeichert': 'No screenshot saved',
  'Match #{id} gespeichert': 'Match #{id} saved',
  'Match #{id} ergänzt': 'Match #{id} completed',
  'Match löschen?': 'Delete match?',
  'Match #{id} vom {date} wird endgültig gelöscht, samt Screenshots.': 'Match #{id} from {date} will be deleted for good, screenshots included.',
  'Match #{id} gelöscht': 'Match #{id} deleted',
  '← → wechselt das Bild · Enter speichert': '← → switches the picture · Enter saves',
  'Enter speichert': 'Enter saves',

  // ------------------------------------------------------------ setup
  'Zahl': 'Number',
  'Stand': 'Latest',
  'Sterne': 'Stars',
  'Feld': 'Field',
  'Neues Feld': 'New field',
  'Wiedererkennung': 'Recognition',
  'WIEDERERKENNUNG': 'RECOGNITION',
  'Pausiert, solange die Einrichtung offen ist': 'Paused while the setup is open',
  'Auf dieser Seite speichert HuntXtract keine Matches – auch wenn die Mission Summary erkannt wird. Zum Zählen einfach zur Übersicht wechseln.':
    'HuntXtract saves no matches while this page is open – even if the Mission Summary is detected. To count, just switch to the overview.',
  'Fertig – zur Übersicht': 'Done – to the overview',
  '•••••••• (gespeichert)': '•••••••• (saved)',
  'Passwort aus OBS': 'Password from OBS',
  '{name} (aktuell)': '{name} (current)',
  'Erst „Verbinden“ klicken': 'Click “Connect” first',
  'Verbinde …': 'Connecting …',
  'Verbunden mit OBS {version} – {n} Videoquelle gefunden. Wähle unten deine Spielaufnahme.':
    'Connected to OBS {version} – found {n} video source. Pick your game capture below.',
  'Verbunden mit OBS {version} – {n} Videoquellen gefunden. Wähle unten deine Spielaufnahme.':
    'Connected to OBS {version} – found {n} video sources. Pick your game capture below.',
  'Bitte zuerst verbinden und eine Quelle wählen.': 'Please connect first and pick a source.',
  'OBS-Einstellungen gespeichert': 'OBS settings saved',
  'Bildquelle': 'Image source',
  'In OBS: ': 'In OBS: ',
  'Werkzeuge → WebSocket-Servereinstellungen': 'Tools → WebSocket Server Settings',
  ' → „WebSocket-Server aktivieren“ anhaken. Das Passwort kopierst du dort über ': ' → tick “Enable WebSocket server”. Copy the password there via ',
  '„Verbindungsinformationen anzeigen“': '“Show Connect Info”',
  'Adresse': 'Address',
  'Passwort': 'Password',
  'Verbinden & Quellen laden': 'Connect & load sources',
  'Bildquelle (deine Hunt-Spielaufnahme)': 'Image source (your Hunt game capture)',
  'Die Quelle muss in der gerade aktiven Szene sichtbar sein, sonst nimmt OBS das Spiel nicht auf.':
    'The source has to be visible in the active scene, otherwise OBS does not capture the game.',
  'Über OBS': 'Via OBS',
  'Bildschirm direkt': 'Screen directly',
  'HuntXtract liest direkt von einem Monitor – OBS muss dafür nicht laufen.': 'HuntXtract reads straight from a monitor – OBS does not need to run for this.',
  'HuntXtract holt das Bild aus deiner OBS-Aufnahme.': 'HuntXtract takes the picture from your OBS capture.',
  'Hauptbildschirm': 'Main screen',
  'Bildschirm übernommen': 'Screen applied',
  'Hunt sollte dafür im ': 'For this, Hunt should run in ',
  'randlosen Fenster': 'borderless window',
  ' laufen. Im exklusiven Vollbild liefert Windows oft ein schwarzes Bild – dann ist OBS die bessere Quelle. Das Countdown-Fenster von HuntXtract wird nicht mitgelesen.':
    ' mode. In exclusive fullscreen Windows often delivers a black picture – then OBS is the better source. HuntXtract’s countdown window is not captured.',
  'Bildschirm, auf dem Hunt läuft': 'Screen Hunt runs on',
  'Im nächsten Schritt siehst du ein Livebild – daran erkennst du sofort, ob die Aufnahme klappt.':
    'The next step shows a live picture – so you see right away whether capturing works.',
  'Livebild': 'Live picture',
  'Noch kein Bild – ist Schritt 1 erledigt und läuft das Spiel?': 'No picture yet – is step 1 done and is the game running?',
  'LIVE VOM BILDSCHIRM': 'LIVE FROM THE SCREEN',
  'LIVE AUS OBS': 'LIVE FROM OBS',
  'Jetzt aufnehmen': 'Capture now',
  'In 5 Sekunden aufnehmen': 'Capture in 5 seconds',
  'Mission Summary aufnehmen': 'Capture the Mission Summary',
  'Öffne in Hunt nach einem Match die Mission Summary (mit Bounty Collected, Monsters Killed, Hunters Killed …) und warte, bis alle Zahlen fertig hochgezählt sind. Dann aufnehmen. Mit „In 5 Sekunden“ hast du Zeit, zurück ins Spiel zu wechseln.':
    'After a match in Hunt, open the Mission Summary (with Bounty Collected, Monsters Killed, Hunters Killed …) and wait until all numbers have finished counting up. Then capture. “In 5 seconds” gives you time to switch back to the game.',
  'Eine Mission Summary ist schon aufgenommen – neu aufnehmen ersetzt sie, markierte Felder bleiben.':
    'A Mission Summary is already captured – capturing again replaces it, marked fields stay.',
  'Aufnahme in {n} …': 'Capturing in {n} …',
  '{page} aufgenommen ({w}×{h}). Jetzt die Felder markieren.': '{page} captured ({w}×{h}). Now mark the fields.',
  'Felder markieren': 'Mark fields',
  'Zeigt eine fertige Einrichtung – so ungefähr gehören die Rahmen hin': 'Shows a finished setup – roughly where the frames belong',
  'Beispiel ansehen': 'View example',
  'Noch keine Mission Summary aufgenommen': 'No Mission Summary captured yet',
  'Erledige zuerst Schritt 2.': 'Do step 2 first.',
  'Wähle rechts einen Eintrag und zieh auf dem Bild einen Rahmen darum. Rahmen lassen sich verschieben und an den Ecken ändern. Tipp: die Rahmen eng um die Zahl legen, ohne Beschriftung daneben.':
    'Pick an entry on the right and drag a frame around it on the picture. Frames can be moved and resized at the corners. Tip: keep the frames tight around the number, without the label next to it.',
  'nur bei Extracted: ': 'only when extracted: ',
  'Felder, die nur in der Mission Summary stehen, wenn dein Hunter rausgekommen ist – zum Beispiel Hunter Level oder Hunter Progress. HuntXtract trägt dann bei jedem Match von selbst „Extracted“ oder „Died“ ein.':
    'fields the Mission Summary only shows when your hunter got out – for example Hunter Level or Hunter Progress. HuntXtract then enters “Extracted” or “Died” for every match by itself.',
  'Feld hinzufügen': 'Add field',
  'Größer zoomen hilft, die Rahmen genau um kleine Zahlen zu legen.': 'Zooming in helps to put the frames tightly around small numbers.',
  'markiert – daran erkennt HuntXtract die Team Details': 'marked – this is how HuntXtract recognises the Team Details',
  'markiert – daran erkennt HuntXtract die Mission Summary': 'marked – this is how HuntXtract recognises the Mission Summary',
  'z. B. die Überschrift „{title}“': 'e.g. the heading “{title}”',
  'ermittelt HuntXtract selbst – die Sterne aus der Zeile mit deinem Namen': 'worked out by HuntXtract – the stars of the row with your name',
  'Feld entfernen': 'Remove field',
  'nicht gefunden': 'not found',
  'Zahl: wird zusammengezählt (Kills, Bounty …)\nStand: nur der neueste Wert zählt, wird nicht summiert (Hunter Level …)\nSterne: zählt die hellen Sterne im Rahmen (MMR)\nStatus: Extracted oder Died\nText: beliebiger Text':
    'Number: added up (kills, bounty …)\nLatest: only the newest value counts, not added up (Hunter Level …)\nStars: counts the lit stars in the frame (MMR)\nStatus: Extracted or Died\nText: any text',
  'markiert': 'marked',
  'jetzt auf dem Bild einen Rahmen ziehen': 'now drag a frame on the picture',
  'noch nicht markiert': 'not marked yet',
  'Steht in der Mission Summary nur, wenn der Hunter rausgekommen ist – z. B. Hunter Level oder Hunter Progress.\nIst keins dieser Felder lesbar, trägt HuntXtract „Died“ ein, sonst „Extracted“.':
    'Only on the Mission Summary when the hunter got out – e.g. Hunter Level or Hunter Progress.\nIf none of these fields is readable, HuntXtract enters “Died”, otherwise “Extracted”.',
  'nur bei Extracted': 'only when extracted',
  'nicht erkannt': 'not recognised',
  'leer – zählt als Died': 'empty – counts as Died',
  '„{raw}“': '“{raw}”',
  'Zeigt eine fertige Einrichtung der Team Details': 'Shows a finished setup of the Team Details',
  'Wechselst du nach der Mission Summary auf „Team Details“, kann HuntXtract auch diese Seite lesen und die Werte beim selben Match ergänzen: Namen deiner Mitspieler, deren MMR-Sterne und die Match-MMR.':
    'If you switch to “Team Details” after the Mission Summary, HuntXtract can read that page too and add its values to the same match: your teammates’ names, their MMR stars and the match MMR.',
  'Dein Steam-Name': 'Your Steam name',
  'Dein Name in Hunt': 'Your name in Hunt',
  'Deine Zeile steht mal oben, mal unten. HuntXtract sucht die Zeile mit diesem Namen und übernimmt deren Sterne als „Eigene MMR“.':
    'Your row is sometimes at the top, sometimes further down. HuntXtract looks for the row with this name and takes its stars as “Own MMR”.',
  'Noch keine Team Details aufgenommen': 'No Team Details captured yet',
  'Öffne in Hunt nach einem Match den Reiter „Team Details“ und nimm ihn auf. Ohne diesen Schritt bleibt alles wie bisher.':
    'After a match in Hunt, open the “Team Details” tab and capture it. Without this step everything stays as it is.',
  'Auslesen testen': 'Test reading',
  'Neu aufnehmen': 'Capture again',
  'Sterne: ': 'Stars: ',
  'Für MMR ziehst du den Rahmen nur um die Sternenreihe, ohne die Beschriftung „MMR“ daneben. HuntXtract zählt die hellen Sterne – aus fünf hellen und einem dunklen wird die 5.':
    'For MMR, drag the frame around the row of stars only, without the “MMR” label next to it. HuntXtract counts the lit stars – five lit and one dark make a 5.',
  'Kyrillische Namen: ': 'Cyrillic names: ',
  'Deine Texterkennung kann nur {lang}. Namen wie „Хантер“ kommen damit als Buchstabensalat an. Abhilfe: Windows-Einstellungen → Zeit und Sprache → Sprache und Region → Sprache hinzufügen → Russisch, dabei das optionale Feature „Texterkennung“ mitinstallieren (nicht als Anzeigesprache setzen). Danach HuntXtract neu starten, dann werden kyrillische Namen richtig gelesen und gezählt.':
    'Your text recognition only knows {lang}. Names like “Хантер” come out as gibberish. Fix: Windows Settings → Time & language → Language & region → Add a language → Russian, and include the optional feature “Optical character recognition” (don’t make it the display language). Then restart HuntXtract, and Cyrillic names are read and counted correctly.',
  'Gespeichert wird alles zusammen mit Schritt 6.': 'Everything is saved together with step 6.',
  // The map on Tab and the trail.
  'Karte · Laufspur': 'Map · trail',
  'Drückst du im Match Tab, zeigt Hunt die Karte mit deinem gelben Punkt. HuntXtract merkt sich bei jedem Blick darauf, wo du gerade bist, und zeichnet daraus deine Laufspur – zu sehen in der Match-Ansicht.':
    'When you press Tab in a match, Hunt shows the map with your yellow dot. Every time you look at it, HuntXtract notes where you are and draws your trail from it – shown in the match view.',
  'Noch keine Karte aufgenommen': 'No map captured yet',
  'Öffne zu Rundenbeginn, vor dem ersten Clue, mit Tab die Karte, ohne zu zoomen, und nimm sie auf – am einfachsten mit „In 5 Sekunden aufnehmen“.':
    'At the start of a round, before the first clue, open the map with Tab without zooming and capture it – easiest with “Capture in 5 seconds”.',
  'Die Rahmen sind für 16:9-Bildschirme schon gesetzt. Prüf nur, ob der Kartenausschnitt genau auf dem roten Rand liegt und die Wiedererkennung die Legende umfasst. Gezählt wird nur die Karte, wie Hunt sie öffnet – gezoomt passen die Positionen nicht. Jede Karte merkt sich HuntXtract einmal zu Rundenbeginn, solange der rote Rand noch außen herum läuft – danach erkennt es sie die ganze Runde.':
    'The frames are already set for 16:9 screens. Just check that the map area sits exactly on the red border and the recognition area covers the legend. Only the map as Hunt opens it counts – zoomed in, the positions don’t fit. HuntXtract remembers each map once at the start of a round, while the red border still runs around the edge – after that it knows it for the whole round.',
  'Karte testen': 'Test map',
  'Kartenausschnitt': 'Map area',
  'Kartenname': 'Map name',
  'die Legende rechts – daran erkennt HuntXtract die Karte': 'the legend on the right – this is how HuntXtract recognises the map',
  'genau auf dem roten Rand der Karte': 'exactly on the red border of the map',
  'der Name über der Karte': 'the name above the map',
  'dein Punkt bei {x} % / {y} %': 'your dot at {x} % / {y} %',
  'kein gelber Punkt gefunden': 'no yellow dot found',
  'roter Rand gefunden': 'red border found',
  'roter Rand nicht gefunden ({edges})': 'red border not found ({edges})',
  'Karte aufgenommen ({w}×{h}). Prüf jetzt die Rahmen und teste die Karte.': 'Map captured ({w}×{h}). Now check the frames and test the map.',
  'Der rote Rand liegt nicht außen am Kartenausschnitt. Zieh den Rahmen genau auf den Rand – und nimm die Karte zu Rundenbeginn auf: ungezoomt und vor dem ersten Clue.':
    'The red border is not along the edge of the map area. Put the frame exactly on the border – and capture the map at the start of a round: without zooming and before the first clue.',
  'Auf der Karte ist kein gelber Punkt zu finden. Nimm die Karte im Match auf, dann ist dein Punkt zu sehen.':
    'There is no yellow dot on the map. Capture the map during a match, then your dot is on it.',
  'Karte erkannt: {name}. Dein Punkt ist gefunden – dann speichern.': 'Map recognised: {name}. Your dot was found – now save.',
  'Dein Punkt ist gefunden – dann speichern.': 'Your dot was found – now save.',
  'Für die Karte fehlt die Wiedererkennung oder der Kartenausschnitt.': 'The map is missing its recognition area or the map area.',
  'Laufspur': 'Trail',
  'Laufspur mit 1 Punkt': 'Trail with 1 point',
  'Laufspur mit {n} Punkten': 'Trail with {n} points',
  'Ende': 'End',
  'Zuletzt hier': 'Last seen here',
  'Punkt {n} von {total} · {time} min': 'Point {n} of {total} · {time} min',
  'Fahr über einen Punkt – dann siehst du die Karte aus diesem Moment.': 'Hover over a point to see the map from that moment.',
  'Im Match · {map} · {n} min': 'In a match · {map} · {n} min',
  'Im Match · {n} min': 'In a match · {n} min',
  'Lädt {map} …': 'Loading {map} …',
  'Lädt …': 'Loading …',
  'Schießstand': 'Shooting Range',
  'Im Level {name}': 'In level {name}',
  'Im Menü': 'In the menu',
  'Aus Hunts eigenem Protokoll': 'From Hunt’s own log',
  'Drück im Match Tab – bei jedem Blick auf die Karte wächst hier deine Spur.': 'Press Tab in the match – your trail grows here with every look at the map.',
  'Von Steam übernommen': 'Taken from Steam',
  'Steam-Namen übernehmen': 'Use Steam name',
  'Laufendes Match': 'Match in progress',
  'Karte noch nicht erkannt': 'Map not recognised yet',
  'Punkte': 'Points',
  'Letzter Blick': 'Last look',
  'Die Spur wächst bei jedem Blick auf die Karte (Tab). Mit der Mission Summary wird sie beim Match gespeichert.':
    'The trail grows with every look at the map (Tab). With the Mission Summary it is saved to the match.',
  'Match abbrechen': 'Cancel match',
  'Nach einem Absturz oder Verbindungsabbruch: nicht länger auf die Mission Summary dieses Matches warten':
    'After a crash or a lost connection: stop waiting for this match’s Mission Summary',
  'Laufendes Match abbrechen?': 'Cancel the match in progress?',
  'Nach einem Absturz oder Verbindungsabbruch kommt keine Mission Summary. HuntXtract wartet dann nicht länger auf dieses Match und verwirft seine Laufspur. Das nächste Match wird wie gewohnt erfasst, gespeicherte Matches bleiben unverändert.':
    'After a crash or a lost connection no Mission Summary comes. HuntXtract then stops waiting for this match and drops its trail. The next match is tracked as usual; saved matches stay as they are.',
  'Weiter verfolgen': 'Keep tracking',
  'Match abgebrochen – HuntXtract wartet auf das nächste.': 'Match cancelled – HuntXtract waits for the next one.',
  'Match abgebrochen': 'Match cancelled',
  'Sucht Match · {time}': 'Searching · {time}',
  'Match gefunden': 'Match found',
  'Matchsuche': 'Matchmaking',
  '1 Suche erfasst': '1 search recorded',
  '{n} Suchen erfasst': '{n} searches recorded',
  'Ø heute': 'Ø today',
  'Ø letzte 10': 'Ø last 10',
  'Ø gesamt': 'Ø all time',
  'Längste': 'Longest',
  '1 Suche': '1 search',
  '{n} Suchen': '{n} searches',
  'Ø Klick bis Match nach Tageszeit': 'Ø click to match by time of day',
  'Lobby gefunden · wartet {time}': 'Lobby found · waiting {time}',
  'Ø Klick bis Match': 'Ø click to match',
  'Vom Klick auf „Bereit“ bis die Karte lädt': 'From clicking “Ready” until the map loads',
  'Ø Suche': 'Ø search',
  'Bis Hunt eine Lobby gefunden hat': 'Until Hunt found a lobby',
  'Ø Lobby': 'Ø lobby',
  'Von der gefundenen Lobby bis die Karte lädt – warten, bis sie voll ist': 'From the lobby being found until the map loads – waiting for it to fill up',
  'Lobbys aufgelöst': 'Lobbies broken up',
  'Lobbys, die sich vor dem Match wieder aufgelöst haben': 'Lobbies that broke up again before the match',
  'Suche': 'Search',
  'Lobby': 'Lobby',
  'Suchen': 'Searches',
  'Insgesamt': 'Overall',
  'Nachts': 'Night',
  'Morgens': 'Morning',
  'Nachmittags': 'Afternoon',
  'Abends': 'Evening',
  '{from}–{to} Uhr': '{from}:00–{to}:00',
  'Ø Suchzeit': 'Ø search time',
  'live': 'live',
  'Warte auf Bild …': 'Waiting for a picture …',
  'Nach dem Speichern siehst du hier live, wie sicher die Mission Summary erkannt wird.': 'After saving, you see live here how reliably the Mission Summary is recognised.',
  'Ungespeicherte Änderungen': 'Unsaved changes',
  'Testen & speichern': 'Test & save',
  '„Auslesen testen“ liest die markierten Felder auf der aufgenommenen Mission Summary. Stimmt etwas nicht, den Rahmen enger oder etwas größer ziehen und nochmal testen.':
    '“Test reading” reads the marked fields on the captured Mission Summary. If something is off, make the frame tighter or a bit bigger and test again.',
  'Einrichtung speichern': 'Save setup',
  'Alles zurücksetzen': 'Reset everything',
  'Schwelle ': 'Threshold ',
  'Solange diese Seite offen ist, wird nichts gespeichert. Zeigt die Anzeige bei offener Mission Summary deutlich mehr als die Schwelle und sonst deutlich weniger, passt alles.':
    'Nothing is saved while this page is open. If the meter shows clearly more than the threshold while the Mission Summary is open and clearly less otherwise, everything is fine.',
  'Wörter für den Status (Extracted / Died)': 'Words for the status (Extracted / Died)',
  'Bedeutet „Extracted“': 'Means “Extracted”',
  'Bedeutet „Died“': 'Means “Died”',
  'Jetzt: {pct} % – Mission Summary erkannt (gespeichert wird erst außerhalb der Einrichtung)':
    'Now: {pct} % – Mission Summary detected (saving only happens outside the setup)',
  'Jetzt: {pct} % – keine Mission Summary': 'Now: {pct} % – no Mission Summary',
  'Noch keine Felder markiert.': 'No fields marked yet.',
  '{failed} von {total} Feldern nicht erkannt: {names}': '{failed} of {total} fields not recognised: {names}',
  'Alle {n} Felder erkannt. Passen die Werte? Dann speichern.': 'All {n} fields recognised. Values look right? Then save.',
  'Bitte zuerst den Bereich „Wiedererkennung“ markieren.': 'Please mark the “Recognition” area first.',
  'Markiere in den Team Details mindestens ein Feld – oder entferne dort die Wiedererkennung.':
    'Mark at least one field on the Team Details – or remove the recognition area there.',
  'In den Team Details fehlt die Wiedererkennung, sonst findet HuntXtract die Seite nicht.':
    'The Team Details need a recognition area, otherwise HuntXtract cannot find the page.',
  'Bitte mindestens ein Feld markieren.': 'Please mark at least one field.',
  'Einrichtung gespeichert. Gezählt wird, sobald du die Einrichtung verlässt – z. B. mit „Fertig – zur Übersicht“.':
    'Setup saved. Counting starts as soon as you leave the setup – e.g. with “Done – to the overview”.',
  'Deine Änderungen an der Einrichtung sind noch nicht gespeichert. Trotzdem zur Übersicht wechseln? Die Änderungen gehen dann verloren.':
    'Your changes to the setup are not saved yet. Switch to the overview anyway? The changes will be lost.',
  'Verwerfen & wechseln': 'Discard & switch',
  'Einrichtung zurücksetzen?': 'Reset the setup?',
  'Die aufgenommene Mission Summary und alle markierten Felder werden gelöscht. Deine gespeicherten Matches bleiben erhalten.':
    'The captured Mission Summary and all marked fields will be deleted. Your saved matches are kept.',
  'Zurücksetzen': 'Reset',
  'Einrichtung zurückgesetzt': 'Setup reset',
  'Aufgenommene Mission Summary': 'Captured Mission Summary',

  // ------------------------------------------------------------ overlay
  'Letzte 10': 'Last 10',
  'Letzte 10 Matches (Ø)': 'Last 10 matches (Ø)',
  '{name} (Kopie)': '{name} (copy)',
  'Overlay löschen?': 'Delete overlay?',
  '„{name}“ wird gelöscht. Eine OBS-Quelle mit seiner Adresse zeigt danach das erste Overlay.':
    '“{name}” will be deleted. An OBS source with its address then shows the first overlay.',
  'Im Overlay zeigen': 'Show in the overlay',
  'Anklicken zum Umbenennen – leer lassen für den normalen Namen': 'Click to rename – leave empty for the normal name',
  'Ziehen zum Sortieren': 'Drag to sort',
  'Nach oben': 'Move up',
  'Nach unten': 'Move down',
  'Anzahl': 'count',
  'Größe im Stream: {w} × {h} px': 'Size on stream: {w} × {h} px',
  ' (hier auf {pct} % verkleinert)': ' (shrunk to {pct} % here)',
  ' · Browserquelle in OBS mindestens {w} × {h}': ' · browser source in OBS at least {w} × {h}',
  'Vorlage „{name}“ übernehmen': 'Apply template “{name}”',
  'Overlay-Server läuft nicht': 'Overlay server is not running',
  'Overlay ist ausgeschaltet': 'Overlay is switched off',
  'Schalte es in den Einstellungen ein, damit OBS die Werte abrufen kann.': 'Switch it on in the settings so OBS can fetch the values.',
  'Zu den Einstellungen': 'Go to settings',
  '+ Neues Overlay': '+ New overlay',
  'Deine Overlays': 'Your overlays',
  'jedes mit eigener Adresse und eigenem Aussehen': 'each with its own address and look',
  'Adresse für OBS': 'Address for OBS',
  'Adresse kopiert': 'Address copied',
  'Im Browser öffnen': 'Open in browser',
  'Duplizieren': 'Duplicate',
  'In OBS: Quellen → + → ': 'In OBS: Sources → + → ',
  ' → Adresse einfügen. Was du hier einstellst, übernimmt OBS nach einer Sekunde von selbst – die Adresse bleibt gleich. Alte Adressen mit „?scope=…“ laufen weiter, übernehmen aber keine Änderungen von hier.':
    ' → paste the address. Whatever you set here, OBS picks up by itself within a second – the address stays the same. Old addresses with “?scope=…” keep working but don’t pick up changes from here.',
  'Karo': 'Checkerboard',
  'Spielszene': 'Game scene',
  'Dunkel': 'Dark',
  'Hell': 'Light',
  'mit deinen echten Werten': 'with your real values',
  'Vorlage': 'Template',
  'setzt Farben, Schrift und Rahmen – Werte und Anordnung bleiben': 'sets colours, font and frame – values and layout stay',
  'Summe': 'Total',
  'Ø pro Match': 'Ø per match',
  'Werte & Reihenfolge': 'Values & order',
  'Zeitraum': 'Period',
  'Zahlen als': 'Numbers as',
  'Titel': 'Title',
  'Automatisch': 'Automatic',
  'Eigener': 'Custom',
  'Keiner': 'None',
  'Werte': 'Values',
  'Am Griff ziehen zum Sortieren · Haken = wird gezeigt · Namen anklicken zum Umbenennen':
    'Drag by the handle to sort · tick = shown · click a name to rename it',
  'Anordnung': 'Layout',
  'Darstellung': 'Style',
  'Leiste': 'Bar',
  'Karte': 'Card',
  'Raster': 'Grid',
  'Eine Zeile': 'One line',
  'Spalten': 'Columns',
  'Ausrichtung': 'Alignment',
  'Links': 'Left',
  'Mitte': 'Centre',
  'Rechts': 'Right',
  'Beschriftung': 'Labels',
  'Vor dem Wert': 'Before the value',
  'Nach dem Wert': 'After the value',
  'Über dem Wert': 'Above the value',
  'Unter dem Wert': 'Below the value',
  'Größe': 'Size',
  'Abstände': 'Spacing',
  'Unterzeilen zeigen, z. B. „Ø 2,4“': 'Show sub-lines, e.g. “Ø 2.4”',
  'Kurz aufleuchten, wenn sich ein Wert ändert': 'Flash briefly when a value changes',
  'Farben & Hintergrund': 'Colours & background',
  'Hintergrund – Deckkraft 0 % ist ganz durchsichtig': 'Background – opacity 0 % is fully transparent',
  'Bild im Hintergrund': 'Background picture',
  'Kein Bild': 'No picture',
  'Bild {n} %': 'Picture {n} %',
  'Farben': 'Colours',
  'Akzent': 'Accent',
  'Extracted und Died farbig zeigen': 'Show Extracted and Died in colour',
  'Schrift & Rahmen': 'Font & frame',
  'Schrift': 'Font',
  'Rahmen': 'Frame',
  'Linie oben': 'Line on top',
  'Linie links': 'Line on the left',
  'Doppelt': 'Double',
  'Ecken': 'Corners',
  'Schatten um die Box': 'Shadow around the box',
  'Schriftschatten – gut lesbar auch ohne Hintergrund': 'Text shadow – easy to read even without a background',
  // Templates, fonts and pictures of the overlay (overlay.js).
  'Glas': 'Glass',
  'Nur Schrift': 'Text only',
  'Steckbrief': 'Wanted poster',
  'Technisch': 'Technical',
  'Jäger im Nebel': 'Hunter in the Mist',
  'Aufbruch': 'Setting Out',
  'Duell': 'Duel',
  'Nacht am Sumpf': 'Night on the Swamp',
  'Dunkler Wald': 'Dark Forest',
  'Zikade': 'Cicada',
  'Garten der Hexe': 'Witch’s Garden',

  // ------------------------------------------------------------ design
  'Knochenweiß und Messing auf Schwarz, Schrift wie im Spiel.': 'Bone white and brass on black, lettering as in the game.',
  'Blutmond': 'Blood Moon',
  'Glut und tiefes Rot für lange Nächte.': 'Embers and deep red for long nights.',
  'Moos, Messing und Sumpfnebel.': 'Moss, brass and swamp mist.',
  'Mitternacht': 'Midnight',
  'Kühles Mondlicht, klare Schrift.': 'Cool moonlight, clear lettering.',
  'Pergament': 'Parchment',
  'Hell wie ein altes Kontobuch.': 'Light like an old ledger.',
  'Das schlichte Design der ersten Version.': 'The plain design of the first version.',
  'Wie Design': 'From design',
  'Kills heute': 'Kills today',
  'gilt sofort, auch für das Countdown-Fenster im Spiel': 'applies at once, also to the countdown window in the game',
  'Hintergrundbild': 'Background picture',
  'Motive aus dem offiziellen Hunt-Fankit.': 'Artwork from the official Hunt fan kit.',
  'Stärke des Bildes': 'Picture strength',
  'Wie deutlich das Bild hinter den Karten zu sehen ist.': 'How clearly the picture shows behind the cards.',
  'Hunt: Garamond und Crimson Text wie im Spiel und auf huntshowdown.com.': 'Hunt: Garamond and Crimson Text as in the game and on huntshowdown.com.',
  'Akzentfarbe': 'Accent colour',
  'Knöpfe, Markierungen und Hervorhebungen.': 'Buttons, marks and highlights.',

  // ------------------------------------------------------------ settings
  'Tastenkombination drücken …': 'Press a key combination …',
  'Bitte eine Kombination mit Strg/Alt/Umschalt oder eine F-Taste wählen.': 'Please choose a combination with Ctrl/Alt/Shift or an F key.',
  'Erkennung': 'Detection',
  'Automatisch erkennen': 'Detect automatically',
  'Liest die Mission Summary selbstständig aus, sobald sie erscheint.': 'Reads the Mission Summary by itself as soon as it appears.',
  'Prüfungen pro Sekunde': 'Checks per second',
  'Wie oft das Bild geprüft wird. 4 reicht; weniger schont den PC und OBS beim Streamen.':
    'How often the picture is checked. 4 is enough; fewer spares your PC and OBS while streaming.',
  'Screenshots speichern': 'Save screenshots',
  'Hebt zu jedem Match ein Bild der Mission Summary und der Team Details auf – praktisch zum Nachprüfen –, dazu bei der Laufspur die Karte zu jedem Punkt.':
    'Keeps a picture of the Mission Summary and the Team Details for every match – handy for checking – and, with the trail, the map at every point.',
  'Matches aufbewahren': 'Keep matches',
  'Ältere Matches räumt HuntXtract von selbst auf – beim Start und nach jedem Match.': 'HuntXtract tidies up older matches by itself – at the start and after every match.',
  'Beim Aufräumen löschen': 'When tidying up, delete',
  'Nur die Bilder: Werte und Statistik bleiben. Das ganze Match: auch Werte und Laufspur.': 'Only the pictures: values and statistics stay. The whole match: values and trail too.',
  'Alle behalten': 'Keep all',
  'Die letzten 30 Tage': 'The last 30 days',
  'Die letzten 90 Tage': 'The last 90 days',
  'Das letzte Jahr': 'The last year',
  'Die letzten 100 Matches': 'The last 100 matches',
  'Die letzten 250 Matches': 'The last 250 matches',
  'Die letzten 500 Matches': 'The last 500 matches',
  'Die letzten 1000 Matches': 'The last 1000 matches',
  'Nur die Bilder': 'Only the pictures',
  'Das ganze Match': 'The whole match',
  'Alte Matches aufräumen': 'Tidy up old matches',
  'Aufräumen': 'Tidy up',
  'Damit werden jetzt {n} ältere Matches gelöscht – Werte, Laufspur und Bilder ({mb} MB). Das lässt sich nicht rückgängig machen.':
    'This deletes {n} older matches now – values, trail and pictures ({mb} MB). It can’t be undone.',
  'Damit werden jetzt die Bilder von {n} älteren Matches gelöscht ({mb} MB). Werte und Statistik bleiben.':
    'This deletes the pictures of {n} older matches now ({mb} MB). Values and statistics stay.',
  '{mb} MB für {n} Matches – ein Ordner pro Tag, darin einer pro Match mit Bildern und stats.txt.':
    '{mb} MB for {n} matches – a folder per day, in it one per match with pictures and stats.txt.',
  'Screenshot-Breite': 'Screenshot width',
  'Pixelbreite der gespeicherten Screenshots.': 'Width of the saved screenshots in pixels.',
  'Tastenkürzel „Jetzt auslesen“': 'Shortcut “Read now”',
  'Funktioniert auch, während Hunt im Vordergrund ist – falls die Erkennung mal ein Match verpasst.':
    'Also works while Hunt is in the foreground – in case detection misses a match.',
  'Programm': 'Program',
  'Sprache': 'Language',
  'Sprache der Oberfläche, des Countdowns im Spiel und des Overlays.': 'Language of the interface, the in-game countdown and the overlay.',
  'Tagesbeginn': 'Start of the day',
  'Ab dieser Uhrzeit zählt „Heute“ neu – so landet eine Session nach Mitternacht noch beim Vortag.':
    '“Today” starts again at this hour – so a session after midnight still counts for the day before.',
  'Uhr': 'o’clock',
  'Signalton': 'Sound',
  'Kurzer Ton, wenn ein Match gespeichert wurde.': 'A short sound when a match was saved.',
  'In den Infobereich minimieren': 'Minimize to the tray',
  'Beim Minimieren verschwindet das Fenster neben die Uhr und läuft dort weiter.': 'When minimized, the window moves next to the clock and keeps running there.',
  'Damit findet HuntXtract in den Team Details deine Zeile und übernimmt deine MMR-Sterne.':
    'This is how HuntXtract finds your row on the Team Details and takes your MMR stars.',
  'Einführung': 'Tour',
  'Die kurze Tour durch das Programm, die beim ersten Start angeboten wird.': 'The short tour through the program that is offered on the first start.',
  'Tour starten': 'Start tour',
  'Oben Mitte': 'Top centre',
  'Oben links': 'Top left',
  'Oben rechts': 'Top right',
  'Unten Mitte': 'Bottom centre',
  'Unten rechts': 'Bottom right',
  'Anzeige im Spiel': 'In-game display',
  'Countdown im Spiel einblenden': 'Show countdown in the game',
  'Sobald die Mission Summary erkannt ist, zählt ein kleines Fenster über dem Spiel bis zum Auslesen herunter und bestätigt danach das gespeicherte Match. Man kann durch das Fenster hindurchklicken, und es erscheint nicht im Stream.':
    'Once the Mission Summary is detected, a small window above the game counts down to the reading and then confirms the saved match. You can click through the window, and it does not appear on stream.',
  'Auf dem Bildschirm, auf dem das Spiel läuft.': 'On the screen the game runs on.',
  'Overlay-Server': 'Overlay server',
  'Overlay bereitstellen': 'Provide overlay',
  'Stellt die Werte unter einer lokalen Adresse für eine OBS-Browserquelle bereit.': 'Serves the values at a local address for an OBS browser source.',
  'Adresse: {url}': 'Address: {url}',
  'Daten': 'Data',
  'Datenordner': 'Data folder',
  'Protokoll': 'Log',
  'Sicherung': 'Backup',
  'HuntXtract legt jeden Tag automatisch eine Kopie deiner Matches im Unterordner „sicherungen“ an (die letzten 14 Tage).':
    'Every day HuntXtract automatically keeps a copy of your matches in the subfolder “sicherungen” (the last 14 days).',
  'Texterkennung': 'Text recognition',
  'Windows-Texterkennung ({lang}) – läuft komplett auf deinem PC.': 'Windows text recognition ({lang}) – runs entirely on your PC.',
  'by {author} · Match-Tracker für Hunt: Showdown – kein Eingriff ins Spiel, alle Daten bleiben auf deinem PC.':
    'by {author} · match tracker for Hunt: Showdown – never touches the game, all data stays on your PC.',
  'Steam-Profil': 'Steam profile',
  'Fan-Projekt, nicht mit Crytek verbunden. Hintergrundbilder aus dem offiziellen Hunt-Fankit. Schriften: Crimson Text, EB Garamond und Rye unter der SIL Open Font License.':
    'Fan project, not affiliated with Crytek. Background pictures from the official Hunt fan kit. Fonts: Crimson Text, EB Garamond and Rye under the SIL Open Font License.',

  // ------------------------------------------------------------ tour
  'Sprache · Language': 'Language · Sprache',
  'Wähle die Sprache für HuntXtract. Du kannst sie später in den Einstellungen ändern.': 'Choose the language for HuntXtract. You can change it later in the settings.',
  'Willkommen bei HuntXtract': 'Welcome to HuntXtract',
  'HuntXtract liest nach jedem Match deine Mission Summary aus und führt Buch über Kills, Bounty, Hunt Dollars, Extraction Rate und MMR. Ich zeige dir in ein paar kurzen Schritten, wie du es einrichtest.':
    'After every match HuntXtract reads your Mission Summary and keeps track of kills, bounty, Hunt Dollars, extraction rate and MMR. A few short steps show you how to set it up.',
  'Wie heißt du in Hunt?': 'What’s your name in Hunt?',
  'Magst du schon deinen Steam-Namen eintragen, so wie er im Spiel steht? Damit findet HuntXtract in den Team Details deine eigene Zeile und schreibt deine MMR mit. Du kannst das auch überspringen und später in den Einstellungen nachholen.':
    'Want to enter your Steam name now, exactly as it shows in the game? HuntXtract uses it to find your own row on the Team Details and to track your MMR. You can also skip this and do it later in the settings.',
  'Hier geht es los': 'This is where it starts',
  'In der Einrichtung zeigst du HuntXtract einmal, wie deine Mission Summary aussieht. Das dauert ein paar Minuten und muss nur einmal sein.':
    'In the setup you show HuntXtract once what your Mission Summary looks like. It takes a few minutes and only has to be done once.',
  'Schritt 1 · Bildquelle': 'Step 1 · Image source',
  '„Bildschirm direkt“ ist schon gewählt – HuntXtract liest das Bild selbst vom Monitor, OBS brauchst du dafür nicht. Aufgenommen wird dabei nur Hunt, solange es im Vordergrund ist – sonst bleibt das Bild schwarz. Den Bildschirm, auf dem Hunt läuft, findet HuntXtract selbst. Hunt sollte im randlosen Fenster laufen.':
    '“Screen directly” is already selected – HuntXtract reads the picture from the monitor itself, you don’t need OBS for it. Only Hunt is captured, and only while it is in front – otherwise the picture stays black. HuntXtract finds the screen Hunt runs on by itself. Hunt should run in borderless window mode.',
  'Schritt 2 · Mission Summary aufnehmen': 'Step 2 · Capture the Mission Summary',
  'Spiel ein Match und bleib auf der Mission Summary, bis alle Punkte vergeben sind. Dann „In 5 Sekunden aufnehmen“ – so bleibt Zeit, zurück ins Spiel zu wechseln.':
    'Play a match and stay on the Mission Summary until all points are awarded. Then “Capture in 5 seconds” – that leaves time to switch back to the game.',
  'Schritt 3 · Felder markieren': 'Step 3 · Mark fields',
  'Zieh auf dem Bild Rahmen: zuerst um die Überschrift als „Wiedererkennung“, dann um jede Zahl, die gezählt werden soll. „Beispiel ansehen“ zeigt dir, wo die Rahmen ungefähr hingehören. Bei Hunter Level und Hunter Progress „nur bei Extracted“ anhaken – daran erkennt HuntXtract, ob du rausgekommen bist.':
    'Drag frames on the picture: first around the heading as “Recognition”, then around every number that should be counted. “View example” shows roughly where the frames belong. Tick “only when extracted” for Hunter Level and Hunter Progress – that’s how HuntXtract knows whether you got out.',
  'Schritt 4 · Team Details (freiwillig)': 'Step 4 · Team Details (optional)',
  'Wer auch MMR und Mitspieler mitschreiben will, nimmt hier zusätzlich den Reiter „Team Details“ auf und trägt seinen Hunt-Namen ein, damit HuntXtract die eigene Zeile findet.':
    'To track MMR and teammates too, also capture the “Team Details” tab here and enter your Hunt name, so HuntXtract finds your own row.',
  'Schritt 5 · Karte (freiwillig)': 'Step 5 · Map (optional)',
  'Nimm hier die Karte auf, die Hunt auf Tab zeigt. Dann merkt sich HuntXtract bei jedem Blick auf die Karte, wo du bist, und zeichnet deine Laufspur zu jedem Match.':
    'Capture the map Hunt shows on Tab here. Then HuntXtract notes where you are every time you look at the map and draws your trail for each match.',
  'Schritt 6 · Testen & speichern': 'Step 6 · Test & save',
  '„Auslesen testen“ zeigt dir sofort, was erkannt wird. Passt alles: „Einrichtung speichern“, dann „Fertig – zur Übersicht“. Ab da zählt HuntXtract jedes Match von selbst.':
    '“Test reading” shows right away what is recognised. All good? “Save setup”, then “Done – to the overview”. From then on HuntXtract counts every match by itself.',
  'Läuft alles?': 'Is everything running?',
  'Hier oben siehst du jederzeit, ob ein Bild ankommt und ob auf die Mission Summary gewartet wird. Wird sie erkannt, zählt ein kleines Fenster im Spiel bis zum Auslesen herunter.':
    'Up here you always see whether a picture comes in and whether HuntXtract is waiting for the Mission Summary. When it is detected, a small window in the game counts down to the reading.',
  'Match verpasst?': 'Missed a match?',
  '„Jetzt auslesen“ – oder Strg+Alt+K mitten im Spiel – liest den Bildschirm sofort aus. Stehst du dabei auf „Team Details“, ergänzt es das letzte Match.':
    '“Read now” – or Ctrl+Alt+K in the middle of the game – reads the screen right away. If you are on “Team Details”, it adds them to the last match.',
  'Das war’s': 'That’s it',
  'Übersicht, Matches, ein Overlay für deinen Stream und die Einstellungen findest du links. In den Einstellungen wählst du auch das Design und kannst diese Tour jederzeit wieder starten. Viel Erfolg da draußen, Hunter!':
    'Overview, matches, an overlay for your stream and the settings are on the left. In the settings you also choose the design and can start this tour again at any time. Good luck out there, hunter!',
  'Los geht’s': 'Let’s go',
  'Name gespeichert: {name}': 'Name saved: {name}',
  'Speichern & weiter': 'Save & next',
  'Einführung · {n} von {total}': 'Tour · {n} of {total}',
  'Später (Esc)': 'Later (Esc)',
  'Ich brauche keine Hilfe': 'I don’t need help',

  // Maps on the overview
  'Karten': 'Maps',
  'Zu diesen Matches ist keine Karte bekannt.': 'No map is known for these matches.',
  '{n} Karte': '{n} map',
  '{n} Karten': '{n} maps',
  'Extraction': 'Extraction',
  'Zuletzt': 'Last played',
  '{n}× Extracted · {m}× Died': '{n}× extracted · {m}× died',
  'Durchschnitt {label}': 'Average {label}',
  '1 Match ohne Karte': '1 match without a map',
  '{n} Matches ohne Karte': '{n} matches without a map',

  // Straight from the monitor only Hunt is captured
  'Hunt nicht im Vordergrund': 'Hunt not in front',
  'Hunt läuft nicht': 'Hunt isn’t running',
  'Hunt auf anderem Bildschirm': 'Hunt on another screen',
  'HuntXtract nimmt nur Hunt auf. Solange Hunt nicht im Vordergrund ist, bleibt das Bild schwarz – dein Desktop und andere Programme werden nicht gelesen.':
    'HuntXtract only captures Hunt. While Hunt isn’t in front, the picture stays black – your desktop and other programs are not read.',
  'Erkennung pausiert': 'Detection paused',
  'Nur Hunt wird aufgenommen: ': 'Only Hunt is captured: ',
  'HuntXtract liest nur Hunts Fenster und nur, solange Hunt im Vordergrund ist. Ist ein anderes Programm vorne, bleibt das Bild schwarz – dein Desktop und andere Programme werden nie gelesen.':
    'HuntXtract reads only Hunt’s window, and only while Hunt is in front. If another program is in front, the picture stays black – your desktop and other programs are never read.',
  'Wechsle zu Hunt – aufgenommen wird, sobald Hunt im Vordergrund ist.': 'Switch to Hunt – it is captured as soon as Hunt is in front.',
  'Hunt läuft nicht. HuntXtract nimmt nur Hunt auf – starte das Spiel, dann erscheint hier das Livebild.':
    'Hunt isn’t running. HuntXtract only captures Hunt – start the game and the live picture appears here.',
  'Hunt läuft auf einem anderen Bildschirm – wähle in Schritt 1 den Bildschirm, auf dem Hunt läuft.':
    'Hunt runs on another screen – pick the screen Hunt runs on in step 1.',
  'Hunt ist gerade nicht im Vordergrund. HuntXtract nimmt nur Hunt auf – wechsle ins Spiel, dann erscheint hier das Livebild.':
    'Hunt isn’t in front right now. HuntXtract only captures Hunt – switch to the game and the live picture appears here.',
  'Kein Bild – {why}': 'No picture – {why}',
  'Den Bildschirm, auf dem Hunt läuft, findet HuntXtract selbst – auch mit mehreren Monitoren. Im nächsten Schritt siehst du ein Livebild – daran erkennst du sofort, ob die Aufnahme klappt.':
    'HuntXtract finds the screen Hunt runs on by itself – with several monitors too. In the next step you see a live picture – it tells you right away whether capturing works.',

  // The ready-made setup (16:9)
  'Fertig eingerichtet für {res}': 'Set up for {res}',
  'Die Rahmen für Mission Summary, Team Details und Karte kommen aus der mitgelieferten Einrichtung für Hunt in 16:9 (Full HD, 2K, 4K) – zeichnen musst du nichts. Sitzt bei dir nach dem ersten Match ein Rahmen nicht, verschieb ihn und speichere. Dann ist es deine eigene Einrichtung.':
    'The frames for the Mission Summary, Team Details and map come from the ready-made setup for Hunt in 16:9 (Full HD, 2K, 4K) – you don’t have to draw anything. If a frame doesn’t sit right after your first match, move it and save. Then it’s your own setup.',
  'Fertige Einrichtung für {res}': 'Ready-made setup for {res}',
  'Hunt läuft bei dir in {res}. Statt selbst Rahmen zu zeichnen, kannst du die mitgelieferte Einrichtung laden – mit Mission Summary, Team Details und Karte.':
    'Hunt runs in {res} on your PC. Instead of drawing the frames yourself, you can load the setup that comes with HuntXtract – with Mission Summary, Team Details and map.',
  'Vorgabe laden': 'Load preset',
  'Vorgabe laden?': 'Load the preset?',
  'Fertige Einrichtung laden': 'Load ready-made setup',
  'Achtung: {reason} Die Rahmen passen dann vermutlich nicht.': 'Careful: {reason} The frames will probably not fit then.',
  'Die fertige Einrichtung für Hunt in 16:9 ersetzt deine Rahmen und die aufgenommenen Bilder. Deine gespeicherten Matches bleiben erhalten.{warn}':
    'The ready-made setup for Hunt in 16:9 replaces your frames and the captured pictures. Your saved matches are kept.{warn}',
  'Die fertige Einrichtung für Hunt in 16:9 setzt alle Rahmen für dich.{warn}':
    'The ready-made setup for Hunt in 16:9 sets all frames for you.{warn}',
  'Fertige Einrichtung geladen. Spiel einfach ein Match – verschieben kannst du die Rahmen jederzeit.':
    'Ready-made setup loaded. Just play a match – you can move the frames at any time.',
  'Schon eingerichtet für {res}': 'Already set up for {res}',
  'HuntXtract hat die mitgelieferten Rahmen für Mission Summary, Team Details und Karte übernommen. Spiel einfach ein Match – die Mission Summary wird von selbst erkannt.':
    'HuntXtract has taken over the frames it comes with for the Mission Summary, Team Details and map. Just play a match – the Mission Summary is recognised by itself.',
  'Einrichtung ansehen': 'View setup',
  'Die MMR wird erkannt. Die Namen sind auf dem Beispielbild unkenntlich gemacht – in deinen Matches liest HuntXtract sie mit.':
    'The MMR is recognised. The names on the sample picture are made unreadable – in your matches HuntXtract reads them too.',
  'HuntXtract liest nach jedem Match deine Mission Summary aus und führt Buch über Kills, Bounty, Hunt Dollars, Extraction Rate und MMR. Eingerichtet ist schon alles – ich zeige dir in ein paar kurzen Schritten, wo du was findest.':
    'After every match HuntXtract reads your Mission Summary and keeps track of kills, bounty, Hunt Dollars, extraction rate and MMR. Everything is set up already – a few short steps show you where to find what.',
  'Schon eingerichtet': 'Already set up',
  'Hunt läuft bei dir in {res}. Dafür bringt HuntXtract eine fertige Einrichtung mit: Die Rahmen für Mission Summary, Team Details und Karte sind schon gesetzt – zeichnen musst du nichts.':
    'Hunt runs in {res} on your PC. For that, HuntXtract comes with a ready-made setup: the frames for the Mission Summary, Team Details and map are already in place – you don’t have to draw anything.',
  '„Bildschirm direkt“ ist schon gewählt – HuntXtract liest das Bild selbst vom Monitor, OBS brauchst du dafür nicht. Aufgenommen wird dabei nur Hunt, solange es im Vordergrund ist – sonst bleibt das Bild schwarz. Den Bildschirm, auf dem Hunt läuft, findet HuntXtract selbst.':
    '“Screen directly” is already selected – HuntXtract reads the picture from the monitor itself, you don’t need OBS for it. Only Hunt is captured, and only while it is in front – otherwise the picture stays black. HuntXtract finds the screen Hunt runs on by itself.',
  'Testen & anpassen': 'Test & adjust',
  '„Auslesen testen“ zeigt dir, was auf dem Beispielbild erkannt wird. Sitzt bei dir nach dem ersten Match ein Rahmen nicht, verschieb ihn und speichere – dann ist es deine eigene Einrichtung.':
    '“Test reading” shows you what is recognised on the sample picture. If a frame doesn’t sit right after your first match, move it and save – then it’s your own setup.',
};

// English texts are not looked up again (h() sees the result of t() and of tr() once more).
const EN_VALUES = new Set(Object.values(EN));
