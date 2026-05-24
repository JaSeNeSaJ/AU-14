using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.UserInterface;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Blackfoot;

[TestFixture]
public sealed class BlackfootPrototypeTest
{
    private static readonly EntProtoId BlackfootId = "VehicleBlackfoot";
    private static readonly EntProtoId DoorGunVariantId = "VehicleBlackfootDoorGunVariant";
    private static readonly EntProtoId ReconId = "VehicleBlackfootRecon";
    private static readonly EntProtoId TransportId = "VehicleBlackfootTransport";
    private static readonly EntProtoId LaunchersId = "VehicleBlackfootLaunchers";
    private static readonly EntProtoId DoorGunId = "VehicleBlackfootDoorGun";
    private static readonly EntProtoId PilotSeatId = "CMUSeatBlackfootPilot";
    private static readonly EntProtoId DoorGunnerSeatId = "CMUSeatBlackfootDoorGunner";
    private static readonly EntProtoId SideExitId = "CMUBlackfootSideExit";
    private static readonly EntProtoId SideExitRightId = "CMUBlackfootSideExitRight";
    private static readonly EntProtoId RearExitId = "CMUBlackfootRearExit";
    private static readonly EntProtoId RearDoorButtonId = "CMUBlackfootRearDoorButton";
    private static readonly EntProtoId AmmoLoaderRightId = "CMUBlackfootAmmoLoaderRight";
    private static readonly EntProtoId ChassisId = "CMUBlackfootChassis";
    private static readonly EntProtoId ViewportId = "CMUBlackfootViewport";
    private static readonly EntProtoId CassettePlayerId = "CMUBlackfootCassettePlayer";
    private static readonly EntProtoId ExtinguisherCabinetId = "CMUBlackfootExtinguisherCabinet";
    private static readonly EntProtoId LandingPadId = "CMUBlackfootLandingPad";
    private static readonly EntProtoId FoldedLandingPadId = "CMUBlackfootLandingPadFoldedProp";
    private static readonly EntProtoId FlightComputerId = "CMUBlackfootFlightComputer";
    private static readonly EntProtoId FuelPumpId = "CMUBlackfootFuelPump";
    private static readonly EntProtoId PadLightId = "CMUBlackfootLandingPadLight";
    private static readonly EntProtoId PadLightOnId = "CMUBlackfootLandingPadLightOn";
    private static readonly EntProtoId FuelPumpCrateId = "CMUBlackfootFuelPumpCrate";
    private static readonly EntProtoId FlightComputerCrateId = "CMUBlackfootFlightComputerCrate";
    private static readonly EntProtoId TugId = "CMUBlackfootAerospaceTug";
    private static readonly EntProtoId FlareAmmoBoxId = "CMUBlackfootAmmoBoxFlareLauncher";
    private static readonly EntProtoId DoorGunAmmoBoxId = "CMUBlackfootAmmoBoxDoorGun";
    private static readonly EntProtoId FlareLauncherBulletType = "VehicleAmmoBoxFlareLauncher";
    private static readonly EntProtoId DoorGunBulletType = "VehicleAmmoBoxGrenadeLauncher";
    private static readonly EntProtoId<AreaComponent> OpenAreaId = "RMCAreaVaraderoExterior";

    private static readonly ResPath[] InteriorMapPaths =
    {
        new("/Maps/_CMU14/Vehicles/Blackfoot/blackfoot.yml"),
        new("/Maps/_CMU14/Vehicles/Blackfoot/blackfoot_doorgun.yml"),
        new("/Maps/_CMU14/Vehicles/Blackfoot/blackfoot_transport.yml"),
    };

    private static readonly Dictionary<string, BlackfootInteriorExpectation> InteriorExpectations = new()
    {
        ["/Maps/_CMU14/Vehicles/Blackfoot/blackfoot.yml"] = new(8, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 19),
        ["/Maps/_CMU14/Vehicles/Blackfoot/blackfoot_doorgun.yml"] = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2),
        ["/Maps/_CMU14/Vehicles/Blackfoot/blackfoot_transport.yml"] = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2),
    };

    private static readonly ResPath[] PositionalAudioPaths =
    {
        new("/Audio/_CMU14/Blackfoot/buttonpress.ogg"),
        new("/Audio/_CMU14/Blackfoot/doorgun.wav"),
        new("/Audio/_CMU14/Blackfoot/engineidle.wav"),
        new("/Audio/_CMU14/Blackfoot/engineshutdown.wav"),
        new("/Audio/_CMU14/Blackfoot/enginestartup.wav"),
        new("/Audio/_CMU14/Blackfoot/exteriorflight.wav"),
        new("/Audio/_CMU14/Blackfoot/flight_transition.wav"),
        new("/Audio/_CMU14/Blackfoot/interior.wav"),
        new("/Audio/_CMU14/Blackfoot/landing.wav"),
        new("/Audio/_CMU14/Blackfoot/launcher.wav"),
        new("/Audio/_CMU14/Blackfoot/mechanical.wav"),
        new("/Audio/_CMU14/Blackfoot/radar_new_contact.ogg"),
        new("/Audio/_CMU14/Blackfoot/radaractive.wav"),
        new("/Audio/_CMU14/Blackfoot/takeoff.wav"),
    };

    private static readonly EntProtoId[] SupportPeripheralIds =
    {
        FuelPumpId,
        FoldedLandingPadId,
        PadLightId,
        PadLightOnId,
        FuelPumpCrateId,
        FlightComputerCrateId,
        FlareAmmoBoxId,
        DoorGunAmmoBoxId,
        TugId,
    };

    private static readonly (EntProtoId Id, bool RearDoor, bool Recon)[] Variants =
    {
        (BlackfootId, true, false),
        (DoorGunVariantId, true, false),
        (ReconId, true, true),
        (TransportId, true, false),
    };

    [Test]
    public async Task BlackfootVariantsHaveFlightInteriorAndWeaponContracts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var variant in Variants)
            {
                var variantName = variant.Id.ToString();
                Assert.That(prototypes.TryIndex<EntityPrototype>(variant.Id, out var proto), Is.True);
                Assert.That(proto!.TryGetComponent<BlackfootFlightComponent>(out var flight, factory), Is.True, variantName);
                Assert.That(flight!.State, Is.EqualTo(BlackfootFlightState.Stowed), variantName);
                Assert.That(
                    flight.FootprintOffsets,
                    Is.EquivalentTo(new[]
                    {
                        new Vector2i(-1, 1),
                        new Vector2i(0, 1),
                        new Vector2i(1, 1),
                        new Vector2i(-1, 0),
                        new Vector2i(0, 0),
                        new Vector2i(1, 0),
                        new Vector2i(0, -1),
                    }),
                    variantName);
                Assert.That(proto.TryGetComponent<VehicleWeaponsComponent>(out _, factory), Is.True, variantName);
                Assert.That(proto.TryGetComponent<VehicleEnterComponent>(out var enter, factory), Is.True, variantName);
                Assert.That(enter.EntryPoints, Has.Count.EqualTo(3), variantName);
                AssertEntryPoint(enter.EntryPoints[0], new Vector2(0f, 2f), new Vector2(8f, 9.5f), variantName);
                AssertEntryPoint(enter.EntryPoints[1], new Vector2(-1f, -1f), new Vector2(6.75f, 8.75f), variantName);
                AssertEntryPoint(enter.EntryPoints[2], new Vector2(1f, -1f), new Vector2(9.25f, 8.75f), variantName);
                Assert.That(enter.MaxPassengers, Is.EqualTo(9), variantName);
                Assert.That(enter.MaxXenos, Is.EqualTo(5), variantName);

                Assert.That(proto.TryGetComponent<ItemSlotsComponent>(out var itemSlots, factory), Is.True, variantName);
                Assert.That(itemSlots!.Slots["thrusters"].StartingItem, Is.EqualTo("VehicleBlackfootThrusters"), variantName);
                Assert.That(itemSlots.Slots["launchers"].StartingItem, Is.EqualTo("VehicleBlackfootLaunchers"), variantName);

                Assert.That(proto.TryGetComponent<HardpointSlotsComponent>(out var hardpoints, factory), Is.True, variantName);
                Assert.That(hardpoints!.VehicleFamily?.ToString(), Is.EqualTo("Blackfoot"), variantName);
                Assert.That(hardpoints.Slots.Any(slot => slot.Id == "thrusters" && slot.HardpointType == "Thruster"), Is.True, variantName);
                Assert.That(hardpoints.Slots.Any(slot => slot.Id == "launchers" && slot.HardpointType == "Launcher"), Is.True, variantName);

                Assert.That(proto.TryGetComponent<BlackfootRearDoorComponent>(out var rearDoor, factory), Is.EqualTo(variant.RearDoor), variantName);
                Assert.That(rearDoor!.Open, Is.False, variantName);
                Assert.That(rearDoor.RearEntryIndex, Is.EqualTo(0), variantName);
                Assert.That(proto.TryGetComponent<BlackfootStealthComponent>(out _, factory), Is.EqualTo(variant.Recon), variantName);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootExteriorHitboxMatchesSourceShape()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(BlackfootId, out var proto), Is.True);
            Assert.That(proto!.TryGetComponent<FixturesComponent>(out var fixtures, factory), Is.True);

            Assert.That(fixtures!.Fixtures.Keys, Is.EquivalentTo(new[] { "main", "cockpit" }));
            AssertFixtureBounds(fixtures.Fixtures["main"], -1.5f, -0.5f, 1.5f, 1.5f);
            AssertFixtureBounds(fixtures.Fixtures["cockpit"], -0.5f, -1.5f, 0.5f, -0.5f);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootLauncherUsesFlareAmmoLoaderContract()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(LaunchersId, out var proto), Is.True);
            Assert.That(proto!.TryGetComponent<HardpointItemComponent>(out var hardpoint, factory), Is.True);
            Assert.That(hardpoint!.HardpointType, Is.EqualTo("Launcher"));
            Assert.That(hardpoint.VehicleFamily?.ToString(), Is.EqualTo("Blackfoot"));
            Assert.That(hardpoint.SlotType?.ToString(), Is.EqualTo("Launcher"));

            Assert.That(proto.TryGetComponent<BallisticAmmoProviderComponent>(out var ammo, factory), Is.True);
            Assert.That(ammo!.Proto?.ToString(), Is.EqualTo("CMFlare"));
            Assert.That(ammo.Capacity, Is.EqualTo(10));

            Assert.That(proto.TryGetComponent<VehicleHardpointAmmoComponent>(out var hardpointAmmo, factory), Is.True);
            Assert.That(hardpointAmmo!.MagazineSize, Is.EqualTo(10));
            Assert.That(hardpointAmmo.MaxStoredMagazines, Is.EqualTo(3));

            Assert.That(proto.TryGetComponent<RefillableByBulletBoxComponent>(out var refill, factory), Is.True);
            Assert.That(refill!.BulletType?.ToString(), Is.EqualTo("VehicleAmmoBoxFlareLauncher"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootDoorGunUsesDedicatedGunnerContract()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(DoorGunId, out var doorGunProto), Is.True);
            Assert.That(doorGunProto!.TryGetComponent<HardpointItemComponent>(out var hardpoint, factory), Is.True);
            Assert.That(hardpoint!.HardpointType, Is.EqualTo("DoorGun"));
            Assert.That(hardpoint.VehicleFamily?.ToString(), Is.EqualTo("Blackfoot"));
            Assert.That(hardpoint.SlotType?.ToString(), Is.EqualTo("DoorGun"));

            Assert.That(doorGunProto.TryGetComponent<GunComponent>(out var gun, factory), Is.True);
            Assert.That(gun!.SelectedMode, Is.EqualTo(SelectiveFire.FullAuto));
            Assert.That(gun.ProjectileSpeed, Is.EqualTo(18));

            Assert.That(doorGunProto.TryGetComponent<ShootAtFixedPointComponent>(out var fixedPoint, factory), Is.True);
            Assert.That(fixedPoint!.ShootArcProj, Is.True);
            Assert.That(fixedPoint.MaxFixedRange, Is.EqualTo(10));

            Assert.That(doorGunProto.TryGetComponent<GunFireArcComponent>(out var arc, factory), Is.True);
            Assert.That(arc!.Arc.Degrees, Is.EqualTo(180).Within(0.001));
            Assert.That(arc.AngleOffset.Degrees, Is.EqualTo(180).Within(0.001));

            Assert.That(doorGunProto.TryGetComponent<BallisticAmmoProviderComponent>(out var ammo, factory), Is.True);
            Assert.That(ammo!.Proto?.ToString(), Is.EqualTo("CMGrenadeHighExplosive"));
            Assert.That(ammo.Capacity, Is.EqualTo(10));

            Assert.That(doorGunProto.TryGetComponent<VehicleHardpointAmmoComponent>(out var hardpointAmmo, factory), Is.True);
            Assert.That(hardpointAmmo!.MagazineSize, Is.EqualTo(10));
            Assert.That(hardpointAmmo.MaxStoredMagazines, Is.EqualTo(3));

            Assert.That(doorGunProto.TryGetComponent<RefillableByBulletBoxComponent>(out var refill, factory), Is.True);
            Assert.That(refill!.BulletType?.ToString(), Is.EqualTo("VehicleAmmoBoxGrenadeLauncher"));

            Assert.That(prototypes.TryIndex<EntityPrototype>(PilotSeatId, out var pilotSeatProto), Is.True);
            Assert.That(pilotSeatProto!.TryGetComponent<VehicleWeaponsSeatComponent>(out var pilotSeat, factory), Is.True);
            Assert.That(pilotSeat!.IsPrimaryOperatorSeat, Is.True);
            Assert.That(pilotSeat.AllowedHardpointTypes, Is.EquivalentTo(new[] { "Launcher" }));

            Assert.That(prototypes.TryIndex<EntityPrototype>(DoorGunnerSeatId, out var doorGunnerSeatProto), Is.True);
            Assert.That(doorGunnerSeatProto!.TryGetComponent<VehicleWeaponsSeatComponent>(out var gunnerSeat, factory), Is.True);
            Assert.That(gunnerSeat!.IsPrimaryOperatorSeat, Is.False);
            Assert.That(gunnerSeat.AllowedHardpointTypes, Is.EquivalentTo(new[] { "DoorGun" }));
            Assert.That(gunnerSeat.BaseViewPvsScale, Is.GreaterThan(0));
            Assert.That(gunnerSeat.BaseViewCursorMaxOffset, Is.GreaterThan(0));

            Assert.That(prototypes.TryIndex<EntityPrototype>(SideExitId, out var sideExitProto), Is.True);
            Assert.That(sideExitProto!.TryGetComponent<VehicleExitComponent>(out var sideExit, factory), Is.True);
            Assert.That(sideExit!.EntryIndex, Is.EqualTo(2));

            Assert.That(prototypes.TryIndex<EntityPrototype>(SideExitRightId, out var sideExitRightProto), Is.True);
            Assert.That(sideExitRightProto!.TryGetComponent<VehicleExitComponent>(out var sideExitRight, factory), Is.True);
            Assert.That(sideExitRight!.EntryIndex, Is.EqualTo(1));

            Assert.That(prototypes.TryIndex<EntityPrototype>(RearExitId, out var rearExitProto), Is.True);
            Assert.That(rearExitProto!.TryGetComponent<BlackfootRearDoorVisualsComponent>(out var rearVisuals, factory), Is.True);
            Assert.That(rearVisuals!.ShowOverlay, Is.False);
            Assert.That(rearVisuals.OverlayLayer, Is.Empty);
            Assert.That(rearExitProto.TryGetComponent<VehicleExitComponent>(out var rearExit, factory), Is.True);
            Assert.That(rearExit!.EntryIndex, Is.EqualTo(0));

            Assert.That(prototypes.TryIndex<EntityPrototype>(RearDoorButtonId, out var rearButtonProto), Is.True);
            Assert.That(rearButtonProto!.TryGetComponent<BlackfootRearDoorControlComponent>(out _, factory), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootWeaponsRespectFlightDoorAndStealthGates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var itemSlots = entMan.System<ItemSlotsSystem>();

            var doorGunVehicle = entMan.Spawn(DoorGunVariantId);
            var flight = entMan.GetComponent<BlackfootFlightComponent>(doorGunVehicle);
            var rearDoor = entMan.GetComponent<BlackfootRearDoorComponent>(doorGunVehicle);

            var launcher = GetMountedItem(itemSlots, doorGunVehicle, "launchers");
            var doorGun = GetMountedItem(itemSlots, doorGunVehicle, "door-gun");

            flight.State = BlackfootFlightState.Grounded;
            Assert.That(AttemptShot(entMan, launcher, doorGunVehicle).Cancelled, Is.True, "launchers should be airborne-only");

            flight.State = BlackfootFlightState.VTOL;
            Assert.That(AttemptShot(entMan, launcher, doorGunVehicle).Cancelled, Is.False, "launchers should fire in VTOL");

            rearDoor.Open = false;
            Assert.That(AttemptShot(entMan, doorGun, doorGunVehicle).Cancelled, Is.True, "door gun requires open rear door");

            rearDoor.Open = true;
            Assert.That(AttemptShot(entMan, doorGun, doorGunVehicle).Cancelled, Is.False, "door gun should fire when rear door is open");

            var reconVehicle = entMan.Spawn(ReconId);
            var reconFlight = entMan.GetComponent<BlackfootFlightComponent>(reconVehicle);
            var reconDoor = entMan.GetComponent<BlackfootRearDoorComponent>(reconVehicle);
            var stealth = entMan.GetComponent<BlackfootStealthComponent>(reconVehicle);
            var reconLauncher = GetMountedItem(itemSlots, reconVehicle, "launchers");
            var reconDoorGun = GetMountedItem(itemSlots, reconVehicle, "door-gun");

            reconFlight.State = BlackfootFlightState.Flight;
            reconDoor.Open = true;
            stealth.Enabled = true;

            Assert.That(AttemptShot(entMan, reconLauncher, reconVehicle).Cancelled, Is.True, "stealth should disable launchers");
            Assert.That(AttemptShot(entMan, reconDoorGun, reconVehicle).Cancelled, Is.True, "stealth should disable door gun");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootRearDoorGatesRearEntryAndRearExit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            var vehicle = entMan.Spawn(BlackfootId);
            var rearDoor = entMan.GetComponent<BlackfootRearDoorComponent>(vehicle);
            var user = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var rearExit = entMan.SpawnEntity(RearExitId.ToString(), MapCoordinates.Nullspace);
            var sideExit = entMan.SpawnEntity(SideExitId.ToString(), MapCoordinates.Nullspace);

            rearDoor.Open = false;

            var rearEntryAttempt = new VehicleEntryAttemptEvent(user, rearDoor.RearEntryIndex);
            entMan.EventBus.RaiseLocalEvent(vehicle, ref rearEntryAttempt);
            Assert.That(rearEntryAttempt.Cancelled, Is.True, "closed rear door should block rear boarding");

            var sideEntryAttempt = new VehicleEntryAttemptEvent(user, rearDoor.RearEntryIndex + 1);
            entMan.EventBus.RaiseLocalEvent(vehicle, ref sideEntryAttempt);
            Assert.That(sideEntryAttempt.Cancelled, Is.False, "closed rear door should not block side/cockpit entry points");

            var rearExitAttempt = new VehicleExitAttemptEvent(user, rearExit);
            entMan.EventBus.RaiseLocalEvent(vehicle, ref rearExitAttempt);
            Assert.That(rearExitAttempt.Cancelled, Is.True, "closed rear door should block rear exit");

            var sideExitAttempt = new VehicleExitAttemptEvent(user, sideExit);
            entMan.EventBus.RaiseLocalEvent(vehicle, ref sideExitAttempt);
            Assert.That(sideExitAttempt.Cancelled, Is.False, "closed rear door should not block side exits");

            rearDoor.Open = true;

            rearEntryAttempt = new VehicleEntryAttemptEvent(user, rearDoor.RearEntryIndex);
            entMan.EventBus.RaiseLocalEvent(vehicle, ref rearEntryAttempt);
            Assert.That(rearEntryAttempt.Cancelled, Is.False, "open rear door should allow rear boarding");

            rearExitAttempt = new VehicleExitAttemptEvent(user, rearExit);
            entMan.EventBus.RaiseLocalEvent(vehicle, ref rearExitAttempt);
            Assert.That(rearExitAttempt.Cancelled, Is.False, "open rear door should allow rear exit");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootSupportPeripheralsResolveGameplayContracts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var id in SupportPeripheralIds)
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(id, out _), Is.True, id.ToString());
            }

            Assert.That(prototypes.TryIndex<EntityPrototype>(TugId, out var tugProto), Is.True);
            Assert.That(tugProto!.TryGetComponent<BlackfootTowComponent>(out var tow, factory), Is.True);
            Assert.That(tow!.CanTow, Is.True);
            Assert.That(tow.CanBeTowed, Is.False);
            Assert.That(tow.AllowAirborneTowing, Is.False);
            Assert.That(tow.TowHardpointId, Is.EqualTo("tow-hitch"));
            Assert.That(tow.AttachRange, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(tow.AttachOffset.X, Is.EqualTo(0f).Within(0.001f));
            Assert.That(tow.AttachOffset.Y, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(tow.TaxiSpeedMultiplier, Is.GreaterThan(0));
            Assert.That(tow.TaxiAccelerationMultiplier, Is.GreaterThan(0));

            Assert.That(prototypes.TryIndex<EntityPrototype>(FuelPumpId, out var fuelPumpProto), Is.True);
            Assert.That(fuelPumpProto!.TryGetComponent<BlackfootFuelPumpComponent>(out _, factory), Is.True);
            AssertMountedPadSupport(prototypes, factory, FuelPumpId);
            AssertPackable(prototypes, factory, FuelPumpId, FuelPumpCrateId);

            Assert.That(prototypes.TryIndex<EntityPrototype>(FlightComputerId, out var flightComputerProto), Is.True);
            Assert.That(flightComputerProto!.TryGetComponent<ActivatableUIComponent>(out var activatableUi, factory), Is.True);
            Assert.That(activatableUi!.Key, Is.EqualTo(BlackfootFlightComputerUiKey.Key));
            Assert.That(flightComputerProto.TryGetComponent<UserInterfaceComponent>(out _, factory), Is.True);
            AssertMountedPadSupport(prototypes, factory, FlightComputerId);
            AssertPackable(prototypes, factory, FlightComputerId, FlightComputerCrateId);

            Assert.That(prototypes.TryIndex<EntityPrototype>(LandingPadId, out var landingPadProto), Is.True);
            Assert.That(landingPadProto!.TryGetComponent<BlackfootLandingPadComponent>(out var landingPad, factory), Is.True);
            Assert.That(landingPad!.State, Is.EqualTo(BlackfootLandingPadState.Deployed));
            Assert.That(landingPad.FuelPumpOffset.X, Is.EqualTo(-1.15625f).Within(0.001f));
            Assert.That(landingPad.FuelPumpOffset.Y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(landingPad.FlightComputerOffset.X, Is.EqualTo(-1.5f).Within(0.001f));
            Assert.That(landingPad.FlightComputerOffset.Y, Is.EqualTo(-1f).Within(0.001f));
            AssertPackable(prototypes, factory, LandingPadId, FoldedLandingPadId);

            Assert.That(prototypes.TryIndex<EntityPrototype>(PadLightId, out var padLightProto), Is.True);
            Assert.That(padLightProto!.TryGetComponent<BlackfootLandingPadLightComponent>(out var padLight, factory), Is.True);
            Assert.That(padLight!.State, Is.EqualTo(BlackfootLandingPadLightState.Off));

            Assert.That(prototypes.TryIndex<EntityPrototype>(PadLightOnId, out var padLightOnProto), Is.True);
            Assert.That(padLightOnProto!.TryGetComponent<BlackfootLandingPadLightComponent>(out var padLightOn, factory), Is.True);
            Assert.That(padLightOn!.State, Is.EqualTo(BlackfootLandingPadLightState.Servicing));

            AssertDeployable(prototypes, factory, FoldedLandingPadId, LandingPadId);
            AssertDeployable(prototypes, factory, FuelPumpCrateId, FuelPumpId, BlackfootLandingPadAttachment.FuelPump);
            AssertDeployable(prototypes, factory, FlightComputerCrateId, FlightComputerId, BlackfootLandingPadAttachment.FlightComputer);
            AssertUnpickableSupport(prototypes, factory, FoldedLandingPadId);
            AssertUnpickableSupport(prototypes, factory, FuelPumpCrateId);
            AssertUnpickableSupport(prototypes, factory, FlightComputerCrateId);

            AssertBlackfootAmmoBox(prototypes, factory, FlareAmmoBoxId, FlareLauncherBulletType, 10);
            AssertBlackfootAmmoBox(prototypes, factory, DoorGunAmmoBoxId, DoorGunBulletType, 10);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootDynamicSpriteStatesExist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/blackfoot.rsi/meta.json",
                RequiredBlackfootRuntimeStates());

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/blackfoot_peripherals.rsi/meta.json",
                new[]
                {
                    "fuelpump-crate",
                    "flightcpu-crate",
                    "blackfoot-ammo",
                    "doorgun-ammo",
                    "landing pad light",
                    "landing pad light on",
                    "blackfoot-ammo-interior-left",
                    "blackfoot-ammo-interior-right",
                });

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/blackfoot_structures.rsi/meta.json",
                new[] { "fuel pump", "landing-pad-folded", "flight-cpu" });

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/blackfoot_landing_pad.rsi/meta.json",
                new[] { "pad" });

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/blackfoot_tug.rsi/meta.json",
                new[] { "aerospace-tug" });

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/blackfoot_door_button.rsi/meta.json",
                new[] { "blackfoot", "blackfoot0", "blackfoot1" });

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/blackfoot_hardpoints.rsi/meta.json",
                new[] { "engines", "launchers", "doorgun-module", "radar" });

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/interiors/blackfoot.rsi/meta.json",
                RequiredBlackfootInteriorStates());

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/interiors/blackfoot_64x64.rsi/meta.json",
                new[] { "doorgun", "doorgun-deployed" });

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/interiors/blackfoot_chassis.rsi/meta.json",
                new[] { "chassis" });

            AssertRsiStates(
                resources,
                "/Textures/_CMU14/Structures/Vehicles/Blackfoot/interiors/blackfoot_rear_overlay.rsi/meta.json",
                new[] { "overlay" });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootInteriorMapsLoad()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var mapLoader = server.System<MapLoaderSystem>();
            var mapSystem = server.System<SharedMapSystem>();
            var resources = server.ResolveDependency<IResourceManager>();

            foreach (var path in InteriorMapPaths)
            {
                Assert.That(mapLoader.TryLoadMap(path, out var map, out _), Is.True, path.ToString());
                Assert.That(map, Is.Not.Null, path.ToString());
                AssertBlackfootInteriorTiles(resources, path);
                AssertBlackfootInteriorEntities(resources, path);
                mapSystem.DeleteMap(map!.Value.Comp.MapId);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootReferencedAudioIsMono()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();

            foreach (var path in PositionalAudioPaths)
            {
                using var stream = resources.ContentFileRead(path);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);

                Assert.That(GetAudioChannelCount(memory.ToArray(), path.ToString()), Is.EqualTo(1), path.ToString());
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootSupportAssembliesDeployIntoPadHardware()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid user = default;
        EntityUid wrench = default;
        EntityUid pumpCrate = default;
        EntityUid computerCrate = default;
        EntityUid foldedPad = default;
        EntityUid pad = default;
        EntityUid map = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            map = CreateBlackfootZMaps(server, includeUpper: false).LowerMap;
            user = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map, 0, 2));
            wrench = entMan.SpawnEntity("Wrench", new EntityCoordinates(map, 0, 2));
            Assert.That(hands.TryPickupAnyHand(user, wrench, checkActionBlocker: false), Is.True);

            foldedPad = entMan.SpawnEntity(FoldedLandingPadId, new EntityCoordinates(map, 0, 0));
            entMan.GetComponent<BlackfootDeployableSupportComponent>(foldedPad).DeployDelay = 0.1f;
            InteractUsing(entMan, user, wrench, foldedPad);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(HasSpawnedComponent<BlackfootLandingPadComponent>(entMan), Is.True);
            Assert.That(entMan.Deleted(foldedPad), Is.True);

            pad = FindEntityWithComponent<BlackfootLandingPadComponent>(entMan);
            var padComp = entMan.GetComponent<BlackfootLandingPadComponent>(pad);
            Assert.That(padComp.State, Is.EqualTo(BlackfootLandingPadState.Deployed));
            Assert.That(padComp.Lights, Has.Count.EqualTo(4));

            pumpCrate = entMan.SpawnEntity(FuelPumpCrateId, new EntityCoordinates(map, 0, 0));
            entMan.GetComponent<BlackfootDeployableSupportComponent>(pumpCrate).DeployDelay = 0.1f;
            InteractUsing(entMan, user, wrench, pumpCrate);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pump = FindEntityWithComponent<BlackfootFuelPumpComponent>(entMan);
            var pumpComp = entMan.GetComponent<BlackfootFuelPumpComponent>(pump);
            var padComp = entMan.GetComponent<BlackfootLandingPadComponent>(pad);
            var padXform = entMan.GetComponent<TransformComponent>(pad);
            var pumpXform = entMan.GetComponent<TransformComponent>(pump);

            Assert.That(entMan.Deleted(pumpCrate), Is.True);
            Assert.That(pumpComp.LandingPad, Is.EqualTo(pad));
            Assert.That(padComp.FuelPump, Is.EqualTo(pump));
            Assert.That(pumpXform.LocalPosition.X, Is.EqualTo(padXform.LocalPosition.X - 2).Within(0.001f));
            Assert.That(pumpXform.LocalPosition.Y, Is.EqualTo(padXform.LocalPosition.Y).Within(0.001f));

            computerCrate = entMan.SpawnEntity(FlightComputerCrateId, new EntityCoordinates(map, 0, 0));
            entMan.GetComponent<BlackfootDeployableSupportComponent>(computerCrate).DeployDelay = 0.1f;
            InteractUsing(entMan, user, wrench, computerCrate);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var computer = FindEntityWithComponent<BlackfootFlightComputerComponent>(entMan);
            var computerComp = entMan.GetComponent<BlackfootFlightComputerComponent>(computer);
            var padXform = entMan.GetComponent<TransformComponent>(pad);
            var computerXform = entMan.GetComponent<TransformComponent>(computer);

            Assert.That(entMan.Deleted(computerCrate), Is.True);
            Assert.That(computerComp.LandingPad, Is.EqualTo(pad));
            Assert.That(computerXform.LocalPosition.X, Is.EqualTo(padXform.LocalPosition.X - 2).Within(0.001f));
            Assert.That(computerXform.LocalPosition.Y, Is.EqualTo(padXform.LocalPosition.Y - 1).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootSupportHardwarePacksBackWithToolSequence()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid user = default;
        EntityUid wrench = default;
        EntityUid screwdriver = default;
        EntityUid pad = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            user = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 0, 1));
            wrench = entMan.SpawnEntity("Wrench", new EntityCoordinates(map.Grid, 0, 1));
            screwdriver = entMan.SpawnEntity("Screwdriver", new EntityCoordinates(map.Grid, 0, 1));
            Assert.That(hands.TryPickupAnyHand(user, wrench, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, screwdriver, checkActionBlocker: false), Is.True);

            pad = entMan.SpawnEntity(LandingPadId, new EntityCoordinates(map.Grid, 0, 0));
            var pack = entMan.GetComponent<BlackfootPackableSupportComponent>(pad);
            pack.InitialDelay = 0.1f;
            pack.PanelDelay = 0.1f;
            pack.FinalDelay = 0.1f;

            InteractUsing(entMan, user, wrench, pad);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<BlackfootPackableSupportComponent>(pad).Stage, Is.EqualTo(BlackfootSupportPackStage.AnchorsLoosened));
            InteractUsing(entMan, user, screwdriver, pad);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<BlackfootPackableSupportComponent>(pad).Stage, Is.EqualTo(BlackfootSupportPackStage.PanelOpen));
            InteractUsing(entMan, user, wrench, pad);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.Deleted(pad), Is.True);
            Assert.That(HasPrototype(entMan, FoldedLandingPadId), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootFlightComputerRequiresFuelPumpForRefuel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var maps = CreateBlackfootZMaps(server, includeUpper: false);
            var pad = entMan.SpawnEntity(LandingPadId, new EntityCoordinates(maps.LowerMap, 0, 0));
            var computer = entMan.SpawnEntity(FlightComputerId, new EntityCoordinates(maps.LowerMap, 1, 0));
            var vehicle = entMan.SpawnEntity(BlackfootId, new EntityCoordinates(maps.LowerMap, 0, 0));
            var user = entMan.SpawnEntity("MobHuman", new EntityCoordinates(maps.LowerMap, 0, 1));

            var padComp = entMan.GetComponent<BlackfootLandingPadComponent>(pad);
            padComp.State = BlackfootLandingPadState.Deployed;
            padComp.ParkedAircraft = vehicle;
            var computerComp = entMan.GetComponent<BlackfootFlightComputerComponent>(computer);
            computerComp.LandingPad = pad;
            entMan.RemoveComponent<ActivatableUIComponent>(computer);

            var activate = new ActivateInWorldEvent(user, computer, true);
            entMan.EventBus.RaiseLocalEvent(computer, activate);
            Assert.That(activate.Handled, Is.True);
            Assert.That(padComp.Refueling, Is.False, "refuel should require a linked fuel pump");
            Assert.That(padComp.Recharging, Is.True, "recharge can still run from the flight computer");

            padComp.Recharging = false;
            var pump = entMan.SpawnEntity(FuelPumpId, new EntityCoordinates(maps.LowerMap, 0, 1));
            activate = new ActivateInWorldEvent(user, computer, true);
            entMan.EventBus.RaiseLocalEvent(computer, activate);
            Assert.That(activate.Handled, Is.True);
            Assert.That(padComp.Refueling, Is.False, "a nearby but unmounted pump should not refuel the Blackfoot");
            Assert.That(padComp.Recharging, Is.True);
            Assert.That(padComp.FuelPump, Is.Null);
            Assert.That(entMan.GetComponent<BlackfootFuelPumpComponent>(pump).LandingPad, Is.Null);

            padComp.Recharging = false;
            padComp.FuelPump = pump;
            var pumpComp = entMan.GetComponent<BlackfootFuelPumpComponent>(pump);
            pumpComp.LandingPad = pad;
            activate = new ActivateInWorldEvent(user, computer, true);
            entMan.EventBus.RaiseLocalEvent(computer, activate);
            Assert.That(activate.Handled, Is.True);
            Assert.That(padComp.Refueling, Is.True);
            Assert.That(padComp.Recharging, Is.True);
            Assert.That(padComp.FuelPump, Is.EqualTo(pump));
            Assert.That(pumpComp.LandingPad, Is.EqualTo(pad));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootLandingPadLightsFollowPadState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid pad = default;
        EntityUid light = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            pad = entMan.SpawnEntity(LandingPadId, new EntityCoordinates(map.Grid, 0, 0));
            light = entMan.SpawnEntity(PadLightId, new EntityCoordinates(map.Grid, 1, 0));

            var padComp = entMan.GetComponent<BlackfootLandingPadComponent>(pad);
            padComp.State = BlackfootLandingPadState.Deployed;
            entMan.Dirty(pad, padComp);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var lightComp = entMan.GetComponent<BlackfootLandingPadLightComponent>(light);
            Assert.That(lightComp.LandingPad, Is.EqualTo(pad));
            Assert.That(lightComp.State, Is.EqualTo(BlackfootLandingPadLightState.Ready));
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var padComp = entMan.GetComponent<BlackfootLandingPadComponent>(pad);
            padComp.Recharging = true;
            entMan.Dirty(pad, padComp);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var lightComp = server.EntMan.GetComponent<BlackfootLandingPadLightComponent>(light);
            Assert.That(lightComp.State, Is.EqualTo(BlackfootLandingPadLightState.Servicing));
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var padComp = entMan.GetComponent<BlackfootLandingPadComponent>(pad);
            padComp.State = BlackfootLandingPadState.Folded;
            entMan.Dirty(pad, padComp);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var lightComp = server.EntMan.GetComponent<BlackfootLandingPadLightComponent>(light);
            Assert.That(lightComp.LandingPad, Is.Null);
            Assert.That(lightComp.State, Is.EqualTo(BlackfootLandingPadLightState.Off));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootTakeoffDeniesMissingUpperMapAndBrokenThrusters()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var maps = CreateBlackfootZMaps(server, includeUpper: false);
            var (vehicle, pilot, flight) = SpawnPilotedBlackfoot(server, maps.LowerMap);

            flight.State = BlackfootFlightState.Idling;
            flight.TakeoffDuration = TimeSpan.Zero;
            var takeoff = RaiseTakeoff(entMan, pilot);

            Assert.That(takeoff.Handled, Is.True);
            Assert.That(flight.State, Is.EqualTo(BlackfootFlightState.Idling), "takeoff without an upper Z map should be rejected");

            maps = CreateBlackfootZMaps(server, includeUpper: true);
            (vehicle, pilot, flight) = SpawnPilotedBlackfoot(server, maps.LowerMap);

            flight.State = BlackfootFlightState.Idling;
            flight.TakeoffDuration = TimeSpan.Zero;
            var thruster = GetMountedItem(entMan.System<ItemSlotsSystem>(), vehicle, "thrusters");
            entMan.GetComponent<HardpointIntegrityComponent>(thruster).Integrity = 0;

            takeoff = RaiseTakeoff(entMan, pilot);
            Assert.That(takeoff.Handled, Is.True);
            Assert.That(flight.State, Is.EqualTo(BlackfootFlightState.Idling), "takeoff with broken thrusters should be rejected");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootTakeoffDeniesBlockedUpperFootprint()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var maps = CreateBlackfootZMaps(server, includeUpper: true);
            var (_, pilot, flight) = SpawnPilotedBlackfoot(server, maps.LowerMap);

            Assert.That(maps.UpperMap, Is.Not.Null);
            entMan.SpawnEntity("WallSolid", new EntityCoordinates(maps.UpperMap!.Value, 0, 0));

            flight.State = BlackfootFlightState.Idling;
            flight.TakeoffDuration = TimeSpan.Zero;
            var takeoff = RaiseTakeoff(entMan, pilot);

            Assert.That(takeoff.Handled, Is.True);
            Assert.That(flight.State, Is.EqualTo(BlackfootFlightState.Idling), "upper-map dense blockers should reject takeoff");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootTakeoffAndLandingMoveBetweenZMapsAndManageProjectedEffects()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid vehicle = default;
        EntityUid pilot = default;
        EntityUid lowerMap = default;
        EntityUid upperMap = default;

        await server.WaitAssertion(() =>
        {
            var maps = CreateBlackfootZMaps(server, includeUpper: true);
            lowerMap = maps.LowerMap;
            upperMap = maps.UpperMap!.Value;

            (vehicle, pilot, var flight) = SpawnPilotedBlackfoot(server, lowerMap);
            flight.State = BlackfootFlightState.Idling;
            flight.TakeoffDuration = TimeSpan.Zero;

            var takeoff = RaiseTakeoff(server.EntMan, pilot);
            Assert.That(takeoff.Handled, Is.True);
            Assert.That(flight.State, Is.EqualTo(BlackfootFlightState.TakingOff));
        });

        await pair.RunTicksSync(1);

        EntityUid shadow = default;
        EntityUid downwash = default;
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flight = entMan.GetComponent<BlackfootFlightComponent>(vehicle);
            var xform = entMan.GetComponent<TransformComponent>(vehicle);

            Assert.That(flight.State, Is.EqualTo(BlackfootFlightState.VTOL));
            Assert.That(xform.MapUid, Is.EqualTo(upperMap));
            Assert.That(flight.MovementMode, Is.EqualTo(BlackfootMovementMode.VTOL));
            Assert.That(flight.Shadow, Is.Not.Null);
            Assert.That(flight.Downwash, Is.Not.Null);

            shadow = flight.Shadow!.Value;
            downwash = flight.Downwash!.Value;
            Assert.That(entMan.GetComponent<BlackfootShadowComponent>(shadow).Aircraft, Is.EqualTo(vehicle));
            Assert.That(entMan.GetComponent<TransformComponent>(shadow).MapUid, Is.EqualTo(lowerMap));
            Assert.That(entMan.GetComponent<BlackfootDownwashComponent>(downwash).Aircraft, Is.EqualTo(vehicle));
            Assert.That(entMan.GetComponent<TransformComponent>(downwash).MapUid, Is.EqualTo(lowerMap));
            Assert.That(entMan.GetComponent<VehicleComponent>(vehicle).Operator, Is.EqualTo(pilot));

            flight.LandingDuration = TimeSpan.Zero;
            var land = RaiseLand(entMan, pilot);
            Assert.That(land.Handled, Is.True);
            Assert.That(flight.State, Is.EqualTo(BlackfootFlightState.Landing));
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flight = entMan.GetComponent<BlackfootFlightComponent>(vehicle);
            var xform = entMan.GetComponent<TransformComponent>(vehicle);

            Assert.That(flight.State, Is.EqualTo(BlackfootFlightState.Idling));
            Assert.That(xform.MapUid, Is.EqualTo(lowerMap));
            Assert.That(flight.Shadow, Is.Null);
            Assert.That(flight.Downwash, Is.Null);
            Assert.That(entMan.Deleted(shadow), Is.True);
            Assert.That(entMan.Deleted(downwash), Is.True);
            Assert.That(entMan.GetComponent<VehicleComponent>(vehicle).Operator, Is.EqualTo(pilot));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootZeroFuelCrashesAirborneAircraftDownAndDeletesProjectedEffects()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid vehicle = default;
        EntityUid lowerMap = default;
        EntityUid shadow = default;
        EntityUid downwash = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var maps = CreateBlackfootZMaps(server, includeUpper: true);
            lowerMap = maps.LowerMap;
            var upperMap = maps.UpperMap!.Value;

            vehicle = entMan.SpawnEntity(BlackfootId, new EntityCoordinates(upperMap, 0, 0));
            var flight = entMan.GetComponent<BlackfootFlightComponent>(vehicle);
            var fuel = entMan.GetComponent<BlackfootFuelPowerComponent>(vehicle);

            shadow = entMan.SpawnEntity(flight.ShadowPrototype, new EntityCoordinates(lowerMap, 0, 0));
            var shadowComp = entMan.EnsureComponent<BlackfootShadowComponent>(shadow);
            shadowComp.Aircraft = vehicle;
            flight.Shadow = shadow;
            downwash = entMan.SpawnEntity(flight.DownwashPrototype, new EntityCoordinates(lowerMap, 0, 0));
            var downwashComp = entMan.EnsureComponent<BlackfootDownwashComponent>(downwash);
            downwashComp.Aircraft = vehicle;
            flight.Downwash = downwash;
            flight.State = BlackfootFlightState.VTOL;
            fuel.Fuel = 0f;
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flight = entMan.GetComponent<BlackfootFlightComponent>(vehicle);
            var xform = entMan.GetComponent<TransformComponent>(vehicle);

            Assert.That(flight.State, Is.EqualTo(BlackfootFlightState.Crashed));
            Assert.That(xform.MapUid, Is.EqualTo(lowerMap));
            Assert.That(flight.Shadow, Is.Null);
            Assert.That(flight.Downwash, Is.Null);
            Assert.That(entMan.Deleted(shadow), Is.True);
            Assert.That(entMan.Deleted(downwash), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootTugAttachesAndGatesPilotTaxi()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var vehicle = entMan.SpawnEntity(BlackfootId, new EntityCoordinates(map.Grid, 0, 0));
            var offPointTug = entMan.SpawnEntity(TugId, new EntityCoordinates(map.Grid, 1, 0));
            var tug = entMan.SpawnEntity(TugId, new EntityCoordinates(map.Grid, 0, -1));
            var user = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 0, 1));
            var flight = entMan.GetComponent<BlackfootFlightComponent>(vehicle);
            var vehicleComp = entMan.GetComponent<VehicleComponent>(vehicle);

            Assert.That(CanRunVehicle(entMan, vehicle, vehicleComp), Is.False, "ground taxi should require attached towing gear");

            var activate = new ActivateInWorldEvent(user, offPointTug, true);
            entMan.EventBus.RaiseLocalEvent(offPointTug, activate);
            Assert.That(activate.Handled, Is.True);
            Assert.That(entMan.GetComponent<BlackfootTowComponent>(offPointTug).TowedEntity, Is.Null);
            Assert.That(entMan.GetComponent<BlackfootTowComponent>(vehicle).TowVehicle, Is.Null);

            activate = new ActivateInWorldEvent(user, tug, true);
            entMan.EventBus.RaiseLocalEvent(tug, activate);
            Assert.That(activate.Handled, Is.True);

            var tugTow = entMan.GetComponent<BlackfootTowComponent>(tug);
            var vehicleTow = entMan.GetComponent<BlackfootTowComponent>(vehicle);
            Assert.That(tugTow.TowedEntity, Is.EqualTo(vehicle));
            Assert.That(vehicleTow.TowVehicle, Is.EqualTo(tug));
            Assert.That(entMan.GetComponent<TransformComponent>(tug).ParentUid, Is.EqualTo(vehicle));
            Assert.That(entMan.GetComponent<TransformComponent>(tug).LocalPosition.X, Is.EqualTo(0f).Within(0.001f));
            Assert.That(entMan.GetComponent<TransformComponent>(tug).LocalPosition.Y, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(entMan.GetComponent<PhysicsComponent>(tug).CanCollide, Is.False);
            Assert.That(CanRunVehicle(entMan, vehicle, vehicleComp), Is.True, "attached tug should allow pilot taxi");

            flight.State = BlackfootFlightState.Idling;
            Assert.That(CanRunVehicle(entMan, vehicle, vehicleComp), Is.False, "idling aircraft should not taxi under tug");

            flight.State = BlackfootFlightState.Stowed;
            Assert.That(CanRunVehicle(entMan, vehicle, vehicleComp), Is.True, "stowed aircraft can be repositioned by tug");

            activate = new ActivateInWorldEvent(user, tug, true);
            entMan.EventBus.RaiseLocalEvent(tug, activate);
            Assert.That(activate.Handled, Is.True);
            Assert.That(tugTow.TowedEntity, Is.Null);
            Assert.That(vehicleTow.TowVehicle, Is.Null);
            Assert.That(entMan.GetComponent<PhysicsComponent>(tug).CanCollide, Is.True);
            Assert.That(CanRunVehicle(entMan, vehicle, vehicleComp), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid GetMountedItem(ItemSlotsSystem itemSlots, EntityUid vehicle, string slotId)
    {
        Assert.That(itemSlots.TryGetSlot(vehicle, slotId, out var slot), Is.True, slotId);
        Assert.That(slot!.Item, Is.Not.Null, slotId);
        return slot.Item!.Value;
    }

    private static ShotAttemptedEvent AttemptShot(IEntityManager entMan, EntityUid weapon, EntityUid user)
    {
        var gun = entMan.GetComponent<GunComponent>(weapon);
        var ev = new ShotAttemptedEvent
        {
            User = user,
            Used = (weapon, gun),
        };

        entMan.EventBus.RaiseLocalEvent(weapon, ref ev);
        return ev;
    }

    private static bool CanRunVehicle(IEntityManager entMan, EntityUid vehicle, VehicleComponent vehicleComp)
    {
        var ev = new VehicleCanRunEvent((vehicle, vehicleComp));
        entMan.EventBus.RaiseLocalEvent(vehicle, ref ev);
        return ev.CanRun;
    }

    private static bool HasSpawnedComponent<T>(IEntityManager entMan)
        where T : IComponent
    {
        return entMan.EntityQueryEnumerator<T>().MoveNext(out _, out _);
    }

    private static EntityUid FindEntityWithComponent<T>(IEntityManager entMan)
        where T : IComponent
    {
        var query = entMan.EntityQueryEnumerator<T>();
        Assert.That(query.MoveNext(out var uid, out _), Is.True);
        return uid;
    }

    private static bool HasPrototype(IEntityManager entMan, EntProtoId prototype)
    {
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype.ToString())
                return true;
        }

        return false;
    }

    private static void InteractUsing(IEntityManager entMan, EntityUid user, EntityUid used, EntityUid target)
    {
        var coordinates = entMan.GetComponent<TransformComponent>(target).Coordinates;
        var interact = new InteractUsingEvent(user, used, target, coordinates);
        entMan.EventBus.RaiseLocalEvent(target, interact);
        Assert.That(interact.Handled, Is.True);
    }

    private static BlackfootZMaps CreateBlackfootZMaps(
        RobustIntegrationTest.ServerIntegrationInstance server,
        bool includeUpper)
    {
        var entMan = server.EntMan;
        var mapSystem = server.System<SharedMapSystem>();
        var areaSystem = server.System<AreaSystem>();
        var tileDefinition = server.ResolveDependency<ITileDefinitionManager>()["Plating"];

        var lowerMap = mapSystem.CreateMap(out _);
        FillBlackfootTestMap(entMan, mapSystem, areaSystem, lowerMap, tileDefinition.TileId);

        EntityUid? upperMap = null;
        if (includeUpper)
        {
            upperMap = mapSystem.CreateMap(out _);
            FillBlackfootTestMap(entMan, mapSystem, areaSystem, upperMap.Value, tileDefinition.TileId);

            var zLevels = server.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
            {
                [lowerMap] = 0,
                [upperMap.Value] = 1,
            }), Is.True);
        }

        return new BlackfootZMaps(lowerMap, upperMap);
    }

    private static void FillBlackfootTestMap(
        IEntityManager entMan,
        SharedMapSystem mapSystem,
        AreaSystem areaSystem,
        EntityUid map,
        ushort tileId)
    {
        var grid = entMan.EnsureComponent<MapGridComponent>(map);
        var areaGrid = entMan.EnsureComponent<AreaGridComponent>(map);
        var tile = new Tile(tileId);

        for (var x = -2; x <= 2; x++)
        {
            for (var y = -2; y <= 2; y++)
            {
                var indices = new Vector2i(x, y);
                mapSystem.SetTile(map, grid, indices, tile);
                areaSystem.ReplaceArea(areaGrid, indices, OpenAreaId);
            }
        }
    }

    private static (EntityUid Vehicle, EntityUid Pilot, BlackfootFlightComponent Flight) SpawnPilotedBlackfoot(
        RobustIntegrationTest.ServerIntegrationInstance server,
        EntityUid map)
    {
        var entMan = server.EntMan;
        var vehicle = entMan.SpawnEntity(BlackfootId, new EntityCoordinates(map, 0, 0));
        var pilot = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map, 1, 0));
        var flight = entMan.GetComponent<BlackfootFlightComponent>(vehicle);
        var vehicleComp = entMan.GetComponent<VehicleComponent>(vehicle);

        Assert.That(server.System<Content.Shared.Vehicle.VehicleSystem>().TrySetOperator((vehicle, vehicleComp), pilot), Is.True);
        Assert.That(entMan.GetComponent<BlackfootPilotActionComponent>(pilot).Vehicle, Is.EqualTo(vehicle));
        return (vehicle, pilot, flight);
    }

    private static BlackfootTakeoffActionEvent RaiseTakeoff(IEntityManager entMan, EntityUid pilot)
    {
        var ev = new BlackfootTakeoffActionEvent
        {
            Performer = pilot,
        };

        entMan.EventBus.RaiseLocalEvent(pilot, ev);
        return ev;
    }

    private static BlackfootLandActionEvent RaiseLand(IEntityManager entMan, EntityUid pilot)
    {
        var ev = new BlackfootLandActionEvent
        {
            Performer = pilot,
        };

        entMan.EventBus.RaiseLocalEvent(pilot, ev);
        return ev;
    }

    private readonly record struct BlackfootZMaps(EntityUid LowerMap, EntityUid? UpperMap);

    private readonly record struct BlackfootInteriorExpectation(
        int PassengerSeats,
        int DoorGunnerSeats,
        int Viewports,
        int SideExits,
        int SideExitRights,
        int RearExits,
        int PilotSeats,
        int AmmoLoaders,
        int AmmoLoaderRights,
        int RearDoorButtons,
        int Chassis,
        int CassettePlayers,
        int ExtinguisherCabinets,
        int TotalEntities);

    private static void AssertDeployable(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id,
        EntProtoId prototype,
        BlackfootLandingPadAttachment attachment = BlackfootLandingPadAttachment.None)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
        Assert.That(proto!.TryGetComponent<BlackfootDeployableSupportComponent>(out var deploy, factory), Is.True, id.ToString());
        Assert.That(deploy!.Prototype, Is.EqualTo(prototype), id.ToString());
        Assert.That(deploy.DeployTool.ToString(), Is.EqualTo("Anchoring"), id.ToString());
        Assert.That(deploy.DeployDelay, Is.GreaterThan(0), id.ToString());
        Assert.That(deploy.LandingPadAttachment, Is.EqualTo(attachment), id.ToString());

        if (attachment == BlackfootLandingPadAttachment.None)
        {
            Assert.That(deploy.RequireLandingPad, Is.False, id.ToString());
            return;
        }

        Assert.That(deploy.RequireLandingPad, Is.True, id.ToString());
        Assert.That(deploy.RequireClearFootprint, Is.True, id.ToString());
        Assert.That(deploy.ClearFootprint, Is.EqualTo(new Vector2i(1, 1)), id.ToString());
    }

    private static void AssertMountedPadSupport(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
        Assert.That(proto!.TryGetComponent<TransformComponent>(out var xform, factory), Is.True, id.ToString());
        Assert.That(xform!.Anchored, Is.False, id.ToString());
        Assert.That(proto.TryGetComponent<PullableComponent>(out _, factory), Is.False, id.ToString());
    }

    private static void AssertPackable(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id,
        EntProtoId packedPrototype)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
        Assert.That(proto!.TryGetComponent<BlackfootPackableSupportComponent>(out var packable, factory), Is.True, id.ToString());
        Assert.That(packable!.PackedPrototype, Is.EqualTo(packedPrototype), id.ToString());
        Assert.That(packable.InitialTool.ToString(), Is.EqualTo("Anchoring"), id.ToString());
        Assert.That(packable.PanelTool.ToString(), Is.EqualTo("Screwing"), id.ToString());
        Assert.That(packable.FinalTool.ToString(), Is.EqualTo("Anchoring"), id.ToString());
        Assert.That(packable.InitialDelay, Is.GreaterThan(0), id.ToString());
        Assert.That(packable.PanelDelay, Is.GreaterThan(0), id.ToString());
        Assert.That(packable.FinalDelay, Is.GreaterThan(0), id.ToString());
    }

    private static void AssertUnpickableSupport(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
        Assert.That(proto!.TryGetComponent<ItemComponent>(out _, factory), Is.False, id.ToString());
    }

    private static void AssertEntryPoint(VehicleEntryPoint entry, Vector2 offset, Vector2 interiorCoords, string context)
    {
        Assert.That(entry.Offset.X, Is.EqualTo(offset.X).Within(0.001f), context);
        Assert.That(entry.Offset.Y, Is.EqualTo(offset.Y).Within(0.001f), context);
        Assert.That(entry.Radius, Is.EqualTo(0.75f).Within(0.001f), context);
        Assert.That(entry.InteriorCoords, Is.Not.Null, context);
        Assert.That(entry.InteriorCoords!.Value.X, Is.EqualTo(interiorCoords.X).Within(0.001f), context);
        Assert.That(entry.InteriorCoords.Value.Y, Is.EqualTo(interiorCoords.Y).Within(0.001f), context);
    }

    private static void AssertFixtureBounds(Fixture fixture, float left, float bottom, float right, float top)
    {
        var bounds = GetFixtureLocalBounds(fixture);
        Assert.That(bounds.Left, Is.EqualTo(left).Within(0.001f));
        Assert.That(bounds.Bottom, Is.EqualTo(bottom).Within(0.001f));
        Assert.That(bounds.Right, Is.EqualTo(right).Within(0.001f));
        Assert.That(bounds.Top, Is.EqualTo(top).Within(0.001f));
    }

    private static Box2 GetFixtureLocalBounds(Fixture fixture)
    {
        switch (fixture.Shape)
        {
            case PhysShapeAabb aabb:
                return aabb.LocalBounds;
            case PolygonShape polygon:
                var lower = polygon.Vertices[0];
                var upper = lower;
                foreach (var vertex in polygon.Vertices.Skip(1))
                {
                    lower = Vector2.Min(lower, vertex);
                    upper = Vector2.Max(upper, vertex);
                }

                return new Box2(lower, upper);
            default:
                Assert.Fail($"Unexpected Blackfoot fixture shape {fixture.Shape.GetType().Name}");
                return default;
        }
    }

    private static void AssertBlackfootAmmoBox(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id,
        EntProtoId bulletType,
        int amount)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
        Assert.That(proto!.TryGetComponent<BulletBoxComponent>(out var bulletBox, factory), Is.True, id.ToString());
        Assert.That(bulletBox!.BulletType, Is.EqualTo(bulletType), id.ToString());
        Assert.That(bulletBox.Amount, Is.EqualTo(amount), id.ToString());
        Assert.That(bulletBox.Max, Is.EqualTo(amount), id.ToString());
    }

    private static IEnumerable<string> RequiredBlackfootRuntimeStates()
    {
        var modes = new[] { "vtol", "flight", "stowed" };

        foreach (var mode in modes)
            yield return mode;

        foreach (var mode in modes)
            yield return $"{mode}_lights";

        foreach (var state in new[] { "vtol_thrust", "flight_thrust", "fan-overlay", "downwash", "damage" })
            yield return state;

        foreach (var prefix in new[] { "engines", "launchers", "doorgun", "radar", "para", "recon" })
        {
            foreach (var mode in modes)
                yield return $"{prefix}_{mode}";
        }

        foreach (var mode in modes)
            yield return $"{mode}_shadow";
    }

    private static IEnumerable<string> RequiredBlackfootInteriorStates()
    {
        foreach (var state in new[]
                 {
                     "door left",
                     "door right",
                     "rear door open",
                     "rear door closed",
                     "vehicle_bars",
                     "seat",
                     "seat_buckled",
                     "pilot-chair",
                     "cassette-player",
                     "cassette-player-open",
                     "fire-cab",
                     "fire-cab_full",
                     "fire-cab_empty",
                     "fire-cab_mini",
                 })
        {
            yield return state;
        }
    }

    private static void AssertRsiStates(
        IResourceManager resources,
        string metaPath,
        IEnumerable<string> requiredStates)
    {
        using var stream = resources.ContentFileRead(new ResPath(metaPath));
        using var document = JsonDocument.Parse(stream);

        var states = document.RootElement
            .GetProperty("states")
            .EnumerateArray()
            .Select(state => state.GetProperty("name").GetString())
            .Where(state => state != null)
            .Select(state => state!)
            .ToHashSet();

        var missing = requiredStates
            .Where(required => !states.Contains(required))
            .ToArray();

        Assert.That(missing, Is.Empty, metaPath);
    }

    private static void AssertBlackfootInteriorTiles(IResourceManager resources, ResPath path)
    {
        using var stream = resources.ContentFileRead(path);
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();

        const string marker = "tiles: ";
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), path.ToString());

        start += marker.Length;
        var end = text.IndexOf('\n', start);
        var encoded = text[start..end].Trim();
        var bytes = Convert.FromBase64String(encoded);

        var floorTiles = new HashSet<(int X, int Y)>
        {
            (7, 6),
            (8, 6),
            (7, 7),
            (8, 7),
            (7, 8),
            (8, 8),
            (7, 9),
            (8, 9),
        };

        for (var y = 5; y <= 10; y++)
        {
            for (var x = 6; x <= 9; x++)
            {
                var index = (y * 16 + x) * 7;
                var tile = BitConverter.ToUInt16(bytes, index);
                var expected = floorTiles.Contains((x, y)) ? 2 : 1;
                Assert.That(tile, Is.EqualTo(expected), $"{path} tile {x},{y}");
            }
        }

        Assert.That(BitConverter.ToUInt16(bytes, 0), Is.EqualTo(0), $"{path} outer chunk tiles should be space");
    }

    private static void AssertBlackfootInteriorEntities(IResourceManager resources, ResPath path)
    {
        var pathString = path.ToString();
        using var stream = resources.ContentFileRead(path);
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        var expectation = InteriorExpectations[pathString];

        Assert.That(CountMapInstances(text, "CMUSeatBlackfootPassenger"), Is.EqualTo(expectation.PassengerSeats), pathString);
        Assert.That(CountMapInstances(text, DoorGunnerSeatId.ToString()), Is.EqualTo(expectation.DoorGunnerSeats), pathString);
        Assert.That(CountMapInstances(text, ViewportId.ToString()), Is.EqualTo(expectation.Viewports), pathString);
        Assert.That(CountMapInstances(text, SideExitId.ToString()), Is.EqualTo(expectation.SideExits), pathString);
        Assert.That(CountMapInstances(text, SideExitRightId.ToString()), Is.EqualTo(expectation.SideExitRights), pathString);
        Assert.That(CountMapInstances(text, RearExitId.ToString()), Is.EqualTo(expectation.RearExits), pathString);
        Assert.That(CountMapInstances(text, PilotSeatId.ToString()), Is.EqualTo(expectation.PilotSeats), pathString);
        Assert.That(CountMapInstances(text, "CMUBlackfootAmmoLoader"), Is.EqualTo(expectation.AmmoLoaders), pathString);
        Assert.That(CountMapInstances(text, AmmoLoaderRightId.ToString()), Is.EqualTo(expectation.AmmoLoaderRights), pathString);
        Assert.That(CountMapInstances(text, "CMUBlackfootRearDoorButton"), Is.EqualTo(expectation.RearDoorButtons), pathString);
        Assert.That(CountMapInstances(text, ChassisId.ToString()), Is.EqualTo(expectation.Chassis), pathString);
        Assert.That(CountMapInstances(text, CassettePlayerId.ToString()), Is.EqualTo(expectation.CassettePlayers), pathString);
        Assert.That(CountMapInstances(text, ExtinguisherCabinetId.ToString()), Is.EqualTo(expectation.ExtinguisherCabinets), pathString);
        Assert.That(CountOccurrences(text, "\n  - uid:"), Is.EqualTo(expectation.TotalEntities), pathString);
    }

    private static int CountMapInstances(string text, string prototype)
    {
        var marker = $"- proto: {prototype}";
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return 0;

        var next = text.IndexOf("\n- proto:", start + marker.Length, StringComparison.Ordinal);
        var section = next < 0 ? text[start..] : text[start..next];
        return CountOccurrences(section, "\n  - uid:");
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static int GetAudioChannelCount(byte[] data, string path)
    {
        if (data.Length >= 12 &&
            data[0] == 'R' &&
            data[1] == 'I' &&
            data[2] == 'F' &&
            data[3] == 'F' &&
            data[8] == 'W' &&
            data[9] == 'A' &&
            data[10] == 'V' &&
            data[11] == 'E')
        {
            return GetWavChannelCount(data, path);
        }

        for (var i = 0; i <= data.Length - 12; i++)
        {
            if (data[i] == 0x01 &&
                data[i + 1] == 'v' &&
                data[i + 2] == 'o' &&
                data[i + 3] == 'r' &&
                data[i + 4] == 'b' &&
                data[i + 5] == 'i' &&
                data[i + 6] == 's')
            {
                return data[i + 11];
            }
        }

        throw new InvalidDataException($"Unsupported audio header for {path}");
    }

    private static int GetWavChannelCount(byte[] data, string path)
    {
        var offset = 12;
        while (offset + 8 <= data.Length)
        {
            var size = BitConverter.ToInt32(data, offset + 4);
            if (size < 0 || offset + 8 + size > data.Length)
                break;

            if (data[offset] == 'f' &&
                data[offset + 1] == 'm' &&
                data[offset + 2] == 't' &&
                data[offset + 3] == ' ' &&
                size >= 4)
            {
                return BitConverter.ToUInt16(data, offset + 10);
            }

            offset += 8 + size + (size & 1);
        }

        throw new InvalidDataException($"Missing WAV fmt chunk for {path}");
    }
}
