using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using RimWorld;
using RimWorld.Planet;
using Verse;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace CarryMassFramework
{
    [StaticConstructorOnStartup]
    internal static class CMFHarmonyPatch
    {
        static CMFHarmonyPatch()
        {
            Log.Message("[Carry Mass Framework] Applying Harmony patches...");

            var harmonyCMF = new Harmony("ElminsterAU.CMF");
            //Harmony.DEBUG = true;

            harmonyCMF.Patch(original: AccessTools.Method(typeof(StatWorker), "GetBaseValueFor"), prefix: null, postfix: null,
                transpiler: new HarmonyMethod(typeof(CMFHarmonyPatch), nameof(CMFHarmonyPatch.StatWorker_GetBaseValueForTranspiler)));
            harmonyCMF.Patch(original: AccessTools.Method(typeof(StatWorker), "GetExplanationUnfinalized"), prefix: null, postfix: null,
                transpiler: new HarmonyMethod(typeof(CMFHarmonyPatch), nameof(CMFHarmonyPatch.StatWorker_GetExplanationUnfinalizedTranspiler)));
            harmonyCMF.Patch(original: AccessTools.Method(typeof(MassUtility), nameof(MassUtility.Capacity)), prefix: null, postfix: null,
                transpiler: new HarmonyMethod(typeof(CMFHarmonyPatch), nameof(MassUtility_CapacityTranspiler)));
            harmonyCMF.Patch(original: AccessTools.Method(typeof(MassUtility), nameof(MassUtility.GearMass)), prefix: null, postfix: null,
                transpiler: new HarmonyMethod(typeof(CMFHarmonyPatch), nameof(MassUtility_GearMassTranspiler)));
            harmonyCMF.Patch(original: AccessTools.Method(typeof(MassUtility), nameof(MassUtility.InventoryMass)), prefix: null, postfix: null,
                transpiler: new HarmonyMethod(typeof(CMFHarmonyPatch), nameof(MassUtility_InventoryMassTranspiler)));
            harmonyCMF.Patch(original: AccessTools.Method(typeof(ITab_Pawn_Gear), "DrawThingRow"), prefix: null, postfix: null,
                transpiler: new HarmonyMethod(typeof(CMFHarmonyPatch), nameof(ITab_Pawn_Gear_DrawThingRowTranspiler)));
        }
        static public float DoGetBaseValueFor(StatDef stat, StatRequest request)
        {
            float result = stat.defaultBaseValue;
            if (stat is DerivedStatDef)
            {
                DerivedStatDef derivedStat = (stat as DerivedStatDef);
                if (stat != null)
                {
                    StatDef baseStat = derivedStat.defaultBaseStat;
                    if (baseStat != null)
                    {
                        Thing thing = request.Thing;
                        if (thing != null)
                        {
                            result = thing.GetStatValue(baseStat, true);
                        }
                    }
                }
            }
            return result;
        }

        public static float DoCapacity(Pawn p) => p.GetStatValue(CMFStatDefOf.CarryMass);

        public delegate void CaravanThingsTabUtility_DrawMass_Float(float mass, Rect rect);

        public static readonly CaravanThingsTabUtility_DrawMass_Float delegateTo_CaravanThingsTabUtility_DrawMass_Float =
            (CaravanThingsTabUtility_DrawMass_Float)AccessTools.Method(typeof(CaravanThingsTabUtility), nameof(CaravanThingsTabUtility.DrawMass), new Type[] { typeof(float), typeof(Rect) })
            .CreateDelegate(typeof(CaravanThingsTabUtility_DrawMass_Float));

        public static readonly MethodInfo MethodInfo_CaravanThingsTabUtility_DrawMass_Thing =
            AccessTools.Method(typeof(CaravanThingsTabUtility), nameof(CaravanThingsTabUtility.DrawMass), new Type[] { typeof(Thing), typeof(Rect) });
            

        public static void Do_CaravanThingsTabUtility_DrawMass_Thing(Thing thing, Rect rect, bool inventory, Pawn p)
        {
            float mass = (float)thing.stackCount;
            if (inventory)
            {
                float inventoryMassFactor = 1;
                if (p != null)
                {
                    inventoryMassFactor -= p.GetStatValue(CMFStatDefOf.InventoryMassReduction, true);
                }
                mass *= thing.GetStatValue(StatDefOf.Mass, true) * inventoryMassFactor;
            }
            else
            {
                mass *= thing.GetStatValue(CMFStatDefOf.GearMass, true);
            }
            if (mass != 0) { 
                delegateTo_CaravanThingsTabUtility_DrawMass_Float(mass, rect);
            }               
        }

        public static float AdjustInventoryMass(float mass, Pawn p)
        {
            float inventoryMassFactor = 1 - p.GetStatValue(CMFStatDefOf.InventoryMassReduction, true);
            return mass * inventoryMassFactor;
        }

        public static IEnumerable<CodeInstruction> StatWorker_GetBaseValueForTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            int LdfldCount = 0;
            bool Done = false;

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (!Done)
                {

                    if (instruction.opcode == OpCodes.Ldfld)
                    {
                        if (LdfldCount == 1)
                        {
                            yield return new CodeInstruction(opcode: OpCodes.Ldarg_1);
                            yield return new CodeInstruction(opcode: OpCodes.Call, operand: AccessTools.Method(typeof(CMFHarmonyPatch), nameof(CMFHarmonyPatch.DoGetBaseValueFor)));
                            Done = true;
                            continue;
                        }
                        LdfldCount++;
                    }
                }

                yield return instruction;
            }
        }

        public static IEnumerable<CodeInstruction> StatWorker_GetExplanationUnfinalizedTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            int LdstrCount = 0;
            bool Done = false;

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (!Done)
                {
                    if (instruction.opcode == OpCodes.Ldstr)
                    {
                        if (LdstrCount == 1)
                        {
                            yield return new CodeInstruction(opcode: OpCodes.Ldarg_0);
                            yield return new CodeInstruction(opcode: OpCodes.Ldfld, operand: AccessTools.Field(typeof(StatWorker), "stat"));
                            yield return new CodeInstruction(opcode: OpCodes.Call, operand: AccessTools.Method(typeof(DerivedStatDef), nameof(DerivedStatDef.GetFromBaseStatString)));
                            yield return new CodeInstruction(opcode: OpCodes.Call, operand: AccessTools.Method(typeof(TaggedString), "op_Addition", new Type[] { typeof(TaggedString), typeof(string) }));

                            Done = true;
                        }
                        LdstrCount++;
                    }
                }

                yield return instruction;
            }
        }

        public static IEnumerable<CodeInstruction> MassUtility_CapacityTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            bool Done = false;
            bool Skip = false;

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (!Done)
                {

                    if (instruction.opcode == OpCodes.Callvirt)
                    {
                        yield return new CodeInstruction(opcode: OpCodes.Call, operand: AccessTools.Method(typeof(CMFHarmonyPatch), nameof(CMFHarmonyPatch.DoCapacity)));
                        Skip = true;
                    }
                    if (Skip & instruction.opcode == OpCodes.Stloc_0)
                    {
                        Skip = false;
                        Done = true;
                    }
                }

                if (!Skip)
                {
                    yield return instruction;
                }

            }
        }

        public static IEnumerable<CodeInstruction> MassUtility_GearMassTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            List<CodeInstruction> instructionList = instructions.ToList();

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (instruction.opcode == OpCodes.Ldsfld)
                {
                    instruction.operand = AccessTools.Field(typeof(CMFStatDefOf), nameof(CMFStatDefOf.GearMass));
                }

                yield return instruction;

            }
        }

        public static IEnumerable<CodeInstruction> MassUtility_InventoryMassTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            List<CodeInstruction> instructionList = instructions.ToList();

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (instruction.opcode == OpCodes.Ret)
                {
                    yield return new CodeInstruction(opcode: OpCodes.Ldarg_0);
                    yield return new CodeInstruction(opcode: OpCodes.Call, operand: AccessTools.Method(typeof(CMFHarmonyPatch), nameof(CMFHarmonyPatch.AdjustInventoryMass)));
                }

                yield return instruction;

            }
        }

        public static IEnumerable<CodeInstruction> ITab_Pawn_Gear_DrawThingRowTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            List<CodeInstruction> instructionList = instructions.ToList();


            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (instruction.opcode == OpCodes.Call & MethodInfo_CaravanThingsTabUtility_DrawMass_Thing.Equals(instruction.operand))
                {
                    yield return new CodeInstruction(opcode: OpCodes.Ldarg_S, operand: 4);
                    yield return new CodeInstruction(opcode: OpCodes.Ldarg_0);
                    yield return new CodeInstruction(opcode: OpCodes.Call, operand: AccessTools.Method(typeof(ITab_Pawn_Gear), "get_SelPawnForGear"));
                    instruction.operand = AccessTools.Method(typeof(CMFHarmonyPatch), nameof(CMFHarmonyPatch.Do_CaravanThingsTabUtility_DrawMass_Thing));
                }

                yield return instruction;

            }
        }
    }

}
