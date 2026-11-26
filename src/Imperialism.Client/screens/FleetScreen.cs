using Godot;
using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>Naval roster and draft editor. Every outcome and quantity comes from the preview.</summary>
public sealed partial class FleetScreen : Control, IShellScreen
{
    private GameSession? _session;
    private VBoxContainer? _content;
    private readonly HashSet<FleetId> _selected = [];
    private TaskForceId? _force;
    private bool _command;
    private NavalMissionKind _mission;
    private int _target;
    private Label? _feedback;

    public string Title => "Fleet";

    public override void _Ready()
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(panel);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(scroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_content);
        if (_session is not null)
        {
            Refresh();
        }
    }

    public void Enter(GameSession session)
    {
        _session = session;
        Refresh();
    }

    public void Exit() { }

    private void Refresh()
    {
        if (_session is null || _content is null)
        {
            return;
        }

        foreach (var child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }

        var view = _session.Preview().Fleet;
        _selected.IntersectWith(view.Fleets.Select(row => row.Id));
        Heading("Fleet — preview");
        Text(view.OrdersSetAside
            ? "Country eliminated. All drafted orders are set aside."
            : "Orders resolve in the order shown. Battles and interception are not predicted.");
        Heading("Fleets");
        if (view.Fleets.Count == 0)
        {
            Text("No fleets.");
        }

        foreach (var fleet in view.Fleets)
        {
            var pick = new CheckBox
            {
                Text = $"Fleet {fleet.Id.Value} · {fleet.TypeName}",
                ButtonPressed = _selected.Contains(fleet.Id),
            };
            pick.Toggled += selected =>
            {
                if (selected) { _selected.Add(fleet.Id); }
                else { _selected.Remove(fleet.Id); }
            };
            _content.AddChild(pick);
            Text(MilitaryText.Describe(fleet, Zone(fleet.SeaZone)));
        }

        Heading("Task forces");
        if (view.TaskForces.Count == 0)
        {
            Text("No task forces.");
        }

        foreach (var force in view.TaskForces)
        {
            var target = TargetName(force.BlockadeTargetKey, NavalMissionKind.Blockade) ??
                TargetName(force.LandingTargetKey, NavalMissionKind.Landing);
            Text(MilitaryText.Describe(force, Zone(force.SeaZone), target));
            foreach (var id in force.Fleets)
            {
                var fleet = view.Fleets.First(row => row.Id == id);
                Text($"Fleet {id.Value}: {fleet.Count} × {fleet.TypeName}");
            }

            if (force.Activity == TaskForceActivity.Landing)
            {
                Text(force.LandingUsableThisTurn ? "Landing site ready this turn." : "Landing site not ready this turn.");
            }
        }

        Heading("Give an order");
        var mode = new OptionButton();
        mode.AddItem("Assemble selected fleets");
        mode.AddItem("Command a task force");
        mode.Selected = _command ? 1 : 0;
        mode.ItemSelected += index => { _command = index == 1; Refresh(); };
        _content.AddChild(mode);
        if (_command)
        {
            var forces = view.TaskForces;
            var picker = new OptionButton();
            foreach (var row in forces) { picker.AddItem($"Task force {row.Id.Value} · {Zone(row.SeaZone)}"); }
            var selected = forces.ToList().FindIndex(row => row.Id == _force);
            if (selected < 0) { _force = forces.Count > 0 ? forces[0].Id : null; selected = forces.Count > 0 ? 0 : -1; }
            picker.Selected = selected;
            picker.ItemSelected += index => _force = forces[(int)index].Id;
            _content.AddChild(picker);
        }

        var missions = new OptionButton();
        foreach (var name in new[] { "Done / assemble only", "Patrol", "Blockade country", "Landing site", "Move to sea zone" })
        {
            missions.AddItem(name);
        }

        missions.Selected = (int)_mission;
        missions.ItemSelected += index => { _mission = (NavalMissionKind)index; _target = 0; Refresh(); };
        _content.AddChild(missions);
        var targets = Targets();
        if (_mission is NavalMissionKind.Blockade or NavalMissionKind.Landing or NavalMissionKind.Move)
        {
            var picker = new OptionButton();
            foreach (var name in targets) { picker.AddItem(name); }
            picker.Selected = targets.Count > 0 ? _target : -1;
            picker.ItemSelected += index => _target = (int)index;
            _content.AddChild(picker);
        }

        Button("Draft order / replace", DraftOrder);
        _feedback = Text("Draft an order to see its reach and expected result below. Warnings do not block drafting.");
        Heading("Drafted orders — in resolution order");
        if (view.Orders.Count == 0) { Text("No naval orders drafted."); }
        foreach (var order in view.Orders)
        {
            Text(FleetSmoke.Describe(order) + Target(order.TargetKey, order.Mission));
            Button("Clear this order", () =>
            {
                if (order.Kind == DraftNavalOrderKind.Assemble) { _session.Draft.ClearNavalAssembly(order.Fleets[0]); }
                else { _session.Draft.ClearNavalCommand(order.TaskForce!.Value); }
                Refresh();
            });
        }
    }

    private void DraftOrder()
    {
        if (_session is null) { return; }
        if ((_command && _force is null) || (!_command && _selected.Count == 0))
        {
            _feedback!.Text = _command ? "Choose a task force." : "Select at least one fleet above.";
            return;
        }

        if (_mission is NavalMissionKind.Blockade or NavalMissionKind.Landing or NavalMissionKind.Move && Targets().Count == 0)
        {
            _feedback!.Text = "This world has no target for this mission.";
            return;
        }

        var world = _session.World.Definition;
        var mission = _mission switch
        {
            NavalMissionKind.Patrol => NavalMission.Patrol,
            NavalMissionKind.Blockade => NavalMission.Blockade(world.Countries[_target].Id),
            NavalMissionKind.Landing => NavalMission.Landing(world.Map.Provinces[_target].Id),
            NavalMissionKind.Move => NavalMission.MoveTo(world.Map.SeaZones[_target].Id),
            _ => NavalMission.None,
        };
        if (_command) { _session.Draft.SetNavalCommand(_force!.Value, mission); }
        else { _session.Draft.SetNavalAssembly(_selected.OrderBy(id => id.Value), mission); }
        Refresh();
    }

    private IReadOnlyList<string> Targets()
    {
        var world = _session!.World.Definition;
        return _mission switch
        {
            NavalMissionKind.Blockade => world.Countries.Select(row => row.Name).ToArray(),
            NavalMissionKind.Landing => world.Map.Provinces.Select(row => row.Name).ToArray(),
            NavalMissionKind.Move => world.Map.SeaZones.Select(row => row.Name).ToArray(),
            _ => [],
        };
    }

    private string Zone(SeaZoneId? id) => id is { } zone
        ? _session!.World.Definition.Map.SeaZones[zone.Value].Name : "Unplaced";

    private string Target(string? key, NavalMissionKind mission) =>
        TargetName(key, mission) is { } name ? $" · {name}" : string.Empty;

    private string? TargetName(string? key, NavalMissionKind mission)
    {
        if (key is null) { return null; }
        var session = _session!;
        var catalog = session.Package.Catalog;
        var world = session.World.Definition;
        var name = mission switch
        {
            NavalMissionKind.Blockade => world.Countries[catalog.GetCountryId(key).Value].Name,
            NavalMissionKind.Landing => world.Map.Provinces[catalog.GetProvinceId(key).Value].Name,
            NavalMissionKind.Move => world.Map.SeaZones[catalog.GetSeaZoneId(key).Value].Name,
            _ => key,
        };
        return name;
    }

    private void Heading(string text) => _content!.AddChild(new Label
    {
        Text = text,
        ThemeTypeVariation = "ImperialismScreenTitle",
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    });

    private Label Text(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _content!.AddChild(label);
        return label;
    }

    private void Button(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        _content!.AddChild(button);
    }
}
