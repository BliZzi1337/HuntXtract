using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kopfgeld
{
    static class Theme
    {
        public static readonly Color Background = Color.FromArgb(10, 10, 10);
        public static readonly Color Bar = Color.FromArgb(22, 22, 22);
        public static readonly Color Menu = Color.FromArgb(34, 34, 34);
        public static readonly Color Border = Color.FromArgb(58, 58, 58);
        public static readonly Color Hover = Color.FromArgb(50, 50, 50);
        public static readonly Color Text = Color.FromArgb(220, 220, 220);
        public static readonly Color TextDim = Color.FromArgb(150, 150, 150);
        public static readonly Color Link = Color.FromArgb(110, 231, 183);
        public static readonly Color Ok = Color.FromArgb(52, 211, 153);
        public static readonly Color Warn = Color.FromArgb(251, 191, 36);
        public static readonly Color Bad = Color.FromArgb(248, 113, 113);
        public static readonly Color Idle = Color.FromArgb(120, 120, 120);

        static readonly Dictionary<Color, Image> Dots = new Dictionary<Color, Image>();

        public static Image Dot(Color color, float scale)
        {
            Image image;
            if (Dots.TryGetValue(color, out image)) return image;
            int size = (int)(12 * scale + 0.5f);
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(color))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float inset = size * 0.18f;
                g.FillEllipse(brush, inset, inset, size - 2 * inset, size - 2 * inset);
            }
            Dots[color] = bmp;
            return bmp;
        }
    }

    // Colours of the current design for everything outside the web UI: title bar, window
    // background while the UI loads, and the countdown window in the game. The UI reports them
    // as "bg,panel,text,accent,muted,track,dark" (hex without #).
    sealed class WindowChrome
    {
        public Color Background { get; private set; }
        public Color Panel { get; private set; }
        public Color Text { get; private set; }
        public Color Accent { get; private set; }
        public Color Muted { get; private set; }
        public Color Track { get; private set; }
        public bool Dark { get; private set; }

        // The look before the UI has ever reported one (the "Hunt 1896" design).
        public static readonly WindowChrome Default = Parse("0d0b09,16120f,ede6da,d9b36a,a3998b,2c2620,1");

        public static WindowChrome Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] parts = text.Split(',');
            if (parts.Length != 7) return null;
            var colors = new Color[6];
            for (int i = 0; i < 6; i++)
            {
                int rgb;
                if (parts[i].Length != 6 || !int.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber, null, out rgb)) return null;
                colors[i] = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            }
            return new WindowChrome
            {
                Background = colors[0], Panel = colors[1], Text = colors[2], Accent = colors[3], Muted = colors[4], Track = colors[5],
                Dark = parts[6] == "1",
            };
        }
    }

    sealed class DarkColors : ProfessionalColorTable
    {
        public override Color StatusStripGradientBegin { get { return Theme.Bar; } }
        public override Color StatusStripGradientEnd { get { return Theme.Bar; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Menu; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Menu; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Menu; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Menu; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Border; } }
        public override Color MenuItemSelected { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.Menu; } }
        public override Color MenuItemPressedGradientMiddle { get { return Theme.Menu; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.Menu; } }
        public override Color ButtonSelectedHighlight { get { return Theme.Hover; } }
        public override Color ButtonSelectedGradientBegin { get { return Theme.Hover; } }
        public override Color ButtonSelectedGradientMiddle { get { return Theme.Hover; } }
        public override Color ButtonSelectedGradientEnd { get { return Theme.Hover; } }
        public override Color ButtonPressedGradientBegin { get { return Theme.Menu; } }
        public override Color ButtonPressedGradientMiddle { get { return Theme.Menu; } }
        public override Color ButtonPressedGradientEnd { get { return Theme.Menu; } }
        public override Color ButtonSelectedBorder { get { return Theme.Border; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }

    sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.Text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? e.Item.ForeColor : Theme.TextDim;
            base.OnRenderItemText(e);
        }
    }
}
