using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class CombatPresentationTests
{
    [Test]
    public void BattleAdapterUsesArmorLifestealAndUpdatesBothHpLabels()
    {
        // Keep the scene controller inactive: no live GameManager, saves or scene transitions.
        var root = new GameObject("isolated combat adapter"); root.SetActive(false);
        try
        {
            var manager=root.AddComponent<BattleSceneManager>();
            var attacker=new GameObject("attacker"); attacker.transform.SetParent(root.transform);
            var target=new GameObject("target"); target.transform.SetParent(root.transform);
            var a=new Unit{health=50,maxHealth=100,lifeSteal=50};
            var t=new Unit{health=100,maxHealth=100,armor=100};
            attacker.AddComponent<UnitBehavior>().Initialize(a); target.AddComponent<UnitBehavior>().Initialize(t);
            var aLabel=new GameObject("attacker HP").AddComponent<TextMeshProUGUI>(); aLabel.transform.SetParent(root.transform);
            var tLabel=new GameObject("target HP").AddComponent<TextMeshProUGUI>(); tLabel.transform.SetParent(root.transform);
            typeof(BattleSceneManager).GetField("unitHPTexts",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(manager,
                new Dictionary<GameObject,TextMeshProUGUI>{{attacker,aLabel},{target,tLabel}});
            var apply=typeof(BattleSceneManager).GetMethod("ApplyDamage",BindingFlags.NonPublic|BindingFlags.Instance);
            apply.Invoke(manager,new object[]{a,target,100d});
            Assert.AreEqual(50,t.health); Assert.AreEqual(75,a.health);
            Assert.AreEqual("50",tLabel.text); Assert.AreEqual("75",aLabel.text);
            t.evasion=100; apply.Invoke(manager,new object[]{a,target,100d});
            Assert.AreEqual(50,t.health); Assert.AreEqual(75,a.health);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
