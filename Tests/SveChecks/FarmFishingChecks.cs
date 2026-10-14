using HireSkilledHelpers.Workers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using StardewValley;
using StardewValley.GameData.Locations;

internal static class FarmFishingChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run(string? gamePath)
    {
        // Synthetic topology: no water, an isolated shore, a bridge, blocked bank and reopened bank.
        HashSet<Point> land = new();
        for(int y=0;y<8;y++) for(int x=0;x<12;x++) if(x<4 || x>6) land.Add(new(x,y));
        bool FindSpot(Func<Point,bool> open, Func<int,int,bool> water)
        {
            foreach(Point p in WorkerFishingShoreSearch.ReachableTiles(12,8,new(1,1),open))
            foreach(Point d in new[]{new Point(1,0),new Point(-1,0),new Point(0,1),new Point(0,-1)})
                if(WorkerFishingShorePolicy.TryFindCast(p.X,p.Y,d.X,d.Y,water,water,out _,true)) return true;
            return false;
        }
        Check(!FindSpot(land.Contains,(_,_)=>false), "Waterless farm invented a fishing spot");
        bool Pond(int x,int y)=>x==11 && y==3;
        land.Remove(new(11,3));
        Check(!FindSpot(land.Contains,Pond), "Search crossed walls/water to an isolated bank");
        land.UnionWith(new[]{new Point(4,4),new Point(5,4),new Point(6,4)});
        Check(FindSpot(land.Contains,Pond), "Walkable bridge did not connect the bank");
        var blocked = new HashSet<Point>{new(10,3),new(11,2),new(11,4)};
        Check(!FindSpot(p=>land.Contains(p)&&!blocked.Contains(p),Pond), "Blocked banks accepted");
        blocked.Clear(); Check(FindSpot(land.Contains,Pond), "Rechecking after clearing banks failed");
        Check(!WorkerFishingShoreSearch.ReachableTiles(12,8,new(-1,1),_=>true).Any(), "Off-map entry accepted");
        Check(WorkerFishingShoreSearch.ReachableTiles(12,8,new(1,1),_=>true).Count()==96, "Map bounds or duplicate traversal");

        HashSet<Point> porchFarm = new() { new(5,3), new(6,3), new(5,4) };
        for (int y = 6; y < 14; y++)
        for (int x = 0; x < 15; x++) porchFarm.Add(new(x,y));
        Point[] exterior = WorkerFishingShoreSearch.FromFarmEntrance(15,14,new(5,2),porchFarm.Contains).ToArray();
        Check(exterior.Length == 120 && exterior.Contains(new Point(14,13)) && !exterior.Contains(new Point(5,3)),
            "Farm fishing stayed in the isolated farmhouse porch instead of searching the main Farm");

        var farm = new GameLocation{ NameOrUniqueName="Farm", Depth=3 };
        var coast = new GameLocation{ NameOrUniqueName="Beach", Depth=3 };
        Game1.Locations["Farm"]=farm; Game1.Locations["Beach"]=coast;
        DataLoader.LocationAsset.Clear(); DataLoader.FishAsset.Clear();
        const string id="(O)Example.CustomFarm_Fish";
        ItemRegistry.Known.Add(id);
        DataLoader.FishAsset[id[3..]]="Custom Fish/30/mixed/1/10/600 2600/spring/both/null/3/.4/.1/0/false";
        coast.Data.Fish=new(){new(){ItemId=id}};
        farm.Data.Fish=new(){new(){ItemId="LOCATION_FISH Beach BOBBER_X BOBBER_Y WATER_DEPTH",Chance=.5f}};
        var caught=WorkerLocationFishing.Roll(farm,new(1,1),new(4,1),0,1,new FixedRandom());
        Check(caught?.ItemId==id && caught.IsLiveDataCatch && WorkerFishingCatchCatalog.IsPersistable(caught.Clone()), "Inherited custom fish/save marker");
        Check(WorkerLocationFishing.BestCatch(farm,new(1,1),new(4,1),10,1,new Random(100),_ => 100)?.ItemId==id,
            "Prize catch must follow inherited farm pools");
        coast.Data.Fish[0].CanBeInherited=false;
        Check(WorkerLocationFishing.BestCatch(farm,new(1,1),new(4,1),10,1,new Random(100),_ => 100) is null,
            "Prize catch bypassed CanBeInherited");
        Check(WorkerLocationFishing.Roll(farm,new(1,1),new(4,1),0,1,new FixedRandom()) is null, "CanBeInherited ignored");
        coast.Data.Fish[0].CanBeInherited=true;
        // Both nested and parent generic fish chance apply: .5 * .4 * .4 = .08, not .2.
        int count=0;var random=new Random(54321);
        for(int n=0;n<50000;n++) if(WorkerLocationFishing.Roll(farm,new(1,1),new(4,1),0,n,random)!=null)count++;
        Check(Math.Abs(count/50000d-.08)<.005,$"Inherited chance layers incorrect: {count}");
        farm.Data.Fish.Clear();farm.Properties["FarmFishLocationOverride"]="Beach 1";
        Check(WorkerLocationFishing.Roll(farm,new(1,1),new(4,1),0,1,new FixedRandom())?.ItemId==id, "Custom farm map override ignored");
        Check(WorkerLocationFishing.BestCatch(farm,new(1,1),new(4,1),10,1,new Random(100),_ => 100)?.ItemId==id,
            "Prize catch ignored map override");
        farm.Properties["FarmFishLocationOverride"]="Beach 0";
        Check(WorkerLocationFishing.Roll(farm,new(1,1),new(4,1),0,1,new FixedRandom()) is null, "Zero override chance ignored");
        farm.Properties.Clear();
        farm.Data.Fish=new(){new(){ItemId="LOCATION_FISH Farm BOBBER_X BOBBER_Y WATER_DEPTH"}};
        Check(WorkerLocationFishing.Roll(farm,new(1,1),new(4,1),0,1,new FixedRandom()) is null, "Circular query did not terminate");
        farm.Data.Fish=new(){new(){ItemId="LOCATION_FISH Missing BOBBER_X BOBBER_Y WATER_DEPTH"}};
        Check(WorkerLocationFishing.Roll(farm,new(1,1),new(4,1),0,1,new FixedRandom()) is null, "Missing pool fabricated a catch");
        Console.WriteLine($"Farm topology, inheritance, custom overrides and circular-query checks passed; {count}/50,000 inherited catches (expected 4,000).");
        if(gamePath is null) return;
        using var content=new ContentManager(new GameServiceContainer(),Path.Combine(gamePath,"Content"));
        var locations=content.Load<Dictionary<string,LocationData>>("Data/Locations");
        DataLoader.FishAsset=content.Load<Dictionary<string,string>>("Data/Fish");
        DataLoader.LocationAsset=locations;
        foreach(string fish in DataLoader.FishAsset.Keys)ItemRegistry.Known.Add("(O)"+fish);
        foreach(string item in new[]{"(O)167","(O)168","(O)169","(O)170","(O)171","(O)172","(O)152","(O)153","(O)RiverJelly","(O)SeaJelly","(F)2421"})ItemRegistry.Known.Add(item);
        foreach(string name in new[]{"Beach","Forest","Town","Mountain"})
            Game1.Locations[name]=new(){NameOrUniqueName=name,Data=locations[name],UseDataAreas=true};
        Game1.MasterPlayer.Mail.Add("gotBoatPainting");
        farm.UseDataAreas=true;
        foreach(var (name,tile,expectFish) in new (string,Point,bool)[]{
            ("Farm_Standard",new(20,30),false),("Farm_Riverland",new(30,60),true),
            ("Farm_Beach",new(65,60),true),("Farm_Beach",new(43,65),false),
            ("Farm_Forest",new(20,30),true),("Farm_Hilltop",new(20,30),true),
            ("Farm_Wilderness",new(20,30),true),("Farm_FourCorners",new(20,60),true),
            ("Farm_MeadowlandsFarm",new(20,20),true)})
        {
            farm.Data=locations[name];int fishCount=0;random=new(123);
            Check(WorkerLocationFishing.HasFishPoolAt(farm,tile-new Point(3,0),tile)==expectFish,
                $"Productive shore preference is wrong for {name} at {tile}");
            Check((WorkerLocationFishing.BestCatch(farm,tile-new Point(3,0),tile,10,1,new Random(1),_ => 100) is not null)==expectFish,
                $"Prize fish/trash separation wrong for {name}");
            for(int n=0;n<3000;n++) if(WorkerLocationFishing.Roll(farm,tile-new Point(3,0),tile,5,n,random)!=null)fishCount++;
            Check(expectFish ? fishCount>0 : fishCount==0,$"Native {name} at {tile}: expected fish={expectFish}, got {fishCount}");
            Console.WriteLine($"Native {name} at {tile}: {fishCount}/3000 rewarded fish casts.");
        }
    }
}
