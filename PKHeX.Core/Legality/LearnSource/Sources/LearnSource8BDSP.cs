using System;
using System.Diagnostics.CodeAnalysis;
using static PKHeX.Core.LearnMethod;
using static PKHeX.Core.LearnEnvironment;
using static PKHeX.Core.PersonalInfo8BDSP;

namespace PKHeX.Core;

/// <summary>
/// Exposes information about how moves are learned in <see cref="BDSP"/>.
/// </summary>
public sealed class LearnSource8BDSP : ILearnSource<PersonalInfo8BDSP>, IEggSource, IHomeSource
{
    public static readonly LearnSource8BDSP Instance = new();
    private static readonly PersonalTable8BDSP Personal = PersonalTable.BDSP;
    private static readonly Learnset[] Learnsets = LearnsetReader.GetArray(BinLinkerAccessor16.Get(Util.GetBinaryResource("lvlmove_bdsp.pkl"), "bs"u8));
    private static readonly MoveSource[] EggMoves = MoveSource.GetArray(BinLinkerAccessor16.Get(Util.GetBinaryResource("eggmove_bdsp.pkl"), "bs"u8));
    private const int MaxSpecies = Legal.MaxSpeciesID_8b;
    private const LearnEnvironment Game = BDSP;

    public Learnset GetLearnset(ushort species, byte form) => Learnsets[Personal.GetFormIndex(species, form)];

    public bool TryGetPersonal(ushort species, byte form, [NotNullWhen(true)] out PersonalInfo8BDSP? pi)
    {
        pi = null;
        if (species > MaxSpecies)
            return false;
        pi = Personal[species, form];
        return true;
    }

    public bool GetIsEggMove(ushort species, byte form, ushort move)
    {
        // Array is optimized to not have entries for species above 460 (not able to breed / no egg moves).
        var arr = EggMoves;
        if (species >= arr.Length)
            return false;
        var moves = arr[species];
        return moves.GetHasMove(move);
    }

    public ReadOnlySpan<ushort> GetEggMoves(ushort species, byte form)
    {
        // Array is optimized to not have entries for species above 460 (not able to breed / no egg moves).
        var arr = EggMoves;
        if (species >= arr.Length)
            return [];
        return arr[species].Moves;
    }

    public MoveLearnInfo GetCanLearn(PKM pk, PersonalInfo8BDSP pi, EvoCriteria evo, ushort move, MoveSourceType types = MoveSourceType.All, LearnOption option = LearnOption.Current)
    {
        if (types.HasFlag(MoveSourceType.LevelUp))
        {
            var learn = GetLearnset(evo.Species, evo.Form);
            if (learn.TryGetLevelLearnMove(move, out var level) && level <= evo.LevelMax)
                return new(LevelUp, Game, level);
        }

        if (types.HasFlag(MoveSourceType.SharedEggMove) && GetIsSharedEggMove(pi, move))
            return new(Shared, Game);

        if (types.HasFlag(MoveSourceType.Machine) && pi.GetIsLearnTM(MachineMoves.IndexOf(move)))
            return new(TMHM, Game);

        if (types.HasFlag(MoveSourceType.TypeTutor) && pi.GetIsLearnTutorType(TypeTutorMoves.IndexOf(move)))
            return new(Tutor, Game);

        if (types.HasFlag(MoveSourceType.EnhancedTutor) && GetIsEnhancedTutor(evo, pk, move, option))
            return new(Tutor, Game);

        return default;
    }

    private static bool GetIsEnhancedTutor<T1, T2>(T1 evo, T2 current, ushort move, LearnOption option)
        where T1 : ISpeciesForm
        where T2 : ISpeciesForm
        => evo.Species is (int)Species.Rotom && move switch
    {
        (int)Move.Overheat  => option.IsPast() || current.Form == 1,
        (int)Move.HydroPump => option.IsPast() || current.Form == 2,
        (int)Move.Blizzard  => option.IsPast() || current.Form == 3,
        (int)Move.AirSlash  => option.IsPast() || current.Form == 4,
        (int)Move.LeafStorm => option.IsPast() || current.Form == 5,
        _ => false,
    };

    private bool GetIsSharedEggMove(PersonalInfo8BDSP pi, ushort move)
    {
        var baseSpecies = pi.HatchSpecies;
        var baseForm = pi.HatchFormIndex;
        return GetEggMoves(baseSpecies, baseForm).Contains(move);
    }

    public void GetAllMoves(Span<bool> result, PKM _, EvoCriteria evo, MoveSourceType types = MoveSourceType.All)
    {
        if (!TryGetPersonal(evo.Species, evo.Form, out var pi))
            return;

        if (types.HasFlag(MoveSourceType.LevelUp))
        {
            var learn = GetLearnset(evo.Species, evo.Form);
            var span = learn.GetMoveRange(evo.LevelMax);
            foreach (var move in span)
                result[move] = true;
        }

        if (types.HasFlag(MoveSourceType.SharedEggMove))
        {
            var baseSpecies = pi.HatchSpecies;
            var baseForm = pi.HatchFormIndex;
            var egg = GetEggMoves(baseSpecies, baseForm);
            foreach (var move in egg)
                result[move] = true;
        }

        if (types.HasFlag(MoveSourceType.Machine))
            pi.SetAllLearnTM(result, MachineMoves);

        if (types.HasFlag(MoveSourceType.TypeTutor))
            pi.SetAllLearnTutorType(result, TypeTutorMoves);

        if (types.HasFlag(MoveSourceType.EnhancedTutor))
        {
            var species = evo.Species;
            if (species is (int)Species.Rotom && evo.Form is not 0)
                result[MoveTutor.GetRotomFormMove(evo.Form)] = true;
        }
    }

    public LearnEnvironment Environment => Game;
    public MoveLearnInfo GetCanLearnHOME(PKM pk, EvoCriteria evo, ushort move, MoveSourceType types = MoveSourceType.All)
    {
        if (!TryGetPersonal(evo.Species, evo.Form, out var pi))
            return default;

        if (types.HasFlag(MoveSourceType.LevelUp))
        {
            var learn = GetLearnset(evo.Species, evo.Form);
            if (learn.TryGetLevelLearnMove(move, out var level))
                return new(LevelUp, Game, level);
        }

        if (types.HasFlag(MoveSourceType.SharedEggMove) && GetIsSharedEggMove(pi, move))
            return new(Shared, Game);

        if (types.HasFlag(MoveSourceType.Machine) && pi.GetIsLearnTM(MachineMoves.IndexOf(move)))
            return new(TMHM, Game);

        return default;
    }
}
