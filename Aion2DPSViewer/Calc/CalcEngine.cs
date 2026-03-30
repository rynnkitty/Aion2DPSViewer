using Jint;
using Jint.Native;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Aion2DPSViewer.Calc;

public class CalcEngine
{
    private static readonly string[] DisplayStubs = new string[31]
    {
        "displayAttackPowerStats",
        "displayCriticalHitStats",
        "displayDamageAmplificationStats",
        "displayCombatSpeedStats",
        "displayCooldownReductionStats",
        "displayStunHitStats",
        "displayMultiHitStats",
        "displayPerfectStats",
        "displayAccuracyStats",
        "displaySkillDamageStats",
        "displayNormalStats",
        "displayStats",
        "displayEquipment",
        "displayTitles",
        "displaySkills",
        "displayDaevanionInfo",
        "displayDaevanionPoints",
        "displayCombatStatsPercentiles",
        "displayCombatScoreMaxInfo",
        "displayCombatScorePowerRangeInfo",
        "displayCombatScoreRankingsInfo",
        "displayContentRankings",
        "displayJobStatistics",
        "displayRankingTable",
        "displayServerStats",
        "displayAllServersResults",
        "updateAttackPowerCapUI",
        "showJonggulBadge",
        "hideJonggulBadge",
        "applyCompanionBadge",
        "calculateDpsScore"
    };

    public static CalcResult RunCalc(string calcJs, CalcInput input)
    {
        Engine engine = new Engine((Action<Options>)(cfg => cfg.LimitRecursion(256).TimeoutInterval(TimeSpan.FromSeconds(10.0))));
        engine.SetValue("isCalculatingAttackPower", false);
        engine.SetValue("isUpdatingDaevanionPoints", false);
        engine.SetValue("attackPowerResult", JsValue.Null);
        engine.SetValue("combatSpeedResult", JsValue.Null);
        engine.SetValue("damageAmplificationResult", JsValue.Null);
        engine.SetValue("criticalHitResult", JsValue.Null);
        engine.SetValue("cooldownReductionResult", JsValue.Null);
        engine.SetValue("stunHitResult", JsValue.Null);
        engine.SetValue("multiHitResult", JsValue.Null);
        engine.SetValue("perfectResult", JsValue.Null);
        engine.SetValue("accuracyResult", JsValue.Null);
        engine.SetValue("skillDamageResult", JsValue.Null);
        engine.SetValue("isAttackPowerOverCap", false);
        engine.SetValue("cappedAttackPower", JsValue.Null);
        engine.SetValue("weaponMinAttack", 0);
        engine.SetValue("weaponMaxAttack", 0);
        engine.SetValue("hasJonggulTitle", false);
        engine.SetValue("currentCombatScoreMax", 0);
        engine.SetValue("isCacheMiss", false);
        engine.Execute($"var arcanaSetCounts = {JsonSerializer.Serialize<Dictionary<string, int>>(input.ArcanaSetCounts)};");
        engine.SetValue<Func<JsValue, JsValue, JsValue>>("setTimeout", (fn, delay) =>
        {
            if (fn.IsObject())
                fn.AsObject().Call();
            return JsValue.Undefined;
        });
        engine.SetValue<Action<JsValue>>("clearTimeout", v => { });
        engine.SetValue<Func<JsValue, JsValue, JsValue>>("setInterval", (fn, delay) => JsValue.Undefined);
        engine.SetValue<Action<JsValue>>("clearInterval", v => { });
        engine.Execute("\r\nvar document = {\r\n    getElementById: function() { return { style: {}, classList: { add: function(){}, remove: function(){}, toggle: function(){}, contains: function(){ return false; } }, setAttribute: function(){}, getAttribute: function(){ return null; }, textContent: '', innerHTML: '', value: '', appendChild: function(){}, removeChild: function(){}, querySelectorAll: function(){ return []; }, querySelector: function(){ return null; } }; },\r\n    querySelector: function() { return null; },\r\n    querySelectorAll: function() { return []; },\r\n    createElement: function() { return { style: {}, classList: { add: function(){}, remove: function(){}, toggle: function(){}, contains: function(){ return false; } }, setAttribute: function(){}, getAttribute: function(){ return null; }, textContent: '', innerHTML: '', value: '', appendChild: function(){}, removeChild: function(){}, querySelectorAll: function(){ return []; }, querySelector: function(){ return null; } }; },\r\n    createTextNode: function() { return {}; }\r\n};\r\nvar window = this;\r\n");
        foreach (string displayStub in DisplayStubs)
            engine.Execute($"function {displayStub}() {{}}");
        engine.Execute(calcJs);
        engine.Execute($"var currentTitles = {input.TitlesJson};");
        engine.SetValue("currentWingName", input.WingName);
        engine.Execute($"var currentSkills = {input.SkillsJson};");
        engine.Execute($"var currentStigmas = {input.StigmasJson};");
        engine.Execute($"var daevanionData = {input.DaevanionDataJson};");
        engine.SetValue("currentJobName", input.JobName);
        engine.SetValue("currentCharacterJob", input.JobName);
        engine.Execute($"var currentCharacterData = {input.CharacterDataJson};");
        if (input.SkillPrioritiesJson != "null")
            engine.Execute($"var currentSkillPriorities = {input.SkillPrioritiesJson};");
        else
            engine.SetValue("currentSkillPriorities", JsValue.Null);
        engine.Execute($"\r\nif (typeof calculateAttackPower === 'function') {{\r\n    calculateAttackPower({input.EquipmentJson}, {input.AccessoriesJson}, {input.StatDataJson}, daevanionData);\r\n}}\r\n");
        JsValue jsValue = engine.GetValue("skillDamageResult");
        if (jsValue.IsNull() || jsValue.IsUndefined())
        {
            try
            {
                engine.Execute($"\r\nif (typeof calculateSkillDamage === 'function') {{\r\n    calculateSkillDamage({input.SkillsJson}, {input.StigmasJson});\r\n}}\r\n");
            }
            catch
            {
            }
        }
        return new CalcResult()
        {
            AttackPower = GetJsObject(engine, "attackPowerResult"),
            CriticalHit = GetJsObject(engine, "criticalHitResult"),
            DamageAmplification = GetJsObject(engine, "damageAmplificationResult"),
            CombatSpeed = GetJsObject(engine, "combatSpeedResult"),
            CooldownReduction = GetJsObject(engine, "cooldownReductionResult"),
            StunHit = GetJsObject(engine, "stunHitResult"),
            MultiHit = GetJsObject(engine, "multiHitResult"),
            Perfect = GetJsObject(engine, "perfectResult"),
            Accuracy = GetJsObject(engine, "accuracyResult"),
            SkillDamage = GetJsObject(engine, "skillDamageResult"),
            WeaponMinAttack = GetJsDouble(engine, "weaponMinAttack"),
            WeaponMaxAttack = GetJsDouble(engine, "weaponMaxAttack"),
            IsAttackPowerOverCap = GetJsBool(engine, "isAttackPowerOverCap"),
            CappedAttackPower = GetJsNullableDouble(engine, "cappedAttackPower")
        };
    }

    private static JsonElement GetJsObject(Engine engine, string name)
    {
        JsValue jsValue = engine.GetValue(name);
        if (!jsValue.IsNull() && !jsValue.IsUndefined())
        {
            try
            {
                engine.SetValue("__tmp__", jsValue);
                return JsonDocument.Parse(engine.Evaluate("JSON.stringify(__tmp__)").AsString()).RootElement;
            }
            catch
            {
                return new JsonElement();
            }
        }
        return new JsonElement();
    }

    private static double GetJsDouble(Engine engine, string name)
    {
        JsValue jsValue = engine.GetValue(name);
        return !jsValue.IsNumber() ? 0.0 : jsValue.AsNumber();
    }

    private static double? GetJsNullableDouble(Engine engine, string name)
    {
        JsValue jsValue = engine.GetValue(name);
        return !jsValue.IsNumber() ? (double?)null : jsValue.AsNumber();
    }

    private static bool GetJsBool(Engine engine, string name)
    {
        JsValue jsValue = engine.GetValue(name);
        return jsValue.IsBoolean() && jsValue.AsBoolean();
    }
}
