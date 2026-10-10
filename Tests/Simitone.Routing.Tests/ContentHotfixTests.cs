using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using System;
using System.Collections.Generic;

namespace Simitone.Routing.Tests
{
    /// <summary>In-memory corrections to original object BHAVs. The byte strings are the shipped instructions (trashpile.iff 4110).</summary>
    public static class ContentHotfixTests
    {
        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        private static BHAV IncTrashAmount(string label, byte[] cap)
        {
            Func<ushort, byte[], BHAVInstruction> ins = (op, o) => new BHAVInstruction { Opcode = op, Operand = o };
            return new BHAV
            {
                ChunkLabel = label,
                Instructions = new[]
                {
                    ins(2, new byte[] { 0, 0, 0, 0, 0, 3, 1, 9 }),   //SOAttr[0] += Param[0]
                    ins(4100, new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }),
                    ins(2, new byte[] { 0, 0, 10, 0, 0, 0, 1, 7 }),  //SOAttr[0] > 10
                    ins(2, new byte[] { 1, 0, 0, 0, 0, 5, 8, 1 }),  //Temp[1] := SOAttr[0]
                    ins(2, new byte[] { 1, 0, 10, 0, 0, 4, 8, 7 }), //Temp[1] -= 10
                    ins(2, cap),                                      //the cap
                    ins(2, new byte[] { 0, 0, 0, 0, 0, 5, 8, 10 }),
                    ins(343, new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }),
                }
            };
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("pile cap: SOAttr[1] := 10 becomes SOAttr[0] := 10", () =>
            {
                var b = IncTrashAmount("inc trash amount", new byte[] { 1, 0, 10, 0, 0, 5, 1, 7 });
                Check(ContentHotfixes.FixPileCap(b), "faulty cap not patched");
                Check(b.Instructions[5].Operand[0] == 0 && b.Instructions[5].Operand[5] == 5, "cap now writes SOAttr[0]");
            });

            yield return ("pile cap: patching twice changes nothing more", () =>
            {
                var b = IncTrashAmount("inc trash amount", new byte[] { 1, 0, 10, 0, 0, 5, 1, 7 });
                ContentHotfixes.FixPileCap(b);
                Check(!ContentHotfixes.FixPileCap(b), "second pass must be a no-op");
            });

            yield return ("pile cap: trash1.iff variant (already correct) and other BHAVs are untouched", () =>
            {
                var good = IncTrashAmount("inc trash amount", new byte[] { 0, 0, 10, 0, 0, 5, 1, 7 });
                Check(!ContentHotfixes.FixPileCap(good), "correct cap left alone");
                var other = IncTrashAmount("something else", new byte[] { 1, 0, 10, 0, 0, 5, 1, 7 });
                Check(!ContentHotfixes.FixPileCap(other), "unrelated BHAV left alone");
                Check(other.Instructions[5].Operand[0] == 1, "unrelated BHAV unchanged");
            });
        }
    }
}
