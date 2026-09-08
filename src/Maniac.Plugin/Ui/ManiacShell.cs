using Maniac;
using Maniac.Core;
using Maniac.Core.Mania;
using Maniac.Core.Settings;
using Maniac.Core.Status;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osuTK;

namespace Maniac.Plugin.Ui;

public partial class ManiacShell : CompositeDrawable
{
    public const double StatusIntervalMs = 100;
    public const float AdvancedMaxHeight = 560;
    public const float OverlayWidth = 360;
    public const int SettingsPage = 3;

    readonly SettingsStore settings;
    readonly Func<AutoplayStatus> status;
    readonly Func<string> extraDiagnostic;
    readonly Container body;
    readonly Drawable compactBar;
    readonly StatusDot statusDot;
    readonly BrandButton generalNav;
    readonly IconButton settingsButton;
    readonly IconButton panelToggle;
    readonly Container pageHost;
    readonly Drawable[] pages;
    readonly StatusLine diagLine;
    readonly StatusLine playLine;
    readonly StatusLine probeLine;
    readonly PanelButton[] nav;
    readonly ToggleRow autoRow;
    readonly ToggleRow osuRow;
    readonly ToggleRow maniaRow;
    readonly DisclosureRow maniaAdvancedRow;
    readonly SliderRow maniaSkill;
    readonly Container maniaAdvanced;
    readonly FillFlowContainer advancedColumn;
    readonly HumanSliderBinding[] humanSliders;
    readonly SpriteText? expiryHint;
    readonly SpriteText? expiryDiagHint;
    double lastStatusAt;
    string lastPlayText = "";
    string? lastDiagText;
    bool lastAdvanced;
    double? skillPreview;

    public ManiacShell(SettingsStore store, Func<AutoplayStatus> statusOf, Func<string> diagnosticOf)
    {
        settings = store;
        status = statusOf;
        extraDiagnostic = diagnosticOf;
        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        Margin = new MarginPadding(12);
        Width = OverlayWidth;
        AutoSizeAxes = Axes.Y;
        AlwaysPresent = true;
        Depth = float.MinValue;
        Name = "maniac-overlay";

        generalNav = new BrandButton("maniac", () => OpenPage(0));
        nav =
        [
            new PanelButton(OsuIcon.RulesetOsu, () => OpenPage(1))
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            },
            new PanelButton(OsuIcon.RulesetMania, () => OpenPage(2))
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            },
        ];

        autoRow = new ToggleRow("Autoplay", store.Current.AutoplayEnabled && !BuildExpiry.Current.IsExpired(), v =>
        {
            if (BuildExpiry.Current.IsExpired())
                return;
            store.Update(s => s.AutoplayEnabled = v);
        }, "Master switch for sending autoplay input.");
        expiryHint = MakeExpiryHint();
        expiryDiagHint = MakeExpiryHint();
        osuRow = new ToggleRow("osu!standard driver", store.Current.Osu.Enabled, v => store.Update(s => s.Osu.Enabled = v), "When off, osu!standard charts are not autoplayed.");
        maniaRow = new ToggleRow("mania driver", store.Current.Mania.Enabled, v => store.Update(s => s.Mania.Enabled = v), "When off, mania charts are not autoplayed.");
        maniaSkill = new SliderRow(
            "Skill",
            ManiaHumanLimits.SkillMin,
            ManiaHumanLimits.SkillMax,
            1,
            store.Current.Mania.Skill,
            v =>
            {
                skillPreview = null;
                store.Update(s => s.Mania.Skill = v);
            },
            v => $"{v:0}",
            "Overall performance level. 100 is tighter than typical top-level play; 0 is well below mid-tier. Advanced Auto values follow this until you customize them.",
            onPreview: v =>
            {
                skillPreview = v;
                RefreshHumanSliders();
            },
            onRelease: () => skillPreview = null);
        maniaAdvancedRow = new DisclosureRow("Advanced", store.Current.Mania.ShowAdvanced, ToggleAdvanced);

        var resolved = ManiaHumanParams.From(store.Current.Mania);
        var humanColumn = HumanSliderLayout.BuildColumn(store, resolved, store.Current.Mania.Human, out humanSliders);
        advancedColumn = UiLayout.Column(humanColumn);
        advancedColumn.Padding = new MarginPadding { Right = 8 };
        maniaAdvanced = new Container
        {
            RelativeSizeAxes = Axes.X,
            Masking = true,
            Child = new BasicScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = true,
                ScrollbarOverlapsContent = false,
                Child = advancedColumn,
            },
        };
        playLine = new StatusLine();
        probeLine = new StatusLine { Text = ProbeHint() };
        diagLine = new StatusLine();
        statusDot = new StatusDot();
        settingsButton = new IconButton(
            UiTheme.Icon(OsuIcon.Settings, UiTheme.IconMedium, UiTheme.IconMuted),
            OpenSettings);
        panelToggle = new IconButton(
            UiTheme.Icon(FontAwesome.Solid.ChevronDown, UiTheme.IconSmall),
            ToggleExpanded);

        pages =
        [
            UiLayout.Column(
                UiLayout.Heading("General"),
                expiryHint,
                autoRow,
                new KeybindRow("Toggle overlay", store.Current.PanelHotkey, v => store.Update(s => s.PanelHotkey = v), "Hotkey that hides or shows the overlay."),
                new KeybindRow("Toggle autoplay", store.Current.AutoplayHotkey, v => store.Update(s => s.AutoplayHotkey = v), "Hotkey that turns autoplay on or off."),
                UiLayout.Hint("Made by fsoc, github.com/fs-c/maniac")),
            UiLayout.Column(
                UiLayout.Heading("osu!standard"),
                osuRow,
                UiLayout.Hint("osu!std humanization will be available in a future update.")),
            UiLayout.Column(
                UiLayout.Heading("mania"),
                maniaRow,
                maniaSkill,
                AdvancedInset(maniaAdvancedRow, maniaAdvanced)),
            UiLayout.Column(
                UiLayout.Heading("Settings"),
                UiLayout.Nav(
                    new PanelButton("Reload", Session.RequestReload, fillWidth: true),
                    new PanelButton("Unload", Session.RequestUnload, fillWidth: true)),
                new StatusLine { Text = $"plugin {File.GetLastWriteTime(typeof(ManiacShell).Assembly.Location):HH:mm:ss}" },
                expiryDiagHint,
                playLine,
                diagLine,
                probeLine),
        ];

        pageHost = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = pages,
        };

        statusDot.Anchor = Anchor.CentreLeft;
        statusDot.Origin = Anchor.CentreLeft;
        compactBar = CompactBar();
        body = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = UiTheme.OverlayContentBackground },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(12),
                    Child = pageHost,
                },
            },
        };

        InternalChild = new OverlayTooltipContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = UiTheme.OverlayRadius,
                Children = new Drawable[]
                {
                    new BlockingBox { RelativeSizeAxes = Axes.Both, Colour = UiTheme.OverlayBackground },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            body,
                            compactBar,
                        },
                    },
                },
            },
        };

        ApplyVisible(store.Current.General.OverlayVisible);
        ApplyExpanded(store.Current.General.PanelExpanded);
        lastAdvanced = store.Current.Mania.ShowAdvanced;
        ApplyAdvanced(lastAdvanced);
        ShowPage(0);
    }

    static Drawable AdvancedInset(Drawable header, Drawable content) => new Container
    {
        RelativeSizeAxes = Axes.X,
        AutoSizeAxes = Axes.Y,
        Masking = true,
        CornerRadius = UiTheme.InsetRadius,
        Children = new Drawable[]
        {
            new Box { RelativeSizeAxes = Axes.Both, Colour = UiTheme.InsetBackground },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Padding = new MarginPadding(6),
                Children = new[] { header, content },
            },
        },
    };

    Drawable CompactBar() => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 36,
        Padding = new MarginPadding { Horizontal = 10 },
        Children = new Drawable[]
        {
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Children = new Drawable[]
                {
                    statusDot,
                    generalNav,
                    nav[0],
                    nav[1],
                },
            },
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(2, 0),
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Children = new Drawable[]
                {
                    settingsButton,
                    panelToggle,
                },
            },
        },
    };

    public void ToggleVisible()
    {
        bool visible = !settings.Current.General.OverlayVisible;
        settings.Update(s => s.General.OverlayVisible = visible);
        ApplyVisible(visible);
    }

    public void ToggleExpanded()
    {
        bool expanded = !body.IsPresent;
        settings.Update(s => s.General.PanelExpanded = expanded);
        ApplyExpanded(expanded);
    }

    void OpenPage(int index)
    {
        EnsureExpanded();
        ShowPage(index);
    }

    void OpenSettings()
    {
        EnsureExpanded();
        ShowPage(SettingsPage);
    }

    void EnsureExpanded()
    {
        if (body.IsPresent)
            return;
        settings.Update(s => s.General.PanelExpanded = true);
        ApplyExpanded(true);
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

    void ApplyVisible(bool visible)
    {
        AlwaysPresent = visible;
        if (visible)
            Show();
        else
            Hide();
    }

    void ApplyExpanded(bool expanded)
    {
        if (expanded)
            body.Show();
        else
            body.Hide();
        panelToggle.Icon.Rotation = expanded ? 0 : 180;
    }

    void ToggleAdvanced(bool show)
    {
        settings.Update(s => s.Mania.ShowAdvanced = show);
        lastAdvanced = show;
        ApplyAdvanced(show);
    }

    void ApplyAdvanced(bool show)
    {
        if (show)
            maniaAdvanced.Show();
        else
            maniaAdvanced.Hide();
        FitAdvanced();
    }

    void FitAdvanced()
    {
        if (!lastAdvanced)
        {
            maniaAdvanced.Height = 0;
            return;
        }

        float needed = Math.Max(advancedColumn.Height, advancedColumn.DrawHeight);
        maniaAdvanced.Height = Math.Clamp(needed, 1, AdvancedMaxHeight);
    }

    void ShowPage(int index)
    {
        generalNav.SetActive(index == 0);
        for (int i = 0; i < nav.Length; i++)
            nav[i].SetActive(i + 1 == index);
        for (int i = 0; i < pages.Length; i++)
        {
            if (i == index)
                pages[i].Show();
            else
                pages[i].Hide();
        }

        settingsButton.SetActive(index == SettingsPage);
    }

    protected override void Update()
    {
        base.Update();
        FitAdvanced();
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
        maniaAdvancedRow.SetOpen(settings.Current.Mania.ShowAdvanced);
        if (settings.Current.Mania.ShowAdvanced != lastAdvanced)
        {
            lastAdvanced = settings.Current.Mania.ShowAdvanced;
            ApplyAdvanced(lastAdvanced);
        }

        RefreshHumanSliders();

        string play = expired ? "expired" : FormatPlay(snap);
        bool on = !expired && snap.Enabled;
        statusDot.SetState(on, on && snap.Blocked, play);
        if (play != lastPlayText)
        {
            lastPlayText = play;
            playLine.Text = play;
        }

        string? diag = snap.Diagnostic;
        if (string.IsNullOrEmpty(diag))
            diag = extraDiagnostic();
        if (!snap.ProbeOk)
            diag = string.IsNullOrEmpty(diag) ? "startup probe failed" : diag;
        if (diag != lastDiagText)
        {
            lastDiagText = diag;
            diagLine.Text = string.IsNullOrEmpty(diag) ? "ok" : diag;
        }

        probeLine.Text = ProbeHint();
    }

    void RefreshHumanSliders()
    {
        var mania = settings.Current.Mania;
        var resolved = skillPreview is double skill
            ? ManiaHumanProfile.Resolve(new ManiaSettings { Skill = skill, Human = mania.Human })
            : ManiaHumanParams.From(mania);
        var h = mania.Human;
        for (int i = 0; i < humanSliders.Length; i++)
            humanSliders[i].Refresh(resolved, h);
    }

    string ProbeHint()
    {
        var startup = GameBindings.LastProbe;
        var input = GameBindings.LastInputProbe;
        var parts = new List<string>();
        parts.AddRange(startup.Ok ? startup.Resolved : startup.Failures);
        if (input.Resolved.Count > 0 || input.Failures.Count > 0)
            parts.AddRange(input.Ok ? input.Resolved : input.Failures);
        if (!string.IsNullOrEmpty(settings.LastLoadError))
            parts.Add("settings " + settings.LastLoadError);
        return parts.Count == 0 ? "probe pending" : string.Join(" · ", parts);
    }

    static string FormatPlay(in AutoplayStatus snap)
    {
        string mode = !snap.Enabled ? "off" : snap.Blocked ? "paused" : "on";
        string? ruleset = string.IsNullOrEmpty(snap.Ruleset) ? snap.Screen : snap.Ruleset;
        if (snap.Total == 0)
            return ruleset == null ? mode : $"{mode} · {ruleset}";
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
        if (Alpha <= 0)
            return false;
        if (e is MouseEvent)
        {
            base.Handle(e);
            return true;
        }

        if (e is KeyDownEvent or KeyUpEvent)
            return base.Handle(e);
        return false;
    }
}
