using HireSkilledHelpers.Workers;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Locations;
using StardewModdingAPI;
using System.Globalization;

static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
const string prefix = "(O)FlashShifter.StardewValleyExpandedCP_";
const string butter = prefix + "Butterfish", puppy = prefix + "Puppyfish", kitty = prefix + "Kittyfish";
string Butter = "Butterfish/80/mixed/5/20/600 2600/spring summer fall/sunny/null/2/.12/.1/0/false";
string Puppy = "Puppyfish/90/mixed/5/20/600 2600/spring summer fall/both/null/3/.08/.3/0/false";
string Kitty = "Kittyfish/85/mixed/5/20/600 2600/fall winter/both/null/3/.25/.3/8/false";
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
Check(WorkerFishDataRequirements.TryParse(Butter, out var parsed) && parsed.Experience == 29, "Invariant parsing / XP");
Check(parsed.IsEligible(600, false, 0) && !parsed.IsEligible(2600, false, 10) && !parsed.IsEligible(1200, true, 10), "Time/weather gates");
Check(Math.Abs(parsed.Chance(0, 0) - .096f) < .00001f && Math.Abs(parsed.Chance(5, 3) - .22f) < .00001f, "Depth and level formula");
Check(WorkerFishDataRequirements.TryParse(Kitty, out var kitten) && !kitten.IsEligible(1200, false, 7), "Minimum skill");
foreach (string bad in new[] { "", "fish/trap", Butter.Replace(".12", "NaN"), Butter.Replace("600 2600", "600"), Butter.Replace("600 2600", "2600 600") })
    Check(!WorkerFishDataRequirements.TryParse(bad, out _), "Malformed data must fail closed");
WorkerFishDataRequirements.TryParse(Butter.Replace("600 2600", "600 1100 1800 2600"), out var split);
Check(split.IsEligible(1000,false,0) && !split.IsEligible(1100,false,0) && split.IsEligible(1800,false,0), "Split time windows");

GameLocation bridge = new();
foreach (string name in new[] { "Farm", "Forest", "Town", "Mountain", "Beach", "BusStop", "Backwoods", "Railroad", "Woods" }) Game1.Locations[name] = new();
Game1.Locations[WorkerSveIntegration.ShearwaterBridge] = bridge;
WorkerSveIntegration.Initialize(new Registry(false));
Check(WorkerFishingAreaCatalog.Areas.Count == 6 && WorkerForageAreaCatalog.GetAvailableAreas().Count == 9, "Absent SVE changes vanilla menus");
Check(WorkerFishingAreaCatalog.IsKnown(WorkerSveIntegration.ForestWest) && !WorkerFishingAreaCatalog.IsValid(WorkerSveIntegration.ForestWest), "Known saves vs available locations");
WorkerSveIntegration.Initialize(new Registry(true));
Check(WorkerFishingAreaCatalog.Areas.Count == 7 && WorkerForageAreaCatalog.GetAvailableAreas().Count == 10, "Only loaded SVE maps should appear");
Game1.Locations[WorkerSveIntegration.ForestWest] = new();
Check(WorkerFishingAreaCatalog.Areas.Count == 8 && WorkerForageAreaCatalog.GetAvailableAreas().Count == 11, "Both SVE maps should appear");
Check(WorkerFishingAreaCatalog.AccessReason("IslandSouth") is not null, "SVE bypassed island unlock");
Context.IsWorldReady = false;
Check(WorkerFishingAreaCatalog.Areas.Count == 6 && WorkerForageAreaCatalog.GetAvailableAreas().Count == 0, "World readiness guard");
Context.IsWorldReady = true;

void Set(params SpawnFishData[] entries) { bridge.Data.Fish = entries.ToList(); }
void Fish(string id, string raw) { DataLoader.FishAsset[id[3..]] = raw; ItemRegistry.Known.Add(id); }
Fish(butter,Butter); Fish(puppy,Puppy); Fish(kitty,Kitty);
WorkerFishingCatch? Roll(int level = 0, Random? random = null) => WorkerLocationFishing.Roll(bridge, new(10,10), new(13,10), level, 1, random ?? new FixedRandom());
Set(new SpawnFishData() { ItemId = butter, Condition = "LOCATION_Season Here Spring Summer Fall" });
Check(Roll()?.ItemId == butter, "Normal SVE fish not catchable");
bridge.Season = Season.Winter; Check(Roll() is null, "Season leak");
bridge.Season = Season.Spring; bridge.Rain = true; Check(Roll() is null, "Weather leak"); bridge.Rain = false;
Set(new SpawnFishData() { ItemId = kitty, Condition = "LOCATION_Season Here Fall Winter, PLAYER_HAS_SEEN_EVENT Current 69069247" });
bridge.Season = Season.Fall;
Check(Roll(10) is null, "Kittyfish before Camilla"); Game1.MasterPlayer.Events.Add("69069247");
Check(Roll(7) is null && Roll(8)?.ItemId == kitty, "Kittyfish worker skill/event gate");
Set(new SpawnFishData() { ItemId = butter, MinFishingLevel = 5, MinDistanceFromShore = 2, FishAreaId = "River", PlayerPosition = new(10,10,1,1), BobberPosition = new(13,10,1,1) });
Check(Roll(4) is null && Roll(5)?.ItemId == butter, "Location skill / position conditions");
bridge.Depth = 1; Check(Roll(5) is null, "Minimum water depth leak"); bridge.Depth = 3;
bridge.Area = "Pond"; Check(Roll(5) is null, "Fish-area leak"); bridge.Area = "River";
Set(new SpawnFishData() { ItemId = butter, MaxDistanceFromShore = 2 }); Check(Roll() is null, "Maximum depth leak");
Set(new SpawnFishData() { ItemId = butter, PlayerPosition = new(0,0,1,1) }); Check(Roll() is null, "Player-position leak");
Set(new SpawnFishData() { ItemId = butter, BobberPosition = new(0,0,1,1) }); Check(Roll() is null, "Bobber-position leak");
foreach (var special in new SpawnFishData[] { new() { ItemId = butter, IsBossFish = true }, new() { ItemId = butter, SetFlagOnCatch = "quest" }, new() { ItemId = butter, CatchLimit = 1 }, new() { ItemId = butter, RequireMagicBait = true } })
{ Set(special); Check(Roll(10) is null, "Special/bait-only catch granted"); }
// Already exhausted per-player limited entries must not suppress ordinary fish.
Game1.MasterPlayer.fishCaught[butter] = new[] { 1 };
Set(new SpawnFishData() { ItemId = butter, CatchLimit = 1, Precedence = -1 }, new() { ItemId = puppy });
Check(Roll(10)?.ItemId == puppy, "Exhausted catch limit suppressed an ordinary catch");
Game1.MasterPlayer.fishCaught.Clear();
// A winning jelly/default entry consumes the attempt instead of multiplying ordinary fish rates.
ItemRegistry.Known.Add("(O)RiverJelly");
DataLoader.LocationAsset["Default"] = new() { Fish = new() { new() { ItemId = "(O)RiverJelly", Precedence = -1 } } };
Set(new SpawnFishData() { ItemId = butter }); Check(Roll() is null, "Non-fish outcome rerolled");
DataLoader.LocationAsset.Clear();
Set(new SpawnFishData() { ItemId = butter, Chance = 0 }); Check(Roll() is null, "Zero spawn chance ignored");
// Live patch edits must take effect immediately; no stale fish cache.
Set(new SpawnFishData() { ItemId = butter }); DataLoader.FishAsset[butter[3..]] = Butter.Replace("/0/false", "/10/false");
Check(Roll(0) is null && Roll(10)?.ItemId == butter, "Live data invalidation ignored"); Fish(butter, Butter);

// Randomized precedence: with pA=.5*.12=.06, pB=.08, expected A=.0576, B=.0776.
Set(new SpawnFishData() { ItemId = butter, Chance = .5f }, new() { ItemId = puppy });
int a=0,b=0; Random rng = new(12345);
for(int i=0;i<100000;i++) { var catchItem=Roll(0,rng); if(catchItem?.ItemId == butter)a++; if(catchItem?.ItemId == puppy)b++; }
Check(Math.Abs(a/100000d-.0576)<.003 && Math.Abs(b/100000d-.0776)<.003, $"Biased catch rates A={a}, B={b}");
Console.WriteLine($"100,000 cast probability check: Butterfish {a}, Puppyfish {b}, no catch {100000-a-b}.");

// Missing definitions preserve pending custom catches; reinstall delivers exactly once.
WorkerFishingProgress progress = new() { PendingCatches = new() { new() { ItemId = kitty, Experience = 9999 } } };
ItemRegistry.Known.Remove(kitty); int writes=0;
Check(!WorkerFishingStorage.TryDeliver(progress,null,new Monitor(),()=>writes++,out _) && progress.PendingCatches.Count == 1 && WorkerItemStorage.Delivered.Count == 0, "Missing fish lost or shipped as error item");
ItemRegistry.Known.Add(kitty);
Check(WorkerFishingStorage.TryDeliver(progress,null,new Monitor(),()=>writes++,out _) && progress.PendingCatches.Count == 0 && WorkerItemStorage.Delivered.Count == 1 && writes == 1, "Restored catch delivery");
Check(WorkerFishingStorage.TryDeliver(progress,null,new Monitor(),()=>writes++,out _) && WorkerItemStorage.Delivered.Count == 1, "Duplicate delivery");
WorkerSveIntegration.Initialize(new Registry(false));
Check(WorkerFishingAreaCatalog.Areas.Count == 6 && WorkerFishingAreaCatalog.AccessReason(WorkerSveIntegration.ForestWest) is not null, "Removing SVE did not disable destinations");
// Prize mode must enumerate every candidate, ignoring bite odds but keeping all eligibility gates.
bridge.Season = Season.Spring; bridge.Rain = false; Game1.timeOfDay = 1200;
int Price(string id) => id == kitty ? 900 : id == puppy ? 400 : 100;
WorkerFishingCatch? Prize() => WorkerLocationFishing.BestCatch(bridge, new(10,10), new(13,10), 10, 1, new Random(1), Price);
Set(new() { ItemId = butter }, new() { RandomItemId = new() { butter, puppy }, Chance = .00001f });
Check(Prize()?.ItemId == puppy, "Prize must search all random IDs regardless of chance");
Set(new() { ItemId = butter }, new() { ItemId = kitty, Season = Season.Winter });
Check(Prize()?.ItemId == butter, "Prize bypassed season");
Set(new() { ItemId = butter, IsBossFish = true }, new() { ItemId = puppy, CatchLimit = 1 });
Check(Prize() is null, "Prize allowed special/limited fish");
Set(new() { ItemId = butter }, new() { ItemId = puppy, BobberPosition = new(0,0,1,1) });
Check(Prize()?.ItemId == butter, "Prize bypassed bobber position");
Set(new SpawnFishData() { ItemId = butter, Chance = 0 });
Check(Prize() is null, "Prize allowed zero-chance pool");
Set(new SpawnFishData() { ItemId = butter }); bridge.Rain = true;
Check(Prize() is null, "Prize bypassed weather"); bridge.Rain = false;
Game1.timeOfDay = 2600; Check(Prize() is null, "Prize bypassed time"); Game1.timeOfDay = 1200;
Console.WriteLine("Live prize fishing checks passed: highest eligible price, random IDs, season, weather, time, position and special exclusions.");
Console.WriteLine("SVE checks passed: optional locations, live fish data, season/weather/level/event/depth gates, probabilities, special exclusions and missing-content delivery.");

FarmFishingChecks.Run(args.FirstOrDefault());

sealed class Registry(bool loaded) : IModRegistry { public bool IsLoaded(string id) => loaded && id == WorkerSveIntegration.ModId; }
sealed class Monitor : IMonitor { }
sealed class FixedRandom : Random { public override double NextDouble()=>0; public override int Next()=>0; }
