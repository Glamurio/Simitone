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

        //Fireplaces.iff 4112: node 1 is random_number(Local[0], Tuning[8]); BCON 4096 constant 8 is 750.
        private static BHAV BurnSomething(string label = "burn something")
        {
            return new BHAV
            {
                ChunkLabel = label,
                Instructions = new[]
                {
                    new BHAVInstruction { Opcode = 4100, Operand = new byte[8] },
                    new BHAVInstruction { Opcode = 8, Operand = new byte[] { 0x00, 0x00, 0x19, 0x00, 0x08, 0x00, 0x1a, 0x00 } },
                }
            };
        }

        private static BCON FireTuning()
        {
            return new BCON { ChunkID = 4096, Constants = new ushort[] { 30, 45, 60, 75, 5, 10, 15, 20, 750, 3 } };
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("fireplace ignition: the roll range is 10x larger, i.e. 1/7500 instead of 1/750", () =>
            {
                var t = FireTuning();
                Check(ContentHotfixes.ReduceFireplaceIgnition(BurnSomething(), t, 10), "not patched");
                Check(t.Constants[8] == 7500, "range is " + t.Constants[8]);
                Check(t.Constants[9] == 3 && t.Constants[0] == 30, "other tuning constants untouched");
            });

            yield return ("fireplace ignition: ignition stays possible (never scaled to zero or past the 16-bit signed range)", () =>
            {
                Check(ContentHotfixes.ScaleIgnitionRange(750, 10) > 0, "positive");
                Check(ContentHotfixes.ScaleIgnitionRange(30000, 10) == short.MaxValue, "capped");
                Check(ContentHotfixes.ScaleIgnitionRange(750, 1) == 750 && ContentHotfixes.ScaleIgnitionRange(750, 0) == 750, "divisor 1 or less is a no-op");
            });

            yield return ("fireplace ignition: other BHAVs and other rolls are untouched", () =>
            {
                var t = FireTuning();
                Check(!ContentHotfixes.ReduceFireplaceIgnition(BurnSomething("something else"), t, 10), "unrelated label");
                var other = BurnSomething();
                other.Instructions[1].Operand = new byte[] { 0x00, 0x00, 0x19, 0x00, 0x08, 0x00, 0x07, 0x00 }; //literal range, not Tuning
                Check(!ContentHotfixes.ReduceFireplaceIgnition(other, t, 10), "not a Tuning roll");
                Check(t.Constants[8] == 750, "tuning unchanged");
            });
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
