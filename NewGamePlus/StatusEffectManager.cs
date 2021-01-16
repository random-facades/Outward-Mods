using HarmonyLib;
using SideLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGamePlus
{
    class StatusEffectManager
    {
        const int RESISTANCE_CHANGE = 25;
        const int DAMAGE_CHANGE = 15;


        public static SL_StatusEffectFamily fam = new SL_StatusEffectFamily
        {
            UID = "com.random_facades.statusfamily",
            Name = "LegacyFamily",
            StackBehaviour = StatusEffectFamily.StackBehaviors.StackAll,
            MaxStackCount = 50,
            LengthType = StatusEffectFamily.LengthTypes.Long
        };

        public static SL_EffectTransform effTrans = new SL_EffectTransform
        {
            TransformName = "ActivationEffects",
            Effects = new SL_Effect[]
            {
                new SL_AffectStat
                {
                    Delay = 0,
                    SyncType = Effect.SyncTypes.Everyone,
                    OverrideCategory = EffectSynchronizer.EffectCategories.None,
                    Stat_Tag = "AllResistances",
                    AffectQuantity = -RESISTANCE_CHANGE,
                    IsModifier = false
                },
                new SL_AffectStat
                {
                    Delay = 0,
                    SyncType = Effect.SyncTypes.Everyone,
                    OverrideCategory = EffectSynchronizer.EffectCategories.None,
                    Stat_Tag = "AllDamages",
                    AffectQuantity = -DAMAGE_CHANGE,
                    IsModifier = true
                }
            }
        };

        public static SL_StatusEffect eff = new SL_StatusEffect
        {
            TargetStatusIdentifier = "Drawback",
            NewStatusID = 1337,
            StatusIdentifier = "Stretched Thin",
            Name = "Stretched Thin",
            Description = "\"I feel thin, sort of stretched, like butter scraped over too much bread.\"\nAll Resistances -" + RESISTANCE_CHANGE + "% per stack\nAll Damage -" + DAMAGE_CHANGE + "% per stack (Multiplicative Scaling)",
            Lifespan = -1,
            ActionOnHit = StatusEffect.ActionsOnHit.None,
            DisplayedInHUD = true,
            IsHidden = false,
            IsMalusEffect = true,
            FamilyMode = StatusEffect.FamilyModes.Bind,
            BindFamily = fam,
            EffectBehaviour = EditBehaviours.Destroy,
            Effects = new SL_EffectTransform[] { effTrans }
        };

        public static void InitializeEffects()
        {
            //eff.SLPackName = "NewGamePlus";
            //eff.SubfolderName = "LegacyEffect";
            fam.Apply();
            eff.Apply();
        }

        [HarmonyPatch(typeof(StatusEffect), "UpdateTotalData", new Type[] { typeof(bool) })]
        public class StatusEffect_UpdateTotalData
        {
            [HarmonyPrefix]
            public static void Prefix(StatusEffect __instance, bool _updateDelta, ref List<Effect> ___m_effectList, ref List<StatusData> ___m_statusStack)
            {
                // m_statusStack
                //    Array of status effects that are active, and their values in .EffectsData
                //    TODO: change the values in there to what I want them to be

                if (__instance.IdentifierName == "Stretched Thin")
                {
                    float newValue = -100*(float)((1 - Math.Pow((100 - DAMAGE_CHANGE) / 100.0, ___m_statusStack.Count)) / ___m_statusStack.Count);
                    string valStr = newValue.ToString();

                    foreach (StatusData datum in ___m_statusStack)
                    {
                        // Time to modify
                        datum.EffectsData[1].Data[0] = valStr;
                    }
                }
            }
        }
    }
}
