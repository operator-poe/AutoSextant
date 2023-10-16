using System.Collections.Generic;
using System.Linq;

namespace AutoSextant;

public class CompassPrice
{
  public string Name { get; set; }
  public int ChaosPrice { get; set; }
  public float DivinePrice { get; set; }
}

public static class CompassList
{
  public static Dictionary<string, string> PriceToModName = new Dictionary<string, string>{
      {"Strongbox Enraged", "MapAtlasStrongboxMonstersEnrageAndDrops"},
      {"8 Modifiers", "MapAtlasMapDropsAreCorruptedAndHave8Modifiers"},
      {"Yellow Plants", "MapAtlasHarvestPlotSeedsContainYellowSeeds"},
      {"Conqueror Map", "MapAtlasBossDropsConquerorMap"},
      {"Delirium Reward", "MapAtlasDeliriumRewardsFillFaster"},
      {"Blue Plants", "MapAtlasHarvestPlotSeedsContainBlueSeeds"},
      {"Purple Plants", "MapAtlasHarvestPlotSeedsContainPurpleSeeds"},
      {"Sacred Grove", "MapAtlasHarvest"},
      {"Beyond", "MapAtlasBeyondAndExtraBeyondDemonChance"},
      {"Shaper Guardian", "MapAtlasBossDropsShaperGuardianMap"},
      {"Possesed Gilded Scarab", "MapAtlasTormentedBetrayerAndGildedScarabDropChance"},
      {"Elder Guardian", "MapAtlasBossDropsElderGuardianMap"},
      {"Runic Monster Markers", "MapAtlasExpeditionAdditionalRunicMonsterMarkers"},
      {"Mirror of Delirium", "MapAtlasDelirium"},
      {"Mysterious Harbinger", "MapAtlasHarbingerAndBossAdditionalCurrencyShards"},
      {"Chayula", "MapAtlasBreachIsChaos"},
      {"Legion", "MapAtlasLegion"},
      {"Copy of Beasts", "MapAtlasCapturedBeastsDuplicateChance"},
      {"Oils Tier", "MapAtlasBlightOilsChanceToDropOneTierHigher"},
      {"Catalysts Duplicate", "MapAtlasMetamorphCatalystsDuplicated"},
      {"Jun", "MapAtlasContainsJunMission2"},
      {"Strongbox Corrupted", "MapAtlasStrongboxesAreCorruptedAndUnided2_"},
      {"Alva", "MapAtlasContainsAlvaMission2"},
      {"Contracts Implicit", "MapAtlasHeistContractsHaveImplicitModifier"},
      {"Hunted Traitors", "MapAtlasHighValueEnemiesOnOwnTeam"},
      {"Alluring", "MapAtlasFishy"},
      {"Ritual Altars", "MapAtlasRitual"},
      {"Smuggler's Cache", "MapAtlasHeist"},
      {"Breach", "MapAtlasAdditionalBreaches2"},
      {"Essence", "MapAtlasAdditionalEssencesAndRemnantChance_"},
      {"Gloom Shrine", "MapAtlasGloomShrineEffectAndDuration2"},
      {"Magic Pack Size", "MapAtlasMagicPackSize3"},
      {"Map 20% Quality", "MapAtlasMapsHave20PercentQuality"},
      {"Niko", "MapAtlasContainsNikoMission2"},
      {"Syndicate Intelligence", "MapAtlasBetrayalIntelligence"},
      {"Unidentified Map", "MapAtlasUnIDedMapsGrantQuantity3"},
      {"Uul-Netol", "MapAtlasBreachIsPhysical"},
      {"Abyss", "MapAtlasAdditionalAbysses2"},
      {"Lightning Monsters", "MapAtlasLightningDamageAndPacks3"},
      {"Rare Map Rare Packs", "MapAtlasMagicAndRareMapsHaveAdditionalPacks3"},
      {"Reflected", "MapAtlasReducedReflectedDamageTakenMirroredRares3"},
      {"Resonating Shrine", "MapAtlasResonatingShrineEffectAndDuration2"},
      {"Ritual Rerolling", "MapAtlasRitualsInAreasCanBeRerolledOnceAtNoCost"},
      {"Soul Gain Prevention", "MapAtlasNoSoulGainPreventionWithExtraVaalPacks3"},
      {"Splinters Emblems Duplicate", "MapAtlasLegionSplintersEmblemsDuplicated"},
      {"Unique Monsters Drop Corrupted", "MapAtlasUniqueMonstersDropCorruptedItems2_"},
      {"Blight", "MapAtlasBlight"},
      {"Map Boss Unique", "MapAtlasBossAdditionalUnique"},
      {"Chaos Monsters", "MapAtlasChaosDamageAndPacks3"},
      {"Cold Monsters", "MapAtlasColdDamageAndPacks3"},
      {"Einhar", "MapAtlasContainsEinharMission2_"},
      {"Esh", "MapAtlasBreachIsLightning"},
      {"Fire Monsters", "MapAtlasFireDamageAndPacks3"},
      {"Map Quality to Rarity", "MapAtlasMapQualityBonusAlsoAppliesToRarity"},
      {"Metamorph", "MapAtlasMetamorph"},
      {"Physical Monsters", "MapAtlasPhysicalDamageAndPacks3_"},
      {"Possesed Polished Scarab", "MapAtlasTormentedBetrayerAndPolishedScarabDropChance"},
      {"Rogue Exiles", "MapAtlasRogueExilesDamageAndDropAdditionalJewels"},
      {"Possessed Uniques", "MapAtlasTormentedGraverobberAndUniqueDropChance"},
      {"Possesed Additional Map", "MapAtlasTormentedHereticAndMapDropChance"},
      {"Tul", "MapAtlasBreachIsCold"},
      {"Vaal Monsters Items Corrupted", "MapAtlasCorruptedDropWithExtraVaalPacks3____"},
      {"Xoph", "MapAtlasBreachIsFire"},
      {"Bodyguards", "MapAtlasBodyguardsAndBossMapDrops2"},
      {"Boss Drop Vaal", "MapAtlasCorruptedMapBossesDropAdditionalVaalItems2"},
      {"Convert Monsters", "MapAtlasMontersThatConvertOnDeath3"},
      {"Flasks Instant", "MapAtlasInstantFlasksAndHealingMonsters3"},
      {"Mortal/Sacrifice Fragment", "MapAtlasUniqueStrongboxChanceWithExtraVaalPacks3_"},
      {"Possessed Rusted Scarab", "MapAtlasTormentedBetrayerAndScarabDropChance"},
      {"Vaal Soul on Kill", "MapAtlasVaalSoulsOnKillAndExtraVaalPacks3"},
      {"Winged Scarab", "MapAtlasTormentedBetrayerAndWingedScarabDropChanceMaven"},
      {"Strongbox Enraged 600%", "MapAtlasStrongboxMonstersEnrageAndDropsMaven___"},
    };
  public static Dictionary<string, string> ModNameToPrice = PriceToModName.ToDictionary(x => x.Value, x => x.Key);
  public static Dictionary<string, CompassPrice> Prices = new Dictionary<string, CompassPrice>();
}


