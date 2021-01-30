using SideLoader;
using System;

namespace NewGamePlus
{
    class NG_StatusManager
    {
        const int RESISTANCE_CHANGE = 25;
        const int DAMAGE_CHANGE = 15;
        const int MAX_LEVEL = 100;

        public static SL_LevelStatusEffect eff = new SL_LevelStatusEffect
        {
            TargetStatusIdentifier = "Drawback",
            NewStatusID = 1337,
            StatusIdentifier = "Stretched Thin",
            Name = "Stretched Thin ({1})",
            Description = "\"I feel thin, sort of stretched, like butter scraped over too much bread.\"\nAll Resistances [E1V1]%\nAll Damage and Impact [E2V1]%",
            Lifespan = -1,
            Purgeable = false,
            MaxLevel = MAX_LEVEL,
            EffectBehaviour = EditBehaviours.Destroy,
            Effects = new SL_EffectTransform[] {
                new SL_EffectTransform
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
                        },
                        new SL_AffectStat
                        {
                            Delay = 0,
                            SyncType = Effect.SyncTypes.Everyone,
                            OverrideCategory = EffectSynchronizer.EffectCategories.None,
                            Stat_Tag = "Impact",
                            AffectQuantity = -DAMAGE_CHANGE,
                            IsModifier = true
                        }
                    }
                }
            }
        };

        public static void InitializeEffects()
        {
            eff.SerializedSLPackName = "NewGamePlus";
            eff.SerializedSubfolderName = "Stretched_Thin";
            eff.ApplyTemplate();
        }

        public static void UpdateLevelData()
        {
            LevelStatusEffect eff = (LevelStatusEffect)ResourcesPrefabManager.Instance.GetStatusEffectPrefab("Stretched Thin");

            int currMaxLevel = (int)At.GetField(eff, "m_maxLevel");
            if(currMaxLevel != MAX_LEVEL)
                At.SetField(eff, "m_maxLevel", MAX_LEVEL);

            float multiplier = (100F - DAMAGE_CHANGE) / 100F;
            float newVal = multiplier;
            for (int i = 0; i < eff.StatusLevelData.Length; i++)
            {
                newVal *= multiplier;
                // Example values for decreaseAmount:
                //  1  -15.0
                //  2  -27.75
                //  3  -38.5875
                //  4  -47.799375
                float decreaseAmount = 100F * newVal - 100F;
                string newVal_str = Math.Round(decreaseAmount).ToString();
                if (decreaseAmount < -95F)
                    newVal_str = Math.Round(decreaseAmount, 1).ToString();

                eff.StatusLevelData[i].StatusData.EffectsData[1].Data[0] = newVal_str;
                eff.StatusLevelData[i].StatusData.EffectsData[2].Data[0] = newVal_str;
            }
        }

        public static void PrintLevelData(LevelStatusEffect eff)
        {
            for (int i = 0; i < eff.StatusLevelData.Length; i++)
            {
                NewGamePlus.Log("StatusLevelData for Level " + i);
                StatusData.EffectData[] datum = eff.StatusLevelData[i].StatusData.EffectsData;
                foreach (StatusData.EffectData datumum in datum)
                {
                    NewGamePlus.Log("Data: " + string.Join(" ,", datumum.Data));
                }
            }
        }
    }
}
