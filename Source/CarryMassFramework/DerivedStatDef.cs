using RimWorld;

namespace CarryMassFramework
{
    class DerivedStatDef: StatDef
    {
        public StatDef defaultBaseStat;

        public static string GetFromBaseStatString(StatDef stat)
        {
            string result = "";
            if (stat is DerivedStatDef)
            {
                DerivedStatDef derivedStat  = (stat as DerivedStatDef);
                if (derivedStat != null)
                {
                    StatDef baseStat = derivedStat.defaultBaseStat;
                    if (baseStat != null)
                    {
                        result = " (from " + baseStat.LabelCap +")";
                    }

                }
            }
            return result;
        }
    }

}
