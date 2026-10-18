using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;

namespace MapleExpTracker
{
    public enum OverlayStatus { Live, Waiting, NoRegion, Paused }

    /// <summary>Everything the overlay shows, already formatted.</summary>
    public sealed class OverlayView
    {
        public const string Dash = "—";

        public string Name = "";
        public int Level;               // 0 = unknown
        public bool HasExp;
        public long Exp;
        public long LevelTotal;         // EXP the level needs, 0 = unknown
        public bool LevelTotalExact;    // from the EXP table rather than estimated
        public double Percent = double.NaN;
        public string Message;          // shown in the EXP bar instead of the numbers
        public string Recent = Dash, Rate = Dash, RateHour = Dash, Session = Dash, SessionTime = Dash, Eta = Dash;
        public bool RecentNegative, SessionNegative;
        public string EtaLabel = "TO LEVEL UP";
        public OverlayStatus Status = OverlayStatus.Waiting;
        public bool SettingsHover;      // mouse over the settings button

        public string Key()
        {
            return string.Join("|", new[]
            {
                Name, Level.ToString(CultureInfo.InvariantCulture), HasExp ? Exp.ToString(CultureInfo.InvariantCulture) : "-",
                LevelTotal.ToString(CultureInfo.InvariantCulture) + (LevelTotalExact ? "" : "~"),
                Percent.ToString("0.0000", CultureInfo.InvariantCulture), Message ?? "", Recent, Rate, RateHour, Session,
                SessionTime, Eta, EtaLabel, Status.ToString(), RecentNegative ? "1" : "0", SessionNegative ? "1" : "0",
                SettingsHover ? "1" : "0"
            });
        }
    }

    /// <summary>A color theme. All themes are dark; they differ in their accent color.</summary>
    public sealed class Palette
    {
        public string Name, DisplayName;
        public Color Border, PanelTop, PanelBottom;
        public Color HeaderTop, HeaderBottom, HeaderSheen, HeaderLine, Title, NameText;
        public Color BadgeTop, BadgeBottom, BadgeBorder, BadgeText;
        public Color TrackTop, TrackBottom, TrackBorder;
        public Color Label, Value, ValueShadow, Negative, Missing, Divider;
        public Color TileTop, TileMid, TileBottom, TileBorder, TileDigitOutline;   // level digit tiles
        public Color FillTop, FillMid, FillBottom, FillEdge;                      // EXP bar fill
        public Color CoinTop, CoinBottom, CoinRim;                                // header icon
        public bool Rainbow;                                                      // rainbow border, title, tiles and bar

        public static readonly Color[] RainbowColors =
        {
            Rgb(0xFF4D4D), Rgb(0xFF9F43), Rgb(0xFFE14D), Rgb(0x4CD964), Rgb(0x4DA6FF), Rgb(0xA66BFF)
        };

        static Color Rgb(int rgb)
        {
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        static Color Rgba(int alpha, int rgb)
        {
            return Color.FromArgb(alpha, Rgb(rgb));
        }

        /// <summary>The original look: brown and gold with a green EXP bar.</summary>
        public static readonly Palette Default = new Palette
        {
            Name = "default", DisplayName = "Default (Gold)",
            Border = Rgb(0xA27C47), PanelTop = Rgb(0x26211B), PanelBottom = Rgb(0x13110E),
            HeaderTop = Rgb(0x5B3F23), HeaderBottom = Rgb(0x2F2215), HeaderSheen = Rgba(55, 0xFFE1AA),
            HeaderLine = Rgb(0x0B0907), Title = Rgb(0xEBC47C), NameText = Rgb(0xF6ECDA),
            BadgeTop = Rgb(0x0F0D0B), BadgeBottom = Rgb(0x0F0D0B), BadgeBorder = Rgb(0x6A5334), BadgeText = Rgb(0xEFE4D0),
            TrackTop = Rgb(0x0A0908), TrackBottom = Rgb(0x0A0908), TrackBorder = Rgb(0x624E33),
            Label = Rgb(0xA8977D), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x8C8070), Divider = Rgb(0x3A3026),
            TileTop = Rgb(0xFFC46A), TileMid = Rgb(0xF99E44), TileBottom = Rgb(0xE07E22), TileBorder = Rgb(0x794C0E), TileDigitOutline = Rgb(0x7A3A06),
            FillTop = Rgb(0xECF885), FillMid = Rgb(0xC5E72E), FillBottom = Rgb(0x93C214), FillEdge = Rgb(0xFAFFC8),
            CoinTop = Rgb(0xFFDA74), CoinBottom = Rgb(0xE8801A), CoinRim = Rgb(0x6E3508)
        };

        public static readonly Palette Pink = new Palette
        {
            Name = "pink", DisplayName = "Pink",
            Border = Rgb(0xE07AB0), PanelTop = Rgb(0x2A1B24), PanelBottom = Rgb(0x150D12),
            HeaderTop = Rgb(0x6E2A50), HeaderBottom = Rgb(0x3A1529), HeaderSheen = Rgba(60, 0xFFC0E2),
            HeaderLine = Rgb(0x0D0609), Title = Rgb(0xFFB3DA), NameText = Rgb(0xFCE8F3),
            BadgeTop = Rgb(0x120A0E), BadgeBottom = Rgb(0x120A0E), BadgeBorder = Rgb(0x7A3A5C), BadgeText = Rgb(0xF8E1EE),
            TrackTop = Rgb(0x0C0709), TrackBottom = Rgb(0x0C0709), TrackBorder = Rgb(0x7A3A5C),
            Label = Rgb(0xC995B0), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF9A7A),
            Missing = Rgb(0x8E6F80), Divider = Rgb(0x432634),
            TileTop = Rgb(0xFFC2E2), TileMid = Rgb(0xFF6EB4), TileBottom = Rgb(0xD63A8A), TileBorder = Rgb(0x6E1442), TileDigitOutline = Rgb(0x6E1442),
            FillTop = Rgb(0xFFD1EA), FillMid = Rgb(0xFF7AC0), FillBottom = Rgb(0xD9418F), FillEdge = Rgb(0xFFE6F3),
            CoinTop = Rgb(0xFFD0E8), CoinBottom = Rgb(0xE85AA2), CoinRim = Rgb(0x6E1442)
        };

        public static readonly Palette Red = new Palette
        {
            Name = "red", DisplayName = "Red",
            Border = Rgb(0xD9534A), PanelTop = Rgb(0x2A1716), PanelBottom = Rgb(0x140B0A),
            HeaderTop = Rgb(0x701F1A), HeaderBottom = Rgb(0x3A100D), HeaderSheen = Rgba(60, 0xFFB4AA),
            HeaderLine = Rgb(0x0D0505), Title = Rgb(0xFF9C8F), NameText = Rgb(0xFBE9E6),
            BadgeTop = Rgb(0x120909), BadgeBottom = Rgb(0x120909), BadgeBorder = Rgb(0x7A2E28), BadgeText = Rgb(0xF6E0DC),
            TrackTop = Rgb(0x0C0606), TrackBottom = Rgb(0x0C0606), TrackBorder = Rgb(0x7A2E28),
            Label = Rgb(0xC8948C), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFFB36B),
            Missing = Rgb(0x8E6E6A), Divider = Rgb(0x45221F),
            TileTop = Rgb(0xFFB0A0), TileMid = Rgb(0xF2483A), TileBottom = Rgb(0xB81F17), TileBorder = Rgb(0x5E0C08), TileDigitOutline = Rgb(0x5E0C08),
            FillTop = Rgb(0xFFB4A8), FillMid = Rgb(0xF2564A), FillBottom = Rgb(0xB5231A), FillEdge = Rgb(0xFFDAD4),
            CoinTop = Rgb(0xFFB5A8), CoinBottom = Rgb(0xD9362B), CoinRim = Rgb(0x5E0C08)
        };

        public static readonly Palette Black = new Palette
        {
            Name = "black", DisplayName = "Black",
            Border = Rgb(0x8A8A8A), PanelTop = Rgb(0x171717), PanelBottom = Rgb(0x050505),
            HeaderTop = Rgb(0x303030), HeaderBottom = Rgb(0x121212), HeaderSheen = Rgba(45, 0xFFFFFF),
            HeaderLine = Rgb(0x000000), Title = Rgb(0xE6E6E6), NameText = Rgb(0xFFFFFF),
            BadgeTop = Rgb(0x0A0A0A), BadgeBottom = Rgb(0x0A0A0A), BadgeBorder = Rgb(0x4A4A4A), BadgeText = Rgb(0xEDEDED),
            TrackTop = Rgb(0x000000), TrackBottom = Rgb(0x000000), TrackBorder = Rgb(0x4A4A4A),
            Label = Rgb(0x9A9A9A), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF7A6A),
            Missing = Rgb(0x6A6A6A), Divider = Rgb(0x2C2C2C),
            TileTop = Rgb(0xFFFFFF), TileMid = Rgb(0xCFCFCF), TileBottom = Rgb(0x8C8C8C), TileBorder = Rgb(0x2A2A2A), TileDigitOutline = Rgb(0x1A1A1A),
            FillTop = Rgb(0xFFFFFF), FillMid = Rgb(0xC4C4C4), FillBottom = Rgb(0x808080), FillEdge = Rgb(0xFFFFFF),
            CoinTop = Rgb(0xF0F0F0), CoinBottom = Rgb(0x9A9A9A), CoinRim = Rgb(0x2A2A2A)
        };

        public static readonly Palette Orange = new Palette
        {
            Name = "orange", DisplayName = "Orange",
            Border = Rgb(0xE8923A), PanelTop = Rgb(0x2A1E14), PanelBottom = Rgb(0x140D08),
            HeaderTop = Rgb(0x7A4115), HeaderBottom = Rgb(0x3D1F0A), HeaderSheen = Rgba(60, 0xFFD2A0),
            HeaderLine = Rgb(0x0D0703), Title = Rgb(0xFFC27A), NameText = Rgb(0xFFF0E0),
            BadgeTop = Rgb(0x120C07), BadgeBottom = Rgb(0x120C07), BadgeBorder = Rgb(0x7A4A1E), BadgeText = Rgb(0xF9E6D2),
            TrackTop = Rgb(0x0C0805), TrackBottom = Rgb(0x0C0805), TrackBorder = Rgb(0x7A4A1E),
            Label = Rgb(0xC9A27F), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x8E7A68), Divider = Rgb(0x45301E),
            TileTop = Rgb(0xFFC46A), TileMid = Rgb(0xF99E44), TileBottom = Rgb(0xE07E22), TileBorder = Rgb(0x794C0E), TileDigitOutline = Rgb(0x7A3A06),
            FillTop = Rgb(0xFFD58A), FillMid = Rgb(0xFFA53A), FillBottom = Rgb(0xD9700F), FillEdge = Rgb(0xFFEACC),
            CoinTop = Rgb(0xFFDA74), CoinBottom = Rgb(0xE8801A), CoinRim = Rgb(0x6E3508)
        };

        public static readonly Palette Blue = new Palette
        {
            Name = "blue", DisplayName = "Blue",
            Border = Rgb(0x4A9BE0), PanelTop = Rgb(0x151D2A), PanelBottom = Rgb(0x0A0F17),
            HeaderTop = Rgb(0x1F3F6E), HeaderBottom = Rgb(0x0F203A), HeaderSheen = Rgba(60, 0xAAD2FF),
            HeaderLine = Rgb(0x04070D), Title = Rgb(0x8FC8FF), NameText = Rgb(0xE6F2FF),
            BadgeTop = Rgb(0x080C12), BadgeBottom = Rgb(0x080C12), BadgeBorder = Rgb(0x2E5280), BadgeText = Rgb(0xDDEBFA),
            TrackTop = Rgb(0x05080D), TrackBottom = Rgb(0x05080D), TrackBorder = Rgb(0x2E5280),
            Label = Rgb(0x8FA8C8), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x6A7A8E), Divider = Rgb(0x22324A),
            TileTop = Rgb(0xA8DBFF), TileMid = Rgb(0x4AA3F2), TileBottom = Rgb(0x1E6CC4), TileBorder = Rgb(0x0C2E5E), TileDigitOutline = Rgb(0x0C2E5E),
            FillTop = Rgb(0xB8F0FF), FillMid = Rgb(0x4FC3F7), FillBottom = Rgb(0x1A7FC4), FillEdge = Rgb(0xDDF6FF),
            CoinTop = Rgb(0xBFE4FF), CoinBottom = Rgb(0x3A8EE0), CoinRim = Rgb(0x0C2E5E)
        };

        public static readonly Palette Purple = new Palette
        {
            Name = "purple", DisplayName = "Purple",
            Border = Rgb(0xA86BE0), PanelTop = Rgb(0x221A2C), PanelBottom = Rgb(0x110C17),
            HeaderTop = Rgb(0x4E2A7A), HeaderBottom = Rgb(0x28153F), HeaderSheen = Rgba(60, 0xD9B8FF),
            HeaderLine = Rgb(0x07040C), Title = Rgb(0xD2B0FF), NameText = Rgb(0xF1E8FF),
            BadgeTop = Rgb(0x0E0A14), BadgeBottom = Rgb(0x0E0A14), BadgeBorder = Rgb(0x5B3A82), BadgeText = Rgb(0xEDE3FA),
            TrackTop = Rgb(0x08060C), TrackBottom = Rgb(0x08060C), TrackBorder = Rgb(0x5B3A82),
            Label = Rgb(0xAE98C8), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x7C6E8E), Divider = Rgb(0x352847),
            TileTop = Rgb(0xE0C2FF), TileMid = Rgb(0xA35CF0), TileBottom = Rgb(0x6E2CC0), TileBorder = Rgb(0x351066), TileDigitOutline = Rgb(0x351066),
            FillTop = Rgb(0xE6CCFF), FillMid = Rgb(0xB072F5), FillBottom = Rgb(0x7434C9), FillEdge = Rgb(0xF3E6FF),
            CoinTop = Rgb(0xE6CCFF), CoinBottom = Rgb(0x9B4DE8), CoinRim = Rgb(0x351066)
        };

        public static readonly Palette Green = new Palette
        {
            Name = "green", DisplayName = "Green",
            Border = Rgb(0x5CC46B), PanelTop = Rgb(0x18241A), PanelBottom = Rgb(0x0B120C),
            HeaderTop = Rgb(0x24572D), HeaderBottom = Rgb(0x123018), HeaderSheen = Rgba(60, 0xB8F5C0),
            HeaderLine = Rgb(0x040A05), Title = Rgb(0x9EEBA8), NameText = Rgb(0xE8FBEA),
            BadgeTop = Rgb(0x09100A), BadgeBottom = Rgb(0x09100A), BadgeBorder = Rgb(0x2F6B3A), BadgeText = Rgb(0xE0F5E3),
            TrackTop = Rgb(0x050A06), TrackBottom = Rgb(0x050A06), TrackBorder = Rgb(0x2F6B3A),
            Label = Rgb(0x93BC9A), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x6E8A72), Divider = Rgb(0x23382A),
            TileTop = Rgb(0xC2F5C8), TileMid = Rgb(0x4FCF62), TileBottom = Rgb(0x24963A), TileBorder = Rgb(0x0C4A18), TileDigitOutline = Rgb(0x0C4A18),
            FillTop = Rgb(0xD4FFB0), FillMid = Rgb(0x6FE05C), FillBottom = Rgb(0x2EA33A), FillEdge = Rgb(0xE8FFE0),
            CoinTop = Rgb(0xC8F7B8), CoinBottom = Rgb(0x3FB84F), CoinRim = Rgb(0x0C4A18)
        };

        public static readonly Palette Teal = new Palette
        {
            Name = "teal", DisplayName = "Teal",
            Border = Rgb(0x3CC8BE), PanelTop = Rgb(0x142423), PanelBottom = Rgb(0x091211),
            HeaderTop = Rgb(0x1A5752), HeaderBottom = Rgb(0x0D2E2B), HeaderSheen = Rgba(60, 0xAEF2EC),
            HeaderLine = Rgb(0x030A09), Title = Rgb(0x8DEDE3), NameText = Rgb(0xE3FBF8),
            BadgeTop = Rgb(0x071110), BadgeBottom = Rgb(0x071110), BadgeBorder = Rgb(0x26716A), BadgeText = Rgb(0xDDF5F2),
            TrackTop = Rgb(0x040A09), TrackBottom = Rgb(0x040A09), TrackBorder = Rgb(0x26716A),
            Label = Rgb(0x8EBDB8), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x6A8A87), Divider = Rgb(0x1F3A37),
            TileTop = Rgb(0xB8F5EF), TileMid = Rgb(0x33C7BA), TileBottom = Rgb(0x138A80), TileBorder = Rgb(0x06403B), TileDigitOutline = Rgb(0x06403B),
            FillTop = Rgb(0xC8FFF8), FillMid = Rgb(0x3DDBCB), FillBottom = Rgb(0x12988B), FillEdge = Rgb(0xE4FFFB),
            CoinTop = Rgb(0xC0F5EE), CoinBottom = Rgb(0x2BB3A6), CoinRim = Rgb(0x06403B)
        };

        public static readonly Palette Yellow = new Palette
        {
            Name = "yellow", DisplayName = "Yellow",
            Border = Rgb(0xE8C53A), PanelTop = Rgb(0x26220F), PanelBottom = Rgb(0x121006),
            HeaderTop = Rgb(0x6B5A12), HeaderBottom = Rgb(0x382F08), HeaderSheen = Rgba(60, 0xFFF0A0),
            HeaderLine = Rgb(0x0A0802), Title = Rgb(0xFFE27A), NameText = Rgb(0xFFF8DC),
            BadgeTop = Rgb(0x110F06), BadgeBottom = Rgb(0x110F06), BadgeBorder = Rgb(0x7A6820), BadgeText = Rgb(0xFAF1CF),
            TrackTop = Rgb(0x0A0904), TrackBottom = Rgb(0x0A0904), TrackBorder = Rgb(0x7A6820),
            Label = Rgb(0xC9B97F), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x8E8566), Divider = Rgb(0x3F381C),
            TileTop = Rgb(0xFFF2A8), TileMid = Rgb(0xF7D23A), TileBottom = Rgb(0xC9A010), TileBorder = Rgb(0x5E4A04), TileDigitOutline = Rgb(0x5E4A04),
            FillTop = Rgb(0xFFF6B0), FillMid = Rgb(0xFFD93D), FillBottom = Rgb(0xD9A70F), FillEdge = Rgb(0xFFFBE0),
            CoinTop = Rgb(0xFFF0A0), CoinBottom = Rgb(0xE6B820), CoinRim = Rgb(0x5E4A04)
        };

        public static readonly Palette Lavender = new Palette
        {
            Name = "lavender", DisplayName = "Lavender",
            Border = Rgb(0xB9A3F0), PanelTop = Rgb(0x1E1C29), PanelBottom = Rgb(0x0F0E15),
            HeaderTop = Rgb(0x3E3767), HeaderBottom = Rgb(0x211D38), HeaderSheen = Rgba(60, 0xE2DAFF),
            HeaderLine = Rgb(0x05040A), Title = Rgb(0xD6CCFF), NameText = Rgb(0xF3F0FF),
            BadgeTop = Rgb(0x0C0B12), BadgeBottom = Rgb(0x0C0B12), BadgeBorder = Rgb(0x4D4680), BadgeText = Rgb(0xECE8FA),
            TrackTop = Rgb(0x07060B), TrackBottom = Rgb(0x07060B), TrackBorder = Rgb(0x4D4680),
            Label = Rgb(0xA9A2C9), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x77728E), Divider = Rgb(0x2E2B45),
            TileTop = Rgb(0xEEE8FF), TileMid = Rgb(0xB8A6F5), TileBottom = Rgb(0x8A74D9), TileBorder = Rgb(0x3A2C7A), TileDigitOutline = Rgb(0x3A2C7A),
            FillTop = Rgb(0xF0EBFF), FillMid = Rgb(0xC3B2FA), FillBottom = Rgb(0x8F78E0), FillEdge = Rgb(0xFAF8FF),
            CoinTop = Rgb(0xEEE8FF), CoinBottom = Rgb(0xA08CEB), CoinRim = Rgb(0x3A2C7A)
        };

        /// <summary>Neutral dark panel; border, title, level tiles and EXP bar in rainbow colors.</summary>
        public static readonly Palette RainbowTheme = new Palette
        {
            Name = "rainbow", DisplayName = "Rainbow", Rainbow = true,
            Border = Rgb(0xFFFFFF), PanelTop = Rgb(0x1C1C25), PanelBottom = Rgb(0x0B0B10),
            HeaderTop = Rgb(0x2E2E3C), HeaderBottom = Rgb(0x16161F), HeaderSheen = Rgba(50, 0xFFFFFF),
            HeaderLine = Rgb(0x000000), Title = Rgb(0xFFFFFF), NameText = Rgb(0xFFFFFF),
            BadgeTop = Rgb(0x0B0B10), BadgeBottom = Rgb(0x0B0B10), BadgeBorder = Rgb(0x4A4A5A), BadgeText = Rgb(0xFFFFFF),
            TrackTop = Rgb(0x06060A), TrackBottom = Rgb(0x06060A), TrackBorder = Rgb(0x4A4A5A),
            Label = Rgb(0xB0B0C0), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x707080), Divider = Rgb(0x2C2C38),
            TileTop = Rgb(0xFFFFFF), TileMid = Rgb(0xCCCCCC), TileBottom = Rgb(0x999999), TileBorder = Rgb(0x333333), TileDigitOutline = Rgb(0x222222),
            FillTop = Rgb(0xFFFFFF), FillMid = Rgb(0xCCCCCC), FillBottom = Rgb(0x999999), FillEdge = Rgb(0xFFFFFF),
            CoinTop = Rgb(0xFF7A7A), CoinBottom = Rgb(0x6B8BFF), CoinRim = Rgb(0x222244)
        };

        public static readonly Palette[] All =
        {
            Default, Pink, Red, Black, Orange, Blue, Purple, Green, Teal, Yellow, Lavender, RainbowTheme
        };

        public static Palette ByName(string name)
        {
            foreach (Palette p in All)
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p;
            return Default;
        }
    }

    /// <summary>Draws the tracker panel into a transparent bitmap for a per-pixel-alpha window.</summary>
    public sealed class OverlayRenderer
    {
        public const int BaseWidth = 384;
        public const int BaseHeight = 178;
        const float Margin = 6f;         // room for the drop shadow
        const float PanelW = 372f;
        const float PanelH = 166f;
        const float HeaderH = 26f;
        const float RowTop = 35f;
        const float RowH = 21f;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string SansName = PickFamily(new[] { "Segoe UI", "Tahoma", "Arial" });
        static readonly string HeavyName = PickFamily(new[] { "Segoe UI Black", "Arial Black", SansName });
        static readonly FontStyle HeavyStyle = HeavyName == SansName ? FontStyle.Bold : FontStyle.Regular;
        static readonly FontFamily Sans = new FontFamily(SansName);
        static readonly FontFamily Heavy = new FontFamily(HeavyName);
        static readonly StringFormat Typo = MakeTypo();

        static readonly Font HeaderFont = new Font(Sans, 10.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font NameFont = new Font(Sans, 11.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font LvFont = new Font(Sans, 10.5f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Pixel);
        static readonly Font LabelFont = new Font(Sans, 9f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font ValueFont = new Font(Sans, 17f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font CreditFont = new Font(Sans, 9f, FontStyle.Regular, GraphicsUnit.Pixel);
        static readonly Font CreditNameFont = new Font(Sans, 9f, FontStyle.Bold, GraphicsUnit.Pixel);

        Palette pal = Palette.Default;

        public Palette Palette
        {
            get { return pal; }
            set { pal = value ?? Palette.Default; }
        }

        /// <summary>Clickable areas from the last render, in unscaled window coordinates.</summary>
        public RectangleF LevelArea { get; private set; }
        public RectangleF NameArea { get; private set; }
        public RectangleF SettingsArea { get; private set; }

        public Bitmap Render(OverlayView v, float scale)
        {
            int w = (int)Math.Ceiling(BaseWidth * scale), h = (int)Math.Ceiling(BaseHeight * scale);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.ScaleTransform(scale, scale);
                DrawShadow(g);
                g.TranslateTransform(Margin, Margin);
                DrawPanel(g);
                DrawHeader(g, v);
                float barLeft = DrawLevel(g, v);
                DrawBar(g, v, barLeft);
                DrawStats(g, v);
                DrawCredit(g);
            }
            return bmp;
        }

        static void DrawShadow(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(16, 0, 0, 0)))
            {
                for (int i = 6; i >= 1; i--)
                {
                    var r = new RectangleF(Margin - i + 0.5f, Margin - i + 2f, PanelW + 2 * i - 1, PanelH + 2 * i - 2);
                    using (GraphicsPath p = Shapes.RoundRect(r, 9f + i)) g.FillPath(b, p);
                }
            }
        }

        void DrawPanel(Graphics g)
        {
            var panel = new RectangleF(0, 0, PanelW, PanelH);
            using (GraphicsPath shape = Shapes.RoundRect(panel, 8f))
            {
                using (LinearGradientBrush b = Shapes.Vertical(panel, pal.PanelTop, pal.PanelBottom))
                    g.FillPath(b, shape);

                GraphicsState state = g.Save();
                g.SetClip(shape, CombineMode.Intersect);
                var header = new RectangleF(0, 0, PanelW, HeaderH);
                using (LinearGradientBrush b = Shapes.Vertical(header, pal.HeaderTop, pal.HeaderBottom))
                    g.FillRectangle(b, header);
                using (var p = new Pen(pal.HeaderSheen, 1f)) g.DrawLine(p, 0, 1.5f, PanelW, 1.5f);
                using (var p = new Pen(pal.HeaderLine, 1.5f)) g.DrawLine(p, 0, HeaderH, PanelW, HeaderH);
                g.Restore(state);

                using (Brush border = AccentBrush(panel, pal.Border))
                using (var p = new Pen(border, pal.Rainbow ? 1.8f : 1.6f))
                    g.DrawPath(p, shape);
            }
        }

        /// <summary>A solid brush, or a left-to-right rainbow across <paramref name="r"/> for the Rainbow theme.</summary>
        Brush AccentBrush(RectangleF r, Color color)
        {
            return pal.Rainbow ? (Brush)RainbowBrush(r) : new SolidBrush(color);
        }

        static LinearGradientBrush RainbowBrush(RectangleF r)
        {
            var b = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y, Math.Max(2, r.Width + 2), Math.Max(1, r.Height)),
                Palette.RainbowColors[0], Palette.RainbowColors[5], LinearGradientMode.Horizontal);
            var blend = new ColorBlend(6);
            blend.Colors = Palette.RainbowColors;
            blend.Positions = new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f };
            b.InterpolationColors = blend;
            return b;
        }

        void DrawHeader(Graphics g, OverlayView v)
        {
            float cy = HeaderH / 2;
            DrawCoin(g, 14f, cy, 5.5f);
            using (Brush b = AccentBrush(new RectangleF(25f, 0, 112f, HeaderH), pal.Title)) DrawSpaced(g, "EXP TRACKER", HeaderFont, b, 25f, cy, 1.1f);

            // Settings button in the top-right corner.
            var button = new RectangleF(PanelW - 29f, 3f, 22f, 20f);
            DrawGear(g, button, v.SettingsHover);
            SettingsArea = new RectangleF(button.X + Margin, button.Y + Margin, button.Width, button.Height);

            float right = button.X - 7f;
            string name = Fit(g, v.Name ?? "", NameFont, 140f);
            float nameW = name.Length > 0 ? Measure(g, name, NameFont) : 0f;
            if (name.Length > 0)
                using (var b = new SolidBrush(pal.NameText)) g.DrawString(name, NameFont, b, right - nameW, TextTop(NameFont, cy), Typo);

            float dotX = right - nameW - (name.Length > 0 ? 9f : 3f);
            DrawStatusDot(g, dotX, cy, v.Status);
            float left = Math.Min(dotX - 8f, right - 70f);
            NameArea = new RectangleF(Margin + left, Margin, right - left + 4f, HeaderH);
        }

        void DrawGear(Graphics g, RectangleF button, bool hover)
        {
            using (GraphicsPath p = Shapes.RoundRect(button, 4f))
            {
                using (var b = new SolidBrush(hover ? Color.FromArgb(70, pal.Title) : Color.FromArgb(40, 0, 0, 0))) g.FillPath(b, p);
                using (var pen = new Pen(hover ? pal.Title : Color.FromArgb(110, pal.Title), 1f)) g.DrawPath(pen, p);
            }
            float cx = button.X + button.Width / 2, cy = button.Y + button.Height / 2;
            const int teeth = 8;
            const float outer = 6.4f, inner = 4.7f, hole = 2f;
            var points = new PointF[teeth * 4];
            for (int i = 0; i < teeth; i++)
            {
                double a = 2 * Math.PI * i / teeth, step = 2 * Math.PI / teeth;
                double[] angles = { a - step * 0.30, a - step * 0.14, a + step * 0.14, a + step * 0.30 };
                float[] radii = { inner, outer, outer, inner };
                for (int k = 0; k < 4; k++)
                    points[i * 4 + k] = new PointF(cx + (float)(Math.Cos(angles[k]) * radii[k]), cy + (float)(Math.Sin(angles[k]) * radii[k]));
            }
            using (var gear = new GraphicsPath(FillMode.Alternate))
            {
                gear.AddPolygon(points);
                gear.AddEllipse(cx - hole, cy - hole, hole * 2, hole * 2);
                using (var pen = new Pen(Color.FromArgb(200, 20, 14, 8), 1.6f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, gear);
                using (var b = new SolidBrush(hover ? Lighten(pal.Title, 0.45f) : pal.Title)) g.FillPath(b, gear);
            }
        }

        float DrawLevel(Graphics g, OverlayView v)
        {
            const float tileW = 13f, tileH = 17f, gap = 2f;
            string digits = v.Level > 0 ? v.Level.ToString(Inv) : "?";
            float lvW = Measure(g, "LV.", LvFont);
            float width = 7f + lvW + 5f + digits.Length * tileW + (digits.Length - 1) * gap + 4f;
            var badge = new RectangleF(10f, RowTop, width, RowH);

            using (GraphicsPath p = Shapes.RoundRect(badge, 4f))
            {
                using (LinearGradientBrush b = Shapes.Vertical(badge, pal.BadgeTop, pal.BadgeBottom)) g.FillPath(b, p);
                using (var pen = new Pen(pal.BadgeBorder, 1f)) g.DrawPath(pen, p);
            }
            using (var b = new SolidBrush(pal.BadgeText))
                g.DrawString("LV.", LvFont, b, badge.X + 7f, TextTop(LvFont, RowTop + RowH / 2), Typo);

            float x = badge.X + 7f + lvW + 5f, y = RowTop + (RowH - tileH) / 2;
            for (int i = 0; i < digits.Length; i++)
            {
                DrawTile(g, new RectangleF(x, y, tileW, tileH), digits[i].ToString(), i);
                x += tileW + gap;
            }
            LevelArea = new RectangleF(badge.X + Margin, badge.Y + Margin, badge.Width, badge.Height);
            return badge.Right;
        }

        void DrawTile(Graphics g, RectangleF r, string digit, int index)
        {
            Color top = pal.TileTop, mid = pal.TileMid, bottom = pal.TileBottom, border = pal.TileBorder, outline = pal.TileDigitOutline;
            if (pal.Rainbow)
            {
                // Each digit gets its own rainbow color.
                Color c = Palette.RainbowColors[(index * 2) % Palette.RainbowColors.Length];
                top = Lighten(c, 0.5f);
                mid = c;
                bottom = Darken(c, 0.25f);
                border = Darken(c, 0.6f);
                outline = Darken(c, 0.65f);
            }
            using (GraphicsPath p = Shapes.RoundRect(r, 3f))
            {
                using (LinearGradientBrush b = Shapes.Vertical(r, top, mid, bottom, 0.5f))
                    g.FillPath(b, p);
                using (var pen = new Pen(border, 1f)) g.DrawPath(pen, p);
            }
            var gloss = new RectangleF(r.X + 1.5f, r.Y + 1.2f, r.Width - 3f, r.Height * 0.42f);
            using (GraphicsPath p = Shapes.RoundRect(gloss, 2f))
            using (var b = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
                g.FillPath(b, p);
            DrawOutlined(g, digit, Heavy, HeavyStyle, 13.5f, r.X + r.Width / 2, r.Y + r.Height / 2 + 0.3f,
                Color.FromArgb(0xFF, 0xFC, 0xED), outline, 2.2f);
        }

        void DrawBar(Graphics g, OverlayView v, float left)
        {
            float x0 = left + 7f, x1 = PanelW - 10f;
            var track = new RectangleF(x0, RowTop, x1 - x0, RowH);
            using (GraphicsPath p = Shapes.RoundRect(track, 4f))
            using (LinearGradientBrush b = Shapes.Vertical(track, pal.TrackTop, pal.TrackBottom))
                g.FillPath(b, p);

            double pct = v.Percent;
            if (v.Message == null && !double.IsNaN(pct) && pct > 0)
            {
                float inner = track.Width - 4f;
                float fw = Math.Max(3f, (float)(inner * Math.Min(1.0, pct / 100.0)));
                var fill = new RectangleF(x0 + 2f, RowTop + 2f, fw, RowH - 4f);
                using (GraphicsPath fp = Shapes.RoundRect(fill, 2.5f))
                {
                    if (pal.Rainbow)
                    {
                        // The rainbow spans the whole bar, so more colors show as the level fills up.
                        using (LinearGradientBrush b = RainbowBrush(new RectangleF(x0 + 2f, fill.Y, inner, fill.Height)))
                            g.FillPath(b, fp);
                        using (LinearGradientBrush b = Shapes.Vertical(fill, Color.FromArgb(0, 0, 0, 0), Color.FromArgb(80, 0, 0, 0)))
                            g.FillPath(b, fp);
                    }
                    else
                    {
                        using (LinearGradientBrush b = Shapes.Vertical(fill, pal.FillTop, pal.FillMid, pal.FillBottom, 0.4f))
                            g.FillPath(b, fp);
                    }
                    GraphicsState state = g.Save();
                    g.SetClip(fp, CombineMode.Intersect);
                    var gloss = new RectangleF(fill.X, fill.Y, fill.Width, fill.Height * 0.45f);
                    using (LinearGradientBrush b = Shapes.Vertical(gloss, Color.FromArgb(110, 255, 255, 255), Color.FromArgb(10, 255, 255, 255)))
                        g.FillRectangle(b, gloss);
                    using (var pen = new Pen(Color.FromArgb(150, pal.FillEdge), 1f))
                        g.DrawLine(pen, fill.Right - 0.5f, fill.Y, fill.Right - 0.5f, fill.Bottom);
                    g.Restore(state);
                    using (var pen = new Pen(Color.FromArgb(90, 0, 0, 0), 1f)) g.DrawPath(pen, fp);
                }
            }
            using (GraphicsPath p = Shapes.RoundRect(track, 4f))
            using (var pen = new Pen(pal.TrackBorder, 1f))
                g.DrawPath(pen, p);

            float cx = track.X + track.Width / 2, cy = track.Y + track.Height / 2;
            Color outline = Color.FromArgb(235, 20, 16, 12);
            if (v.Message != null)
            {
                DrawOutlined(g, v.Message, Sans, FontStyle.Bold, 10.5f, cx, cy, Color.FromArgb(0xF6, 0xF0, 0xE4), outline, 2.4f);
            }
            else if (v.HasExp)
            {
                // "104,741 / 356,916 [29.35%]", shortened to "4.52B / 9.48B [47.62%]" when it doesn't fit.
                string percent = double.IsNaN(pct) ? "" : " [" + pct.ToString("0.00", Inv) + "%]";
                string approx = v.LevelTotalExact ? "" : "~";
                var options = new System.Collections.Generic.List<string>();
                if (v.LevelTotal > 0)
                {
                    options.Add(v.Exp.ToString("N0", Inv) + " / " + approx + v.LevelTotal.ToString("N0", Inv) + percent);
                    options.Add(Fmt.Short(v.Exp) + " / " + approx + Fmt.Short(v.LevelTotal) + percent);
                }
                options.Add(v.Exp.ToString("N0", Inv) + percent);
                DrawExpText(g, options, cx, cy, track.Width - 12f, outline);
            }
        }

        /// <summary>Draws "EXP" and the first of <paramref name="options"/> that fits in <paramref name="maxWidth"/>.</summary>
        static void DrawExpText(Graphics g, System.Collections.Generic.List<string> options, float cx, float cy, float maxWidth, Color outline)
        {
            using (var label = new GraphicsPath())
            using (var number = new GraphicsPath())
            {
                const float gap = 5f;
                label.AddString("EXP", Sans, (int)(FontStyle.Bold | FontStyle.Italic), 10.5f, PointF.Empty, Typo);
                RectangleF lb = label.GetBounds(), nb = RectangleF.Empty;
                foreach (string value in options)
                {
                    number.Reset();
                    number.AddString(value, Sans, (int)FontStyle.Bold, 11f, PointF.Empty, Typo);
                    nb = number.GetBounds();
                    if (lb.Width + gap + nb.Width <= maxWidth) break;
                }
                float left = cx - (lb.Width + gap + nb.Width) / 2;
                float dy = cy - (lb.Y + lb.Height / 2);
                using (var m = new Matrix())
                {
                    m.Translate(left - lb.X, dy);
                    label.Transform(m);
                }
                using (var m = new Matrix())
                {
                    m.Translate(left + lb.Width + gap - nb.X, dy);
                    number.Transform(m);
                }
                using (var pen = new Pen(outline, 2.6f) { LineJoin = LineJoin.Round })
                {
                    g.DrawPath(pen, label);
                    g.DrawPath(pen, number);
                }
                using (var b = new SolidBrush(Color.FromArgb(0xFF, 0xF4, 0xD2))) g.FillPath(b, label);
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, number);
            }
        }

        /// <summary>Two rows of three: rates on top, session and level progress below.</summary>
        void DrawStats(Graphics g, OverlayView v)
        {
            string[] labels = { "LAST 5 MIN", "EXP/H - 30M", "EXP/H - 1H", "THIS SESSION", "SESSION TIME", v.EtaLabel };
            string[] values = { v.Recent, v.Rate, v.RateHour, v.Session, v.SessionTime, v.Eta };
            bool[] negative = { v.RecentNegative, false, false, v.SessionNegative, false, false };
            const float rowGap = 40f;
            float colW = (PanelW - 12f) / 3f;
            using (var sep = new Pen(pal.Divider, 1f))
            using (var labelBrush = new SolidBrush(pal.Label))
            using (var shadow = new SolidBrush(pal.ValueShadow))
            {
                g.DrawLine(sep, 8f, 64.5f, PanelW - 8f, 64.5f);
                g.DrawLine(sep, 14f, 64.5f + rowGap, PanelW - 14f, 64.5f + rowGap);
                for (int i = 0; i < 6; i++)
                {
                    float x = 6f + (i % 3) * colW, dy = (i / 3) * rowGap;
                    if (i % 3 > 0) g.DrawLine(sep, x + 0.5f, 72f + dy, x + 0.5f, 100f + dy);
                    DrawSpaced(g, labels[i], LabelFont, labelBrush, x + 10f, 76f + dy, 0.5f);

                    Color c = negative[i] ? pal.Negative : values[i] == OverlayView.Dash ? pal.Missing : pal.Value;
                    float top = TextTop(ValueFont, 93f + dy);
                    g.DrawString(values[i], ValueFont, shadow, x + 10f, top + 1.2f, Typo);
                    using (var b = new SolidBrush(c)) g.DrawString(values[i], ValueFont, b, x + 9f, top, Typo);
                }
            }
        }

        void DrawCredit(Graphics g)
        {
            const float y = PanelH - 10.5f;
            using (var pen = new Pen(pal.Divider, 1f)) g.DrawLine(pen, 8f, PanelH - 20.5f, PanelW - 8f, PanelH - 20.5f);
            const string by = "Created by ", name = "Aydrian";
            float w1 = Measure(g, by, CreditFont), w2 = Measure(g, name, CreditNameFont);
            float x = (PanelW - w1 - w2) / 2;
            using (var b = new SolidBrush(pal.Missing)) g.DrawString(by, CreditFont, b, x, TextTop(CreditFont, y), Typo);
            using (Brush b = AccentBrush(new RectangleF(x + w1, y - 8f, w2, 16f), pal.Title))
                g.DrawString(name, CreditNameFont, b, x + w1, TextTop(CreditNameFont, y), Typo);
        }

        static Color Lighten(Color c, float amount)
        {
            return Color.FromArgb(c.A, c.R + (int)((255 - c.R) * amount), c.G + (int)((255 - c.G) * amount), c.B + (int)((255 - c.B) * amount));
        }

        static Color Darken(Color c, float amount)
        {
            return Color.FromArgb(c.A, (int)(c.R * (1 - amount)), (int)(c.G * (1 - amount)), (int)(c.B * (1 - amount)));
        }

        /// <summary>A small color sample of a theme, for the Theme menu.</summary>
        public static Bitmap Swatch(Palette p, int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new RectangleF(1.5f, 1.5f, size - 3f, size - 3f);
                using (GraphicsPath path = Shapes.RoundRect(r, size / 5f))
                {
                    using (Brush b = p.Rainbow ? (Brush)RainbowBrush(r) : Shapes.Vertical(r, p.FillTop, p.FillMid, p.FillBottom, 0.45f))
                        g.FillPath(b, path);
                    using (var pen = new Pen(p.Rainbow ? Color.FromArgb(0x33, 0x33, 0x33) : p.TileBorder, 1f)) g.DrawPath(pen, path);
                }
            }
            return bmp;
        }

        void DrawCoin(Graphics g, float cx, float cy, float r)
        {
            var rect = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
            using (LinearGradientBrush b = Shapes.Vertical(rect, pal.CoinTop, pal.CoinBottom))
                g.FillEllipse(b, rect);
            using (var pen = new Pen(pal.CoinRim, 1f)) g.DrawEllipse(pen, rect);
            RectangleF inner = RectangleF.Inflate(rect, -2.2f, -2.2f);
            using (var pen = new Pen(Color.FromArgb(150, 255, 244, 200), 0.8f)) g.DrawEllipse(pen, inner);
        }

        static void DrawStatusDot(Graphics g, float cx, float cy, OverlayStatus status)
        {
            Color c;
            switch (status)
            {
                case OverlayStatus.Live: c = Color.FromArgb(0x86, 0xE0, 0x4E); break;
                case OverlayStatus.NoRegion: c = Color.FromArgb(0xE0, 0x56, 0x3E); break;
                case OverlayStatus.Paused: c = Color.FromArgb(0x9E, 0x98, 0x90); break;
                default: c = Color.FromArgb(0xF0, 0xB0, 0x38); break;
            }
            using (var b = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) g.FillEllipse(b, cx - 4f, cy - 4f, 8f, 8f);
            using (var b = new SolidBrush(c)) g.FillEllipse(b, cx - 3f, cy - 3f, 6f, 6f);
            using (var b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(b, cx - 1.8f, cy - 2.2f, 2.2f, 2.2f);
        }

        static void DrawOutlined(Graphics g, string text, FontFamily family, FontStyle style, float size,
            float cx, float cy, Color fill, Color outline, float width)
        {
            using (var path = new GraphicsPath())
            {
                path.AddString(text, family, (int)style, size, PointF.Empty, Typo);
                RectangleF b = path.GetBounds();
                using (var m = new Matrix())
                {
                    m.Translate(cx - (b.X + b.Width / 2), cy - (b.Y + b.Height / 2));
                    path.Transform(m);
                }
                using (var pen = new Pen(outline, width) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            }
        }

        /// <summary>Draws text with extra letter spacing, vertically centred on the cap height.</summary>
        static void DrawSpaced(Graphics g, string text, Font font, Brush brush, float x, float centerY, float spacing)
        {
            float y = TextTop(font, centerY);
            foreach (char ch in text)
            {
                string s = ch.ToString();
                g.DrawString(s, font, brush, x, y, Typo);
                x += Measure(g, s, font) + spacing;
            }
        }

        /// <summary>The y to draw at so that capitals and digits are centred on <paramref name="centerY"/>.</summary>
        static float TextTop(Font font, float centerY)
        {
            FontFamily f = font.FontFamily;
            float ascent = font.Size * f.GetCellAscent(font.Style) / f.GetEmHeight(font.Style);
            return centerY - (ascent - font.Size * 0.36f);
        }

        static float Measure(Graphics g, string text, Font font)
        {
            return g.MeasureString(text, font, PointF.Empty, Typo).Width;
        }

        static string Fit(Graphics g, string text, Font font, float maxWidth)
        {
            if (Measure(g, text, font) <= maxWidth) return text;
            while (text.Length > 1 && Measure(g, text + "…", font) > maxWidth) text = text.Substring(0, text.Length - 1);
            return text + "…";
        }

        static StringFormat MakeTypo()
        {
            var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            return sf;
        }

        static string PickFamily(string[] names)
        {
            using (var installed = new InstalledFontCollection())
            {
                foreach (string name in names)
                    foreach (FontFamily f in installed.Families)
                        if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f.Name;
            }
            return FontFamily.GenericSansSerif.Name;
        }
    }
}
