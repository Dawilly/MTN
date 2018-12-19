using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace MTN.Patches.NetBuildingRefPatch {
    public class ValueGetter {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {

            var codes = new List<CodeInstruction>(instructions);
            codes[8].opcode = OpCodes.Nop;
            codes[10].opcode = OpCodes.Call;
            codes[10].operand = AccessTools.Method(typeof(Utilities), "GetBuildingFromFarmsByName", new Type[] { typeof(string) });
            return codes.AsEnumerable();
        }
    }
}
