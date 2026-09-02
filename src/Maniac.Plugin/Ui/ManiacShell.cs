using Maniac;
using Maniac.Core;
using Maniac.Core.Settings;
using Maniac.Core.Status;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;

namespace Maniac.Plugin.Ui;

public partial class ManiacShell : CompositeDrawable
{
    public const double StatusIntervalMs = 100;

    readonly SettingsStore settings;
    readonly Func<AutoplayStatus> status;
    readonly Func<string> extraDiagnostic;
    readonly Container body;
    readonly Container pageHost;
    readonly Drawable[] pages;
    readonly SpriteText compactStatus;
    readonly StatusLine diagLine;
    readonly StatusLine playLine;
    readonly PanelButton[] nav;
    readonly ToggleRow autoRow;
    readonly ToggleRow osuRow;
    readonly ToggleRow maniaRow;
    readonly SliderRow maniaSkill;
    readonly SliderRow maniaPressure;
    readonly SliderRow maniaChord;
    readonly SliderRow maniaMisses;
    readonly SliderRow maniaTapLength;
    readonly SliderRow maniaEarlyLate;
    readonly SpriteText? expiryHint;
    readonly SpriteText? expiryDiagHint;
    double lastStatusAt;
    string lastPlayText = "";
    string lastDiagText = "";

    public ManiacShell(SettingsStore store, Func<AutoplayStatus> statusOf, Func<string> diagnosticOf)
    {
        settings = store;
        status = statusOf;
        extraDiagnostic = diagnosticOf;
        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        Margin = new MarginPadding(12);
        AutoSizeAxes = Axes.Both;
        AlwaysPresent = true;
        Depth = float.MinValue;
        Name = "maniac-overlay";

        nav =
        [
            new PanelButton("General", () => ShowPage(0), 78),
            new PanelButton("osu!", () => ShowPage(1), 64),
            new PanelButton("mania", () => ShowPage(2), 70),
            new PanelButton("Diag", () => ShowPage(3), 60),
        ];

        autoRow = new ToggleRow("Autoplay", store.Current.AutoplayEnabled && !BuildExpiry.Current.IsExpired(), v =>
        {
            if (BuildExpiry.Current.IsExpired())
                return;
            store.Update(s => s.AutoplayEnabled = v);
        });
        expiryHint = MakeExpiryHint();
        expiryDiagHint = MakeExpiryHint();
        osuRow = new ToggleRow("osu!standard driver", store.Current.Osu.Enabled, v => store.Update(s => s.Osu.Enabled = v));
        maniaRow = new ToggleRow("mania driver", store.Current.Mania.Enabled, v => store.Update(s => s.Mania.Enabled = v));
        var mania = store.Current.Mania;
        maniaSkill = new SliderRow("Skill", 0, 100, 1, mania.Skill, v => store.Update(s => s.Mania.Skill = v), v => $"{v:0}");
        maniaPressure = new SliderRow("Pressure", 0, 3, 0.05, mania.Pressure, v => store.Update(s => s.Mania.Pressure = v), v => $"{v:0.00}×");
        maniaChord = new SliderRow("Chord roll", 0, 3, 0.05, mania.ChordRoll, v => store.Update(s => s.Mania.ChordRoll = v), v => $"{v:0.00}×");
        maniaMisses = new SliderRow("Misses", 0, 3, 0.05, mania.Misses, v => store.Update(s => s.Mania.Misses = v), v => $"{v:0.00}×");
        maniaTapLength = new SliderRow("Tap length", 20, 120, 1, mania.TapLength, v => store.Update(s => s.Mania.TapLength = v), v => $"{v:0} ms");
        maniaEarlyLate = new SliderRow("Early/late", -20, 20, 0.5, mania.EarlyLate, v => store.Update(s => s.Mania.EarlyLate = v), v => $"{v:+0.0;-0.0;0} ms");
        playLine = new StatusLine();
        diagLine = new StatusLine();
        compactStatus = new SpriteText
        {
            Text = "maniac",
            Font = FontUsage.Default.With(size: 12),
            Colour = Colour4.White.Opacity(0.75f),
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
        };

        pages =
        [
            UiLayout.Column(
                UiLayout.Heading("General"),
                expiryHint,
                autoRow,
                new KeybindRow("Toggle panel", store.Current.PanelHotkey, v => store.Update(s => s.PanelHotkey = v)),
                new KeybindRow("Toggle autoplay", store.Current.AutoplayHotkey, v => store.Update(s => s.AutoplayHotkey = v)),
                UiLayout.Hint("Collapsed panel does not intercept gameplay input. Settings are saved to %APPDATA%\\maniac-lazer\\settings.json.")),
            UiLayout.Column(
                UiLayout.Heading("osu!standard"),
                osuRow,
                UiLayout.Hint("Timing and movement humanization will be configured here in a later pass.")),
            UiLayout.Column(
                UiLayout.Heading("mania"),
                maniaRow,
                maniaSkill,
                UiLayout.Hint("100 is top-level timing on the current map; 0 is mid-tier."),
                UiLayout.Hint("Advanced — multipliers and offsets on top of skill."),
                maniaPressure,
                maniaChord,
                maniaMisses,
                maniaTapLength,
                maniaEarlyLate),
            UiLayout.Column(
                UiLayout.Heading("Diagnostics"),
                new StatusLine { Text = $"plugin {File.GetLastWriteTime(typeof(ManiacShell).Assembly.Location):HH:mm:ss}" },
                expiryDiagHint,
                playLine,
                diagLine,
                UiLayout.Hint(string.Join(" · ", GameBindings.LastProbe.Ok
                    ? GameBindings.LastProbe.Resolved
                    : GameBindings.LastProbe.Failures))),
        ];

        pageHost = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = pages,
        };

        body = new Container
        {
            Width = 340,
            AutoSizeAxes = Axes.Y,
            Padding = new MarginPadding { Horizontal = 12, Bottom = 12 },
            Child = UiLayout.Column(UiLayout.Row(nav), pageHost),
        };

        InternalChild = new Container
        {
            AutoSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 8,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.Black.Opacity(0.84f) },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        CompactBar(),
                        body,
                    },
                },
            },
        };

        ApplyExpanded(store.Current.General.PanelExpanded);
        ShowPage(0);
    }

    Drawable CompactBar() => new FillFlowContainer
    {
        AutoSizeAxes = Axes.Both,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(6, 0),
        Padding = new MarginPadding { Horizontal = 10, Vertical = 8 },
        Children = new Drawable[]
        {
            new SpriteText
            {
                Text = "maniac",
                Font = FontUsage.Default.With(size: 16, weight: "Bold"),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            },
            compactStatus,
            new PanelButton("Auto", ToggleAuto, 56),
            new PanelButton("Panel", ToggleExpanded, 64),
            new PanelButton("Reload", Session.RequestReload, 64),
            new PanelButton("Unload", Session.RequestUnload, 64),
        },
    };

    public void ToggleExpanded()
    {
        bool expanded = !body.IsPresent;
        settings.Update(s => s.General.PanelExpanded = expanded);
        ApplyExpanded(expanded);
    }

    public void ToggleAuto()
    {
        if (BuildExpiry.Current.IsExpired())
            return;
        settings.Update(s => s.AutoplayEnabled = !s.AutoplayEnabled);
    }

    public void SetExpanded(bool expanded)
    {
        settings.Update(s => s.General.PanelExpanded = expanded);
        ApplyExpanded(expanded);
    }

    void ApplyExpanded(bool expanded)
    {
        if (expanded)
            body.Show();
        else
            body.Hide();
    }

    void ShowPage(int index)
    {
        for (int i = 0; i < nav.Length; i++)
            nav[i].SetActive(i == index);
        for (int i = 0; i < pages.Length; i++)
        {
            if (i == index)
                pages[i].Show();
            else
                pages[i].Hide();
        }
    }

    protected override void Update()
    {
        base.Update();
        if (Time.Current - lastStatusAt < StatusIntervalMs)
            return;
        lastStatusAt = Time.Current;

        var snap = status();
        bool expired = BuildExpiry.Current.IsExpired();
        autoRow.SetValue(settings.Current.AutoplayEnabled && !expired);
        if (expiryHint != null)
            expiryHint.Text = BuildExpiry.Current.Describe(DateTimeOffset.UtcNow);
        if (expiryDiagHint != null)
            expiryDiagHint.Text = expiryHint?.Text ?? BuildExpiry.Current.Describe(DateTimeOffset.UtcNow);
        osuRow.SetValue(settings.Current.Osu.Enabled);
        maniaRow.SetValue(settings.Current.Mania.Enabled);
        maniaSkill.SetValue(settings.Current.Mania.Skill);
        maniaPressure.SetValue(settings.Current.Mania.Pressure);
        maniaChord.SetValue(settings.Current.Mania.ChordRoll);
        maniaMisses.SetValue(settings.Current.Mania.Misses);
        maniaTapLength.SetValue(settings.Current.Mania.TapLength);
        maniaEarlyLate.SetValue(settings.Current.Mania.EarlyLate);

        string play = expired ? "expired" : FormatPlay(snap);
        if (play != lastPlayText)
        {
            lastPlayText = play;
            compactStatus.Text = play;
            playLine.Text = play;
        }

        string diag = snap.Diagnostic;
        if (string.IsNullOrEmpty(diag))
            diag = extraDiagnostic();
        if (!snap.ProbeOk)
            diag = string.IsNullOrEmpty(diag) ? "startup probe failed" : diag;
        if (diag != lastDiagText)
        {
            lastDiagText = diag;
            diagLine.Text = string.IsNullOrEmpty(diag) ? "ok" : diag;
        }
    }

    static string FormatPlay(in AutoplayStatus snap)
    {
        string mode = !snap.Enabled ? "off" : snap.Blocked ? "paused" : "on";
        string ruleset = string.IsNullOrEmpty(snap.Ruleset) ? snap.Screen : snap.Ruleset;
        if (snap.Total == 0)
            return $"{mode} · {ruleset}";
        string keys = snap.Columns > 0 ? $"{snap.Columns}k " : "";
        return $"{mode} · {keys}{snap.Applied}/{snap.Total} · t={snap.Time:0}";
    }

    static SpriteText? MakeExpiryHint()
    {
        var expiry = BuildExpiry.Current;
        if (!expiry.HasEndTime && !expiry.StampInvalid)
            return null;
        return UiLayout.Hint(expiry.Describe(DateTimeOffset.UtcNow));
    }

    protected override bool Handle(UIEvent e)
    {
        if (e is not (MouseEvent or KeyDownEvent or KeyUpEvent))
            return false;
        return base.Handle(e);
    }
}
