using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Kopfgeld
{
    // The map the game shows on Tab, recognised by an anchor (the legend next to it). While it is
    // open, HuntXtract notes where the player's marker is – that becomes the trail of the match.
    sealed class MapPage : AnchoredPage
    {
        // The map itself, marked along its red border.
        public Area MapArea { get; set; }
        // The name of the map above it ("LAWSON DELTA").
        public Area NameArea { get; set; }

        [ScriptIgnore]
        public bool IsReady
        {
            get { return HasAnchor && MapArea != null && MapArea.IsSet; }
        }

        // Where these are on a 16:9 screen – the setup starts from here.
        public static MapPage CreateDefault()
        {
            return new MapPage
            {
                Anchor = new Area { X = 0.7702, Y = 0.1260, W = 0.1261, H = 0.1001 },
                MapArea = new Area { X = 0.3085, Y = 0.1390, W = 0.3846, H = 0.6830 },
                NameArea = new Area { X = 0.3960, Y = 0.1112, W = 0.2189, H = 0.0213 },
            };
        }
    }

    sealed class MapReading
    {
        // The red border on two opposite edges: the whole map is in view, as the game opens it.
        // At the start of a round the border runs along all edges; the clues move it inwards.
        public bool BorderAround;
        // Share of each edge (top, right, bottom, left) that shows the red border.
        public double[] Edges;
        // The map in small, to compare it with the maps HuntXtract remembers.
        public MapPrint Print;
        // Something lies over the map – the Steam overlay: share (%) of the map area in the dark
        // blue-grey of its panels, and the brightness of the paper frame around the map (the overlay
        // dims the whole game). Its pictures could pass for the marker, so such a look doesn't count.
        public double SteamShare, PaperLuma;
        public bool Found;

        public bool Covered
        {
            get { return SteamShare >= 3 || PaperLuma < 45; }
        }
        // Position of the marker within the map, 0..1 from the top left corner.
        public double X, Y;
        public int Candidates;

        public string EdgeText
        {
            get { return string.Join(" / ", Edges.Select(e => Math.Round(e * 100) + " %")); }
        }
    }

    static class MapReader
    {
        // The map is about 738 px wide on a 1080p screen; sizes below are for that.
        const double ReferenceWidth = 738;

        // The maps of the game by the letters of their name, for text recognition that misses one.
        static readonly string[][] KnownMaps =
        {
            new[] { "STILLWATERBAYOU", "Stillwater Bayou" },
            new[] { "LAWSONDELTA", "Lawson Delta" },
            new[] { "DESALLE", "DeSalle" },
            new[] { "MAMMONSGULCH", "Mammon's Gulch" },
        };

        public static MapReading Locate(Bitmap frame, MapPage page, TrailPoint last)
        {
            Rectangle map = page.MapArea.ToPixels(frame.Width, frame.Height);
            int band = Math.Max(3, (int)Math.Round(map.Width * 0.007));
            // The paper frame around the map, a little away from its edge.
            int paperNear = (int)Math.Round(map.Width * 0.012), paperFar = (int)Math.Round(map.Width * 0.030);
            // More than the marked area: the border line is found even if the box is drawn a few
            // pixels off, and the paper frame is in it too.
            int margin = Math.Max(band, paperFar + 1);
            Rectangle wide = Rectangle.Intersect(Rectangle.Inflate(map, margin, margin), new Rectangle(0, 0, frame.Width, frame.Height));
            int stride;
            byte[] px = Pixels(frame, wide, out stride);
            int ox = map.X - wide.X, oy = map.Y - wide.Y;

            var reading = new MapReading();
            var e = reading.Edges = new[]
            {
                RedShare(px, stride, wide, ox, oy, map.Width, true, oy, band),
                RedShare(px, stride, wide, ox, oy, map.Height, false, ox + map.Width - 1, band),
                RedShare(px, stride, wide, ox, oy, map.Width, true, oy + map.Height - 1, band),
                RedShare(px, stride, wide, ox, oy, map.Height, false, ox, band),
            };
            // Zoomed in, the map's edge can show on two neighbouring sides at most – never on
            // two opposite ones.
            reading.BorderAround = (e[0] >= 0.15 && e[2] >= 0.15) || (e[1] >= 0.15 && e[3] >= 0.15);
            reading.Print = MapPrint.From(px, stride, ox, oy, map.Width, map.Height,
                frame.Width + "x" + frame.Height + " " + map.X + "," + map.Y + "," + map.Width + "," + map.Height);
            reading.SteamShare = SteamShare(px, stride, ox, oy, map.Width, map.Height);
            reading.PaperLuma = PaperLuma(px, stride, wide, ox, oy, map.Width, map.Height, paperNear, paperFar);
            FindMarker(px, stride, ox, oy, map.Width, map.Height, last, reading);
            return reading;
        }

        // Share (%) of the map area in the dark blue-grey of Steam's overlay panels. The map itself
        // is grey-brown: at most 0.1 % on the user's maps, 24 % with the friends list over it.
        static double SteamShare(byte[] px, int stride, int ox, int oy, int w, int h)
        {
            int hits = 0, count = 0;
            for (int y = 0; y < h; y += 2)
            {
                for (int x = 0; x < w; x += 2)
                {
                    int i = (oy + y) * stride + (ox + x) * 4;
                    int b = px[i], g = px[i + 1], r = px[i + 2];
                    double luma = 0.114 * b + 0.587 * g + 0.299 * r;
                    if (luma >= 14 && luma <= 90 && b - r >= 6 && b >= g) hits++;
                    count++;
                }
            }
            return count == 0 ? 0 : 100.0 * hits / count;
        }

        // Brightness of the paper frame left, right and below the map (median): about 80 in every
        // match on the user's PC, whatever the map shows. The Steam overlay dims it with the game.
        static double PaperLuma(byte[] px, int stride, Rectangle wide, int ox, int oy, int w, int h, int near, int far)
        {
            var values = new List<int>();
            for (int y = oy + h / 10; y < oy + h * 9 / 10; y += 2)
            {
                for (int d = near; d <= far; d++)
                {
                    AddLuma(values, px, stride, wide, ox - d, y);
                    AddLuma(values, px, stride, wide, ox + w - 1 + d, y);
                }
            }
            for (int x = ox + w / 10; x < ox + w * 9 / 10; x += 2)
                for (int d = near; d <= far; d++) AddLuma(values, px, stride, wide, x, oy + h - 1 + d);
            if (values.Count == 0) return 255;
            values.Sort();
            return values[values.Count / 2];
        }

        static void AddLuma(List<int> values, byte[] px, int stride, Rectangle wide, int x, int y)
        {
            if (x < 0 || y < 0 || x >= wide.Width || y >= wide.Height) return;
            int i = y * stride + x * 4;
            values.Add((int)(0.114 * px[i] + 0.587 * px[i + 1] + 0.299 * px[i + 2]));
        }

        // Red pixels (the dashed border) near one edge, as a share of its length.
        static double RedShare(byte[] px, int stride, Rectangle wide, int ox, int oy, int length, bool horizontal, int line, int band)
        {
            int hits = 0, count = 0;
            for (int t = (int)(length * 0.03); t < length * 0.97; t += 2)
            {
                count++;
                for (int d = -band; d <= band; d++)
                {
                    int x = horizontal ? ox + t : line + d, y = horizontal ? line + d : oy + t;
                    if (x < 0 || y < 0 || x >= wide.Width || y >= wide.Height) continue;
                    int i = y * stride + x * 4;
                    int b = px[i], g = px[i + 1], r = px[i + 2];
                    if (r >= 60 && r - g >= 22 && r - b >= 18)
                    {
                        hits++;
                        break;
                    }
                }
            }
            return count == 0 ? 0 : hits / (double)count;
        }

        // The player's marker is a pale yellow disc; the map itself is grey and teammates are blue.
        // Several candidates: the one nearest to the last position, else the biggest.
        static void FindMarker(byte[] px, int stride, int ox, int oy, int w, int h, TrailPoint last, MapReading reading)
        {
            var mask = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (oy + y) * stride + (ox + x) * 4;
                    int b = px[i], g = px[i + 1], r = px[i + 2];
                    mask[y * w + x] = r >= 150 && g >= 140 && r - b >= 30 && g - b >= 25 && Math.Abs(r - g) <= 45;
                }
            }
            double scale = w / ReferenceWidth;
            int minArea = (int)Math.Max(6, 18 * scale * scale), maxArea = (int)(500 * scale * scale);
            var seen = new bool[w * h];
            var stack = new Stack<int>();
            double bestScore = double.MaxValue;
            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || seen[start]) continue;
                int area = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                long sumX = 0, sumY = 0;
                seen[start] = true;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    int x = p % w, y = p / w;
                    area++;
                    sumX += x;
                    sumY += y;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    if (x > 0) Visit(p - 1, mask, seen, stack);
                    if (x < w - 1) Visit(p + 1, mask, seen, stack);
                    if (y > 0) Visit(p - w, mask, seen, stack);
                    if (y < h - 1) Visit(p + w, mask, seen, stack);
                }
                int bw = maxX - minX + 1, bh = maxY - minY + 1;
                double aspect = bw / (double)bh, fill = area / (double)(bw * bh);
                if (area < minArea || area > maxArea || aspect < 0.5 || aspect > 2 || fill < 0.45) continue;
                reading.Candidates++;
                double cx = (sumX / (double)area + 0.5) / w, cy = (sumY / (double)area + 0.5) / h;
                double score = last != null ? Math.Abs(cx - last.X) + Math.Abs(cy - last.Y) : -area;
                if (score >= bestScore) continue;
                bestScore = score;
                reading.Found = true;
                reading.X = cx;
                reading.Y = cy;
            }
        }

        static void Visit(int p, bool[] mask, bool[] seen, Stack<int> stack)
        {
            if (!mask[p] || seen[p]) return;
            seen[p] = true;
            stack.Push(p);
        }

        static byte[] Pixels(Bitmap frame, Rectangle rect, out int stride)
        {
            var data = frame.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                stride = data.Stride;
                var px = new byte[data.Stride * rect.Height];
                Marshal.Copy(data.Scan0, px, 0, px.Length);
                return px;
            }
            finally
            {
                frame.UnlockBits(data);
            }
        }

        // The map on its own, for the match view.
        public static byte[] Picture(Bitmap frame, MapPage page)
        {
            using (var crop = Imaging.Crop(frame, page.MapArea.ToPixels(frame.Width, frame.Height)))
                return Imaging.EncodeJpeg(crop, 85, 900);
        }

        // The map at one point of the trail – smaller, there are many (about 40 KB each).
        public static byte[] PointPicture(Bitmap frame, MapPage page)
        {
            using (var crop = Imaging.Crop(frame, page.MapArea.ToPixels(frame.Width, frame.Height)))
                return Imaging.EncodeJpeg(crop, 72, 560);
        }

        // The name above the map: { the reading that fits best, the map (or null) }. Only a clear match
        // with one of the maps counts – a garbled reading is no name.
        public static async Task<string[]> ReadNameAsync(OcrService ocr, Bitmap frame, MapPage page)
        {
            if (ocr == null || page.NameArea == null || !page.NameArea.IsSet) return new[] { "", null };
            var readings = await ocr.ReadLineCandidatesAsync(frame, page.NameArea).ConfigureAwait(false);
            string bestRaw = readings.FirstOrDefault(r => r.Length > 0) ?? "", bestName = null;
            double bestScore = double.MaxValue;
            foreach (string raw in readings)
            {
                double score;
                string name = MapName(raw, out score);
                if (name == null || score >= bestScore) continue;
                bestScore = score;
                bestName = name;
                bestRaw = raw;
            }
            return new[] { bestRaw, bestName };
        }

        // "M a m m o n ' s  G u l c h" → "Mammon's Gulch", if the letters come close enough to one of
        // the maps. score: share of letters that differ (0 = exact).
        public static string MapName(string raw, out double score)
        {
            score = double.MaxValue;
            string letters = new string((raw ?? "").ToUpperInvariant().Where(c => c >= 'A' && c <= 'Z').ToArray());
            if (letters.Length < 4) return null;
            string best = null;
            foreach (var map in KnownMaps)
            {
                double share = TeamPage.Distance(letters, map[0]) / (double)Math.Max(letters.Length, map[0].Length);
                if (share >= score) continue;
                score = share;
                best = map[1];
            }
            return score <= 0.35 ? best : null;
        }
    }

    // The map shrunk to Size × Size gray values, and where on which screen it was taken. Compared
    // by its lines (roads, rivers, houses), each measured against the lines around it: an area the
    // clues darkened counts like a bright one. Real maps from the user's rounds: the same map at the
    // start and late in a round 0.68–0.84, another map or a zoomed-in one below 0.05.
    sealed class MapPrint
    {
        const int Size = 96;
        // How far around (in cells) the lines are compared with their surroundings.
        const int Around = 1;
        // At least this similar: the same map, not zoomed in.
        public const double SameMap = 0.35;
        // Prints of another kind (older versions) don't compare.
        public const int CurrentVersion = 2;

        // Which map (null until its name was read), and when it was taken.
        public string Name { get; set; }
        public DateTime Seen { get; set; }
        // Screen size and map area in pixels – prints only compare within the same.
        public string Frame { get; set; }
        // The gray values, base64.
        public string Gray { get; set; }
        public int Version { get; set; }

        double[] lines;

        public static MapPrint From(byte[] px, int stride, int ox, int oy, int w, int h, string frame)
        {
            if (w < Size || h < Size) return null;
            var sum = new double[Size * Size];
            var count = new int[Size * Size];
            for (int y = 0; y < h; y++)
            {
                int row = y * Size / h * Size;
                for (int x = 0; x < w; x++)
                {
                    int i = (oy + y) * stride + (ox + x) * 4;
                    int c = row + x * Size / w;
                    // The brightest channel: the red clue borders hardly stand out from the map then.
                    sum[c] += Math.Max(px[i + 2], Math.Max(px[i + 1], px[i]));
                    count[c]++;
                }
            }
            var gray = new byte[Size * Size];
            for (int c = 0; c < gray.Length; c++) gray[c] = (byte)Math.Round(sum[c] / Math.Max(1, count[c]));
            return new MapPrint { Frame = frame, Gray = Convert.ToBase64String(gray), Version = CurrentVersion };
        }

        // Taken on the same screen and map area, the same way?
        public bool ComparesWith(MapPrint other)
        {
            return other != null && other.Frame == Frame && other.Version == Version;
        }

        // 1 = the same lines in the same places, around 0 = unrelated.
        public double Similarity(MapPrint other)
        {
            if (!ComparesWith(other)) return 0;
            double[] a = Lines(), b = other.Lines();
            double sum = 0;
            int count = 0;
            for (int y = 1; y < Size - 1; y++)
            {
                for (int x = 1; x < Size - 1; x++)
                {
                    sum += a[y * Size + x] * b[y * Size + x];
                    count++;
                }
            }
            return sum / count;
        }

        public void TakeOver(MapPrint newer)
        {
            Gray = newer.Gray;
            Seen = newer.Seen;
            lines = null;
        }

        // How strongly the brightness changes at each point, divided by how strongly it changes
        // around it (Around cells each way), normalised to mean 0 and spread 1.
        double[] Lines()
        {
            if (lines != null) return lines;
            var gray = Convert.FromBase64String(Gray);
            var change = new double[Size * Size];
            for (int y = 1; y < Size - 1; y++)
            {
                for (int x = 1; x < Size - 1; x++)
                {
                    int c = y * Size + x;
                    double gx = gray[c + 1] - gray[c - 1], gy = gray[c + Size] - gray[c - Size];
                    change[c] = Math.Sqrt(gx * gx + gy * gy);
                }
            }
            // Running sums, so the change around each point is quick to add up.
            int stride = Size + 1;
            var sums = new double[stride * stride];
            for (int y = 0; y < Size; y++)
            {
                double row = 0;
                for (int x = 0; x < Size; x++)
                {
                    row += change[y * Size + x];
                    sums[(y + 1) * stride + x + 1] = sums[y * stride + x + 1] + row;
                }
            }
            var result = new double[Size * Size];
            double sum = 0, squares = 0;
            int count = 0;
            for (int y = 1; y < Size - 1; y++)
            {
                for (int x = 1; x < Size - 1; x++)
                {
                    int x1 = Math.Max(1, x - Around), x2 = Math.Min(Size - 2, x + Around);
                    int y1 = Math.Max(1, y - Around), y2 = Math.Min(Size - 2, y + Around);
                    double around = (sums[(y2 + 1) * stride + x2 + 1] - sums[y1 * stride + x2 + 1] - sums[(y2 + 1) * stride + x1] + sums[y1 * stride + x1])
                        / ((x2 - x1 + 1) * (y2 - y1 + 1));
                    double v = change[y * Size + x] / (around + 2.0);
                    result[y * Size + x] = v;
                    sum += v;
                    squares += v * v;
                    count++;
                }
            }
            double mean = sum / count, spread = Math.Sqrt(Math.Max(1e-12, squares / count - mean * mean));
            for (int y = 1; y < Size - 1; y++)
                for (int x = 1; x < Size - 1; x++)
                    result[y * Size + x] = (result[y * Size + x] - mean) / spread;
            return lines = result;
        }
    }

    // The maps as the game opens them, remembered from looks with the red border around – at the
    // start of a round, before the clues move it inwards. A later look counts if it matches one
    // of them: then the map is not zoomed in. Kept on disk for the next rounds.
    sealed class MapMemory
    {
        const int MaxMaps = 12;
        // A remembered map is taken anew after this long.
        const int RenewAfterMinutes = 10;

        readonly object sync = new object();
        List<MapPrint> maps = new List<MapPrint>();

        // The remembered map that matches best (null: none taken the same way), and how well.
        public MapPrint Best(MapPrint print, out double similarity)
        {
            similarity = 0;
            if (print == null) return null;
            lock (sync)
            {
                MapPrint best = null;
                foreach (var known in maps)
                {
                    if (!known.ComparesWith(print)) continue;
                    double s = known.Similarity(print);
                    if (best != null && s <= similarity) continue;
                    similarity = s;
                    best = known;
                }
                return best;
            }
        }

        // A look with the red border around: remember the map, or renew its picture.
        public void Remember(MapPrint print, string name, DateTime now)
        {
            if (print == null) return;
            lock (sync)
            {
                double similarity;
                var known = Best(print, out similarity);
                if (known != null && similarity >= MapPrint.SameMap)
                {
                    bool renew = (now - known.Seen).TotalMinutes >= RenewAfterMinutes, named = known.Name == null && name != null;
                    if (!renew && !named) return;
                    if (renew)
                    {
                        print.Seen = now;
                        known.TakeOver(print);
                    }
                    if (named) known.Name = name;
                    Save();
                    return;
                }
                print.Name = name;
                print.Seen = now;
                maps.Add(print);
                while (maps.Count > MaxMaps) maps.Remove(maps.OrderBy(m => m.Seen).First());
                Save();
                Log.Write("Kartenbild gemerkt" + (name != null ? ": " + name : "") + " – diese Karte zählt jetzt auch ohne roten Rand");
            }
        }

        // A look that matched a remembered map, with the name read: the map gets its name.
        public void Name(MapPrint known, string name)
        {
            if (known == null || name == null) return;
            lock (sync)
            {
                if (known.Name != null) return;
                known.Name = name;
                Save();
            }
        }

        public void Load()
        {
            lock (sync)
            {
                try
                {
                    if (!File.Exists(AppInfo.MapPrintsFile)) return;
                    var loaded = Json.Deserialize<List<MapPrint>>(File.ReadAllText(AppInfo.MapPrintsFile, Encoding.UTF8));
                    if (loaded == null) return;
                    maps = loaded.Where(m => m != null && m.Frame != null && m.Gray != null && m.Version == MapPrint.CurrentVersion).ToList();
                    if (maps.Count < loaded.Count)
                        Log.Write("Gemerkte Kartenbilder aus einer älteren Version verworfen – sie werden zu Rundenbeginn neu gemerkt");
                }
                catch (Exception ex)
                {
                    Log.Write("Gemerkte Kartenbilder konnten nicht gelesen werden: " + ex.Message);
                }
            }
        }

        void Save()
        {
            try
            {
                Files.WriteAllTextAtomic(AppInfo.MapPrintsFile, Json.Serialize(maps));
            }
            catch (Exception ex)
            {
                Log.Write("Kartenbilder konnten nicht gespeichert werden: " + ex.Message);
            }
        }
    }

    sealed class TrailPoint
    {
        // Position on the map, 0..1 from the top left corner.
        public double X { get; set; }
        public double Y { get; set; }
        // Seconds since the first point.
        public int S { get; set; }
        // The map at this moment (with the teammates where they were then): a file in the trail's
        // picture folder while the match runs, in the screenshots once it is saved.
        public string Shot { get; set; }
    }

    // The trail of the match that is running, as saved on disk.
    sealed class TrailState
    {
        public string MapName { get; set; }
        public DateTime Started { get; set; }
        public DateTime LastSeen { get; set; }
        public List<TrailPoint> Points { get; set; }
    }

    // What the trail of a finished match brings along.
    sealed class Trail
    {
        public List<TrailPoint> Points;
        public string MapName;
        public byte[] MapPicture;
        // The map at each point (null where there is none), in the order of the points.
        public List<byte[]> PointPictures;
    }

    // The trail of the running match as the overview shows it live.
    sealed class LiveTrail
    {
        // Counts up with every change, so the overview knows when to fetch it again.
        public int Version;
        public string MapName;
        public DateTime Started, LastSeen;
        public List<TrailPoint> Points;
        public bool HasPicture;
    }

    // Collects the positions from the map while a match runs and hands them to the match that is
    // saved next. Kept on disk, so restarting HuntXtract in the middle of a match loses nothing.
    // All times are UTC.
    sealed class TrailRecorder
    {
        // No look at the map for that long, or a match that would have lasted longer: a new match.
        const int NewMatchAfterMinutes = 30;
        public const int LongestMatchMinutes = 75;
        // Less movement than this (share of the map) is standing still.
        const double MinStep = 0.004;

        // The name is read again this often – a different map means a new match.
        const int NameEverySeconds = 30;
        // The picture of the map on disk is brought up to date this often.
        const int PictureEverySeconds = 15;

        readonly object sync = new object();
        TrailState state = Empty();
        // The map as it looked at the last reading – with the areas the clues ruled out.
        byte[] mapPicture;
        DateTime nameRead, pictureSaved;
        int version;
        // Hunt's log said a match began and no point has come yet: the first one keeps its map and start.
        bool begun;

        static TrailState Empty()
        {
            return new TrailState { Points = new List<TrailPoint>() };
        }

        public int Count
        {
            get { lock (sync) return state.Points.Count; }
        }

        public TrailPoint Last
        {
            get { lock (sync) return state.Points.LastOrDefault(); }
        }

        public int Version
        {
            get { lock (sync) return version; }
        }

        // The map at the last look (JPEG), null if there is none.
        public byte[] Picture
        {
            get { lock (sync) return mapPicture; }
        }

        // A copy of the running trail for the overview.
        public LiveTrail Live()
        {
            lock (sync)
            {
                return new LiveTrail
                {
                    Version = version,
                    MapName = state.MapName,
                    Started = state.Started,
                    LastSeen = state.LastSeen,
                    Points = state.Points.ToList(),
                    HasPicture = mapPicture != null,
                };
            }
        }

        // Is it time to read the name of the map (a new trail, no name yet, or a while since)?
        public bool NeedsName(DateTime now)
        {
            lock (sync)
            {
                return StartsNew(null, now) || state.MapName == null || (now - nameRead).TotalSeconds >= NameEverySeconds;
            }
        }

        // Would this reading start a new trail: the first look at the map, a long break, or a
        // different map by its name? The picture of the map is no measure – it changes during a
        // match (areas the clues rule out get dark).
        public bool StartsNew(string name, DateTime now)
        {
            lock (sync)
            {
                return state.Points.Count == 0
                    || (now - state.LastSeen).TotalMinutes > NewMatchAfterMinutes
                    || (now - state.Started).TotalMinutes > LongestMatchMinutes
                    || (name != null && state.MapName != null && name != state.MapName);
            }
        }

        // Hunt's log says a match began on this map: a trail from an earlier match is over, this one
        // starts with the match. A trail that began with this match stays (HuntXtract restarted).
        // Returns whether anything changed.
        public bool BeginMatch(string mapName, DateTime started)
        {
            lock (sync)
            {
                bool earlier = state.Points.Count > 0 && state.Started < started.AddMinutes(-2);
                if (!earlier && state.Points.Count > 0 && state.MapName == mapName) return false;
                if (earlier || state.Points.Count == 0)
                {
                    state = Empty();
                    mapPicture = null;
                    DeletePointPictures();
                    state.Started = started;
                    state.LastSeen = started;
                    begun = true;
                }
                state.MapName = mapName;
                version++;
                Save(false);
                return true;
            }
        }

        // name: null when it was not read this time (or could not be read). pointPicture: the map
        // for a new point (only asked for when there is one), null to keep none.
        public void Add(double x, double y, string name, byte[] picture, Func<byte[]> pointPicture, DateTime now, bool nameWasRead)
        {
            lock (sync)
            {
                bool fresh = StartsNew(name, now);
                if (fresh && !(begun && state.Points.Count == 0))
                {
                    state = Empty();
                    DeletePointPictures();
                    state.Started = now;
                }
                begun = false;
                if (nameWasRead) nameRead = now;
                if (name != null) state.MapName = name;
                state.LastSeen = now;
                var last = state.Points.LastOrDefault();
                bool moved = last == null || Math.Abs(last.X - x) >= MinStep || Math.Abs(last.Y - y) >= MinStep;
                if (moved)
                {
                    var point = new TrailPoint { X = Math.Round(x, 4), Y = Math.Round(y, 4), S = (int)(now - state.Started).TotalSeconds };
                    state.Points.Add(point);
                    if (pointPicture != null)
                    {
                        try
                        {
                            string file = state.Points.Count.ToString("D3", CultureInfo.InvariantCulture) + ".jpg";
                            Directory.CreateDirectory(AppInfo.TrailPicturesDir);
                            File.WriteAllBytes(Path.Combine(AppInfo.TrailPicturesDir, file), pointPicture());
                            point.Shot = file;
                        }
                        catch (Exception ex)
                        {
                            Log.Write("Kartenbild zum Punkt konnte nicht gespeichert werden: " + ex.Message);
                        }
                    }
                }
                if (picture != null) mapPicture = picture;
                bool withPicture = fresh || (now - pictureSaved).TotalSeconds >= PictureEverySeconds;
                if (withPicture) pictureSaved = now;
                version++;
                Save(withPicture);
            }
        }

        // The trail for the match that is being saved; the recorder starts over.
        public Trail Take(DateTime now)
        {
            lock (sync)
            {
                Trail taken = null;
                if (state.Points.Count > 0 && (now - state.Started).TotalMinutes <= LongestMatchMinutes)
                {
                    taken = new Trail { Points = state.Points, MapName = state.MapName, MapPicture = mapPicture, PointPictures = new List<byte[]>() };
                    foreach (var point in state.Points) taken.PointPictures.Add(ReadPointPicture(point.Shot));
                }
                Clear();
                return taken;
            }
        }

        // The running match was ended by hand (Hunt crashed, the connection dropped – no Mission
        // Summary will come): its trail is dropped. Returns how many points it had.
        public int Discard()
        {
            lock (sync)
            {
                int points = state.Points.Count;
                Clear();
                return points;
            }
        }

        // The picture of a point while the match runs (null: none).
        public static byte[] ReadPointPicture(string shot)
        {
            if (string.IsNullOrEmpty(shot) || shot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
            try
            {
                string file = Path.Combine(AppInfo.TrailPicturesDir, shot);
                return File.Exists(file) ? File.ReadAllBytes(file) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        void Clear()
        {
            state = Empty();
            mapPicture = null;
            nameRead = DateTime.MinValue;
            begun = false;
            version++;
            try
            {
                if (File.Exists(AppInfo.TrailFile)) File.Delete(AppInfo.TrailFile);
                if (File.Exists(AppInfo.TrailMapFile)) File.Delete(AppInfo.TrailMapFile);
            }
            catch (Exception ex)
            {
                Log.Write("Laufspur konnte nicht gelöscht werden: " + ex.Message);
            }
            DeletePointPictures();
        }

        static void DeletePointPictures()
        {
            try
            {
                if (!Directory.Exists(AppInfo.TrailPicturesDir)) return;
                foreach (string file in Directory.GetFiles(AppInfo.TrailPicturesDir, "*.jpg")) File.Delete(file);
            }
            catch (Exception ex)
            {
                Log.Write("Kartenbilder der Laufspur konnten nicht gelöscht werden: " + ex.Message);
            }
        }

        void Save(bool withPicture)
        {
            try
            {
                Files.WriteAllTextAtomic(AppInfo.TrailFile, Json.Serialize(state));
                if (withPicture && mapPicture != null) File.WriteAllBytes(AppInfo.TrailMapFile, mapPicture);
            }
            catch (Exception ex)
            {
                Log.Write("Laufspur konnte nicht gespeichert werden: " + ex.Message);
            }
        }

        // A trail left from before a restart, if its match can still be running.
        public void Load()
        {
            lock (sync)
            {
                try
                {
                    if (!File.Exists(AppInfo.TrailFile)) return;
                    var loaded = Json.Deserialize<TrailState>(File.ReadAllText(AppInfo.TrailFile, Encoding.UTF8));
                    var now = DateTime.UtcNow;
                    if (loaded == null || loaded.Points == null || loaded.Points.Count == 0 ||
                        (now - loaded.LastSeen.ToUniversalTime()).TotalMinutes > NewMatchAfterMinutes)
                    {
                        Clear();
                        return;
                    }
                    loaded.Started = loaded.Started.ToUniversalTime();
                    loaded.LastSeen = loaded.LastSeen.ToUniversalTime();
                    state = loaded;
                    if (File.Exists(AppInfo.TrailMapFile)) mapPicture = File.ReadAllBytes(AppInfo.TrailMapFile);
                    Log.Write("Laufspur des laufenden Matches übernommen: " + state.Points.Count + " Punkte");
                }
                catch (Exception ex)
                {
                    Log.Write("Laufspur konnte nicht gelesen werden: " + ex.Message);
                    state = Empty();
                }
            }
        }
    }
}
