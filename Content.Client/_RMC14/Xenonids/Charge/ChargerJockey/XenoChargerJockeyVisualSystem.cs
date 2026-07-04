using Content.Client._RMC14.Buckle;
using Content.Client._RMC14.Sprite;
using Content.Client._RMC14.Xenonids;
using Content.Client._RMC14.Xenonids.Hide;
using Content.Shared._RMC14.Sprite;
using Content.Shared._RMC14.Xenonids.Charge.ChargerJockey;
using RmcDrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Xenonids.Charge.ChargerJockey;

public sealed partial class XenoChargerJockeyVisualSystem : EntitySystem
{
    [Dependency] private RMCSpriteSystem _rmcSprite = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(RMCSpriteSystem));

        SubscribeLocalEvent<XenoChargerRidingComponent, AfterAutoHandleStateEvent>(OnRiderState);
        SubscribeLocalEvent<XenoChargerRidingComponent, GetDrawDepthEvent>(
            OnGetDrawDepth,
            after: [typeof(XenoHideVisualizerSystem), typeof(XenoVisualizerSystem), typeof(RMCBuckleVisualsSystem)]);

        EntityManager.ComponentRemoved += OnComponentRemoved;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.ComponentRemoved -= OnComponentRemoved;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<XenoChargerRidingComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var riding, out var sprite))
        {
            if (sprite.DrawDepth != riding.DrawDepth)
                _sprite.SetDrawDepth((uid, sprite), riding.DrawDepth);
        }
    }

    private void OnRiderState(Entity<XenoChargerRidingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _rmcSprite.UpdateDrawDepth(ent.Owner);
    }

    private void OnComponentRemoved(RemovedComponentEventArgs args)
    {
        if (args.Terminating || args.BaseArgs.Component is not XenoChargerRidingComponent)
            return;

        _rmcSprite.UpdateDrawDepth(args.BaseArgs.Owner);
    }

    private void OnGetDrawDepth(Entity<XenoChargerRidingComponent> ent, ref GetDrawDepthEvent args)
    {
        args.DrawDepth = (RmcDrawDepth) ent.Comp.DrawDepth;
    }
}
