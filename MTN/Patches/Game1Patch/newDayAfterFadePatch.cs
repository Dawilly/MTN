using StardewValley;
using StardewValley.Locations;

namespace MTN.Patches.Game1Patch
{
    class newDayAfterFadePatch
    {
        public static void Postfix()
        {
            if (Game1.player.slotCanHost == true) return;

            foreach(var location in Game1.locations)
            {
                if (location is BuildableGameLocation)
                {
                    foreach(var building in ((BuildableGameLocation)location).buildings)
                    {
                        if (building.daysOfConstructionLeft.Value <= 0 && building.indoors.Value is Cabin)
                        {
                            Game1.player.slotCanHost = true;
                            return;
                        }
                    }
                }
            }
        }
    }
}
