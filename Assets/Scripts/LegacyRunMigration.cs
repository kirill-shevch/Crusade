using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
public static class LegacyRunMigration
{
    public static RunSave Import(Func<string,string,string> getString, Func<string,int,int> getInt, UnitCatalog catalog, MapConfig maps)
    {
        var s = new RunSave();
        string character = getString("SelectedCharacter", "");
        if (string.IsNullOrEmpty(character)) return s;
        s.active = true; s.runId = Guid.NewGuid().ToString("N"); s.character = character;
        s.map = getString("currentMap", "FieldMap"); s.node = getInt("CharacterNode",1);
        s.squad = catalog.CreateFreshSquad(JsonUtility.FromJson<UnitArrayWrapper>(getString("PlayerSquad", "{}"))?.units);
        s.nodes = Split(getString("VisitedNodes", "")).Select(int.Parse).ToList();
        if (!s.nodes.Contains(s.node)) s.nodes.Add(s.node);
        s.edges = Split(getString("VisitedEdges", ""));
        s.buffs = Split(getString("PlayerBuffs", ""));
        s.permanent = Split(getString("PermanentBuffs", ""));
        int target = getInt("TargetNode",s.node);
        if (target != s.node) s.edges.Remove(RunSave.EdgeKey(s.node,target));
        var map = maps.maps.Single(m => m.name == s.map);
        s.mapCompleted = map.nodes.Single(n => n.id == s.node).type == "end" && map.nextMaps.Length > 0;
        if (map.nodes.Single(n => n.id == s.node).type == "end" && map.nextMaps.Length == 0) s.active = false;
        return s;
    }
    static List<string> Split(string value) => value.Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).ToList();
}
