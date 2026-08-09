// Prints Ghidra's symbols, references, and containing functions for a comma-separated
// IMP_GHIDRA_ADDRESSES environment variable.  Derived decompilation output stays outside
// the repository; this script is only the repeatable inspection tool.
//
// @category Imperialism

import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.Symbol;

public class InspectAddresses extends GhidraScript {
    @Override
    public void run() throws Exception {
        String values = System.getenv("IMP_GHIDRA_ADDRESSES");
        if (values == null || values.trim().isEmpty()) {
            println("[InspectAddresses] set IMP_GHIDRA_ADDRESSES to comma-separated hexadecimal addresses");
            return;
        }

        for (String value : values.split(",")) {
            long raw = Long.parseLong(value.trim().replace("0x", ""), 16);
            Address address = toAddr(raw);
            println(String.format("=== %08X ===", raw));

            Symbol primary = getSymbolAt(address);
            if (primary != null) {
                println("symbol: " + primary.getName(true));
            }

            Function function = getFunctionContaining(address);
            if (function != null) {
                println("function: " + function.getName() + " " + function.getEntryPoint());
            }

            // This also exposes vtable entries and other pointer arrays, for which the
            // W32Dasm-derived SQLite index has no data rows.  Keep it deliberately
            // small: callers supply the exact address they are investigating.
            println("first 40 dwords:");
            for (int offset = 0; offset < 160; offset += 4) {
                try {
                    long word = Integer.toUnsignedLong(getInt(address.add(offset)));
                    println(String.format("  %s: %08X", address.add(offset), word));
                } catch (Exception ignored) {
                    break;
                }
            }

            Reference[] references = getReferencesTo(address);
            println("incoming references: " + references.length);
            for (Reference reference : references) {
                println("  " + reference.getFromAddress() + " " + reference.getReferenceType());
            }
        }

        String decompile = System.getenv("IMP_GHIDRA_DECOMPILE");
        if (decompile == null || decompile.trim().isEmpty()) {
            return;
        }

        DecompInterface decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        try {
            for (String value : decompile.split(",")) {
                long raw = Long.parseLong(value.trim().replace("0x", ""), 16);
                Address address = toAddr(raw);
                Function function = getFunctionContaining(address);
                if (function == null) {
                    // Some C++ virtual-table entries are bare jump thunks that
                    // the imported program has not promoted to functions.
                    // Creating the requested function lets the same narrow,
                    // address-driven inspection decompile those entries.
                    function = createFunction(address, null);
                }
                println(String.format("=== DECOMPILE %08X ===", raw));
                if (function == null) {
                    println("no containing function");
                    continue;
                }

                DecompileResults result = decompiler.decompileFunction(function, 180, monitor);
                if (result.decompileCompleted()) {
                    println(result.getDecompiledFunction().getC());
                } else {
                    println("DECOMPILE FAILED: " + result.getErrorMessage());
                }
            }
        } finally {
            decompiler.dispose();
        }

        String text = System.getenv("IMP_GHIDRA_TEXT");
        if (text == null || text.isEmpty()) {
            return;
        }

        byte[] needle = text.getBytes("US-ASCII");
        for (MemoryBlock block : currentProgram.getMemory().getBlocks()) {
            if (!block.isInitialized() || block.getSize() < needle.length) {
                continue;
            }

            byte[] bytes = new byte[(int)block.getSize()];
            currentProgram.getMemory().getBytes(block.getStart(), bytes);
            for (int offset = 0; offset <= bytes.length - needle.length; offset++) {
                boolean matches = true;
                for (int index = 0; index < needle.length; index++) {
                    if (bytes[offset + index] != needle[index]) {
                        matches = false;
                        break;
                    }
                }
                if (!matches) {
                    continue;
                }

                Address found = block.getStart().add(offset);
                println("=== TEXT " + text + " at " + found + " ===");
                Reference[] references = getReferencesTo(found);
                println("incoming references: " + references.length);
                for (Reference reference : references) {
                    println("  " + reference.getFromAddress() + " " + reference.getReferenceType());
                }
            }
        }
    }
}
