using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RunSaveTests
{
    private string folder;
    private RunSaveStore store;
    private UnitCatalog catalog;
    private MapConfig maps;
    [SetUp] public void SetUp()
    {
        folder = Path.Combine(Path.GetTempPath(), "CrusadeTests-" + Guid.NewGuid().ToString("N"));
        store = new RunSaveStore(Path.Combine(folder,"run.json"));
        catalog = UnitCatalog.FromJson(Resources.Load<TextAsset>("Configs/Units").text);
        maps = JsonUtility.FromJson<MapConfig>(Resources.Load<TextAsset>("Configs/Map").text);
        store.Validate = s => RunRules.Validate(s,catalog,maps);
    }
    [TearDown] public void TearDown() { if (Directory.Exists(folder)) Directory.Delete(folder,true); }
    RunSave Fresh() => new RunSave { active = true, runId = "test-run", character = "Knight", squad = new[]{catalog.CreateLegacyInstance("Knight",1)}, nodes = new List<int>{1} };
    Map Map(RunSave s) => maps.maps.Single(m => m.name == s.map);
    void Reward(RunSave s, string value)
    {
        var node = Map(s).nodes.First(n => n.type == "barracks");
        s.node = node.id; s.nodes.Add(node.id);
        s.reward = new RewardSave { id = s.runId + "/" + s.map + "/" + s.node, type = "barracks", value = value };
    }
    [Test] public void MissingSaveReturnsNull() { Assert.IsNull(store.Load()); }
    [Test] public void RoundTripPreservesEveryField()
    {
        var s = Fresh(); s.heroLevel = 4; s.heroExperience = 123; s.buffs.Add("Armor"); s.permanent.Add("BullHeart");
        Reward(s,"Militia"); s.reward.claimed = true; s.completedBattles.Add("test-run/FieldMap/1-2");
        store.Save(s); Assert.AreEqual(JsonUtility.ToJson(s),JsonUtility.ToJson(store.Load()));
    }
    [Test] public void CorruptPrimaryRecoversBackupAndPreservesItOnNextWrite()
    {
        var s = Fresh(); store.Save(s); s.heroExperience=5; store.Save(s);
        File.WriteAllText(store.Path,"{truncated");
        var recovered = store.Load(); Assert.AreEqual(0,recovered.heroExperience); Assert.IsNotEmpty(store.Warning);
        store.Save(recovered); File.WriteAllText(store.Path,"broken again");
        Assert.AreEqual(0,store.Load().heroExperience);
        Assert.AreEqual(1,Directory.GetFiles(folder,"*.corrupt-*").Length);
    }
    [Test] public void BothDamagedFilesArePreserved()
    {
        Directory.CreateDirectory(folder); File.WriteAllText(store.Path,"bad"); File.WriteAllText(store.Path+".bak","bad2");
        Assert.Throws<IOException>(() => store.Load()); Assert.AreEqual("bad",File.ReadAllText(store.Path));
    }
    [Test] public void NewerFormatDoesNotSilentlyRollback()
    {
        store.Save(Fresh()); store.Save(Fresh()); File.WriteAllText(store.Path,"{\"format\":99}");
        Assert.Throws<NotSupportedException>(() => store.Load());
    }
    [Test] public void UnknownContentCannotOverwriteGoodSave()
    {
        var s=Fresh(); store.Save(s); s.squad[0].definitionId="unknown";
        Assert.Throws<ArgumentException>(() => store.Save(s)); Assert.AreEqual("Knight",store.Load().squad[0].unit);
    }
    [Test] public void LockedPrimaryLeavesLastSaveIntact()
    {
        var s=Fresh(); store.Save(s); s.heroExperience=15;
        using(var locked = new FileStream(store.Path,FileMode.Open,FileAccess.Read,FileShare.None))
            Assert.Throws<IOException>(() => store.Save(s));
        Assert.AreEqual(0,store.Load().heroExperience);
    }
    [Test] public void InterruptedBattleDoesNotVisitEdgeOrTarget()
    {
        var s=Fresh(); var edge=Map(s).edges.First(e=>e.from==1 || e.to==1);
        s.battleFrom=1; s.battleTo=edge.from==1 ? edge.to : edge.from; store.Save(s);
        var resumed=store.Load(); Assert.AreEqual(1,resumed.node); Assert.IsEmpty(resumed.edges); Assert.IsNull(resumed.reward);
    }
    [Test] public void VictoryAndClaimAreIdempotentAcrossReloads()
    {
        var s=Fresh(); var map=Map(s); var node=map.nodes.First(n=>n.type=="treasure");
        var edge=map.edges.First(e=>e.from==node.id || e.to==node.id);
        s.battleFrom=edge.from==node.id ? edge.to : edge.from; s.battleTo=node.id;
        RunRules.Win(s,map,n=>0); store.Save(s); s=store.Load();
        RunRules.Win(s,map,n=>1); RunRules.Claim(s,catalog); store.Save(s); s=store.Load();
        RunRules.Claim(s,catalog); Assert.AreEqual(1,s.buffs.Count); Assert.AreEqual("Armor",s.buffs[0]);
        Assert.AreEqual(1,s.completedBattles.Count); Assert.AreEqual(1,s.edges.Count);
    }
    [Test] public void FullSquadMergesIncomingUnit()
    {
        var s=Fresh(); string[] names={"Knight","Militia","Hunter","Scolar","Defender","Sorcerer"};
        s.squad=names.Select((name,i)=>catalog.CreateLegacyInstance(name,i+1)).ToArray(); Reward(s,"Militia");
        Assert.IsTrue(RunRules.Claim(s,catalog)); Assert.AreEqual(6,s.squad.Length);
        Assert.IsTrue(s.squad.Any(u=>u.unit=="Soldier")); Assert.IsFalse(s.squad.Any(u=>u.unit=="Militia"));
        Assert.AreEqual(6,s.squad.Select(u=>u.placement).Distinct().Count());
    }
    [Test] public void FullSquadWithoutMergeKeepsOriginalAndCanOnlyDecline()
    {
        var s=Fresh(); string[] names={"Knight","Soldier","Bower","Wizard","Defender","Sorcerer"};
        s.squad=names.Select((name,i)=>catalog.CreateLegacyInstance(name,i+1)).ToArray(); Reward(s,"Militia");
        string before=JsonUtility.ToJson(new UnitArrayWrapper{units=s.squad});
        Assert.IsFalse(RunRules.Claim(s,catalog)); Assert.IsFalse(s.reward.claimed);
        s.reward.declined=true; Assert.IsTrue(RunRules.Claim(s,catalog));
        Assert.AreEqual(before,JsonUtility.ToJson(new UnitArrayWrapper{units=s.squad}));
    }
    [Test] public void IncomingUnitCanCascadeMerge()
    {
        var s=Fresh(); s.squad=new[]{s.squad[0],catalog.CreateLegacyInstance("Militia",2),catalog.CreateLegacyInstance("Soldier",3)};
        Reward(s,"Militia"); Assert.IsTrue(RunRules.Claim(s,catalog));
        Assert.AreEqual(2,s.squad.Length); Assert.IsTrue(s.squad.Any(u=>u.unit=="Defender"));
    }
    [Test] public void NextMapRetainsHeroIdentityExperienceAndPermanentBuffs()
    {
        var s=Fresh(); s.heroLevel=3; s.heroExperience=42; s.permanent.Add("BullHeart"); s.buffs.Add("Armor");
        string id=s.squad[0].instanceId; s.squad=new[]{s.squad[0],catalog.CreateLegacyInstance("Militia",2)};
        s.mapCompleted=true; string next=Map(s).nextMaps[0];
        RunRules.NextMap(s,next,maps,catalog); store.Save(s); s=store.Load();
        Assert.AreEqual(id,s.squad.Single().instanceId); Assert.AreEqual(42,s.heroExperience); Assert.AreEqual(3,s.heroLevel);
        CollectionAssert.AreEqual(new[]{"BullHeart"},s.permanent); Assert.IsEmpty(s.buffs); Assert.AreEqual(1,s.node);
        Assert.IsFalse(s.mapCompleted); Assert.IsNull(s.reward);
    }
    [Test] public void NextMapCannotBeSelectedBeforeCompletion()
    {
        var s=Fresh(); Assert.Throws<InvalidOperationException>(()=>RunRules.NextMap(s,Map(s).nextMaps[0],maps,catalog));
    }
    [Test] public void InactiveSaveIsADurableTombstone()
    {
        store.Save(Fresh()); store.Save(new RunSave()); Assert.IsFalse(store.Load().active);
    }
    [Test] public void UncommittedTemporaryFileDoesNotReplacePrimary()
    {
        store.Save(Fresh()); File.WriteAllText(store.Path+".tmp","partial"); Assert.IsTrue(store.Load().active);
    }
    [Test] public void LegacyMigrationAssignsIdsAndRemovesPrematureBattleEdge()
    {
        var data = new Dictionary<string,string> {
            {"SelectedCharacter","Knight"}, {"PlayerSquad","{\"units\":[{\"unit\":\"Knight\",\"placement\":1}]}"},
            {"VisitedNodes","1"}, {"VisitedEdges","1-2"}, {"PlayerBuffs","Armor,"}, {"PermanentBuffs","BullHeart,"}, {"volume","0.5"}
        };
        var s = LegacyRunMigration.Import((key,fallback)=>data.ContainsKey(key)?data[key]:fallback,
            (key,fallback)=>key=="TargetNode"?2:fallback,catalog,maps);
        store.Save(s); s=store.Load(); Assert.IsNotEmpty(s.squad[0].instanceId); Assert.IsNotEmpty(s.squad[0].definitionId);
        Assert.IsEmpty(s.edges); CollectionAssert.AreEqual(new[]{"Armor"},s.buffs);
        Assert.AreEqual("0.5",data["volume"]); Assert.AreEqual("1-2",data["VisitedEdges"]);
    }
    [Test] public void NoLegacyCharacterCreatesInactiveRun()
    {
        Assert.IsFalse(LegacyRunMigration.Import((key,fallback)=>fallback,(key,fallback)=>fallback,catalog,maps).active);
    }
    [Test] public void SnapshotMutationDoesNotAffectCommittedState()
    {
        var committed=Fresh(); var candidate=committed.Copy(); candidate.squad[0].placement=3;
        candidate.buffs.Add("Damage"); Assert.AreEqual(1,committed.squad[0].placement); Assert.IsEmpty(committed.buffs);
        Assert.IsNull(candidate.reward);
    }
}
