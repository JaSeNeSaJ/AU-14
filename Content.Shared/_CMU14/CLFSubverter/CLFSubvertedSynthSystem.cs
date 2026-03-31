using Content.Shared._RMC14.Synth;



namespace Content.Shared._CMU14.CLFSubverter;

public sealed class CLFSubvertedSynthSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CLFSubvertedSynthComponent, ComponentInit>(OnInit);
    }
    //hacky hack just to dirty the entity
    private void OnInit(EntityUid uid, CLFSubvertedSynthComponent comp, ComponentInit args)
    {
        if (TryComp<SynthComponent>(uid, out var sc))
        {
            Dirty(uid, sc);
        }
        DirtyEntity(uid);
    }
}
