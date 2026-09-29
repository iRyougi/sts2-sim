using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Reporting;

public sealed class PublicApiBinaryCompatibilityTests
{
    [Fact]
    public void RunEngine_RetainsBaselineThreeParameterConstructor()
    {
        Type[] parameters =
        [
            typeof(RunState),
            typeof(Func<IReadOnlyList<MapPoint>, MapPoint>),
            typeof(Func<RunState, AbstractRoom>),
        ];

        Assert.NotNull(typeof(RunEngine).GetConstructor(parameters));
    }

    [Fact]
    public void RunDriver_RetainsBaselineThreeParameterConstructor()
    {
        Type[] parameters =
        [
            typeof(RunState),
            typeof(IRunDecisionSource),
            typeof(Func<RunState, AbstractRoom>),
        ];

        Assert.NotNull(typeof(RunDriver).GetConstructor(parameters));
    }
    [Fact]
    public void RunConstructors_PreserveLegacyAndReportingSourceCallShapes()
    {
        Func<RunState, AbstractRoom>? factory = null;

        RunEngine engineTwo = new(null!, null!);
        RunEngine engineFactory = new(null!, null!, factory);
        RunEngine engineNull = new(null!, null!, null);
        RunEngine engineDefault = new(null!, null!, default(Func<RunState, AbstractRoom>));
        RunEngine engineRecorder = new RunEngine(null!, null!, recorder: null);
        RunEngine enginePotionPolicy = new RunEngine(null!, null!, useAvailablePotions: false);

        RunDriver driverTwo = new(null!, null!);
        RunDriver driverFactory = new(null!, null!, factory);
        RunDriver driverNull = new(null!, null!, null);
        RunDriver driverDefault = new(null!, null!, default(Func<RunState, AbstractRoom>));
        RunDriver driverRecorder = new RunDriver(null!, null!, recorder: null);
        RunDriver driverPotionPolicy = new RunDriver(null!, null!, useAvailablePotions: false);

        Assert.NotNull(engineTwo);
        Assert.NotNull(engineFactory);
        Assert.NotNull(engineNull);
        Assert.NotNull(engineDefault);
        Assert.NotNull(engineRecorder);
        Assert.NotNull(enginePotionPolicy);
        Assert.NotNull(driverTwo);
        Assert.NotNull(driverFactory);
        Assert.NotNull(driverNull);
        Assert.NotNull(driverDefault);
        Assert.NotNull(driverRecorder);
        Assert.NotNull(driverPotionPolicy);
    }

    [Theory]
    [MemberData(nameof(BaselineTaskMethods))]
    public void GameplayMethods_RetainBaselineNonGenericTaskReturn(
        Type declaringType,
        string methodName,
        Type[] parameterTypes)
    {
        var method = declaringType.GetMethod(methodName, parameterTypes);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    public static TheoryData<Type, string, Type[]> BaselineTaskMethods => new()
    {
        {
            typeof(CombatEngine),
            nameof(CombatEngine.PlayCardAsync),
            [typeof(Player), typeof(CardModel), typeof(Creature)]
        },
        {
            typeof(CardModel),
            nameof(CardModel.PlayAsync),
            [typeof(Creature)]
        },
        {
            typeof(AutoPlayCmd),
            nameof(AutoPlayCmd.FromTopOfDrawPile),
            [typeof(ICombatState), typeof(Player), typeof(int)]
        },
        {
            typeof(MerchantRoom),
            nameof(MerchantRoom.Buy),
            [typeof(MerchantCardEntry), typeof(Player)]
        },
        {
            typeof(MerchantRoom),
            nameof(MerchantRoom.Buy),
            [typeof(MerchantRelicEntry), typeof(Player)]
        },
        {
            typeof(MerchantRoom),
            nameof(MerchantRoom.Buy),
            [typeof(MerchantPotionEntry), typeof(Player)]
        },
        {
            typeof(MerchantRoom),
            nameof(MerchantRoom.BuyCardRemoval),
            [typeof(CardModel), typeof(Player)]
        },
    };
}
