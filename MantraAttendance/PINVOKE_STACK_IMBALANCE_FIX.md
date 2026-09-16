# Mantra MFS100: Resolving PInvokeStackImbalance and CS0103

## 1. Problem Overview

When launching the application in Visual Studio on the company PC, the following two errors/warnings occur:

### Error 1: Managed Debugging Assistant (MDA) Warning
```text
Managed Debugging Assistant 'PInvokeStackImbalance' has detected a problem in 'c:\Program Files (x86)\IIS Express\iisexpress.exe'.
Additional information: A call to PInvoke function 'MANTRA.MSF100!MANTRA.DLL::MSF100Init' has unbalanced the stack.
This is likely because the managed PInvoke signature does not match the unmanaged target signature.
Check that the calling convention and parameter of the PInvoke signature match the target unmanaged signature.
```

### Error 2: Debugger Watch Expression Error
```text
error CS0103: the name '$exception' does not exist in the current context.
```

---

## 2. Root Cause Analysis

### A. The PInvokeStackImbalance Issue

1. **Process Architecture (32-bit x86)**:
   - The application was launched by Visual Studio using **32-bit IIS Express** (`c:\Program Files (x86)\IIS Express\iisexpress.exe`).
   - In 32-bit x86 execution, calling conventions determine how arguments are passed and who cleans up the stack:
     - **`__stdcall` (`Winapi`)**: The **callee** (the function inside the DLL) cleans the stack before returning (`ret 4`).
     - **`__cdecl`**: The **caller** cleans the stack after the function call returns (`add esp, 4`).

2. **The Vendor Signature Conflict**:
   - Inside `MANTRA.MFS100.dll`, Mantra's managed wrapper declares `MSF100Init` as:
     ```csharp
     [DllImport("MFS100Dll.dll", CallingConvention = CallingConvention.Winapi)]
     public static extern int MFS100Init(IntPtr serialno);
     ```
     On Windows x86, `CallingConvention.Winapi` evaluates to `CallingConvention.StdCall`. The CLR expects `MFS100Dll.dll` to clean up the 4-byte `serialno` argument.
   - However, Mantra compiled the native 32-bit `MFS100Dll.dll` using **`__cdecl`** (disassembly confirms it returns with `c3` / `ret`, rather than `c2 04 00` / `ret 4`).
   - As a result, when `MFS100Init` returns, the stack pointer (`ESP`) is left offset by 4 bytes.

3. **Why the Warning Fires**:
   - **`PInvokeStackImbalance`** is a **Managed Debugging Assistant (MDA)** built into the .NET runtime.
   - When running under the **Visual Studio Debugger (F5)**, this MDA actively checks the stack pointer (`ESP`) integrity across unmanaged transitions. When it detects the 4-byte discrepancy, it pauses execution and presents the warning dialog.

---

### B. The CS0103 (`$exception`) Issue

1. In Visual Studio, `$exception` is a debugger pseudo-variable used in the **Watch** or **Immediate** window to inspect active CLR exception instances.
2. An **MDA warning is NOT a managed CLR `System.Exception` instance**. It is a diagnostic notification from the CLR debugging engine.
3. Because no managed exception was thrown onto the thread stack, evaluating `$exception` in the debugger produces:
   `error CS0103: the name '$exception' does not exist in the current context.`
4. This is purely an inspection artifact from the debugger pausing on the MDA; it is not an error in your C# code.

---

### C. Why Did It Work Previously?

1. **Running without Debugging (<kbd>Ctrl</kbd> + <kbd>F5</kbd>)**:
   - MDAs are active **only** while actively attached to the Visual Studio debugger (<kbd>F5</kbd>).
   - Outside the debugger, the x86 .NET runtime automatically adjusts and restores `ESP` on return, so the device initializes, captures, and compares fingerprints without interruption.
2. **64-bit IIS Express vs 32-bit IIS Express**:
   - On 64-bit Windows, the x64 ABI uses a single, unified calling convention (`fastcall` via `RCX`, `RDX`, `R8`, `R9`).
   - The distinction between `stdcall` and `cdecl` does not exist on x64. If your previous computer ran 64-bit IIS Express (`C:\Program Files\IIS Express\`), `PInvokeStackImbalance` could not physically trigger.
3. **Disabled MDA Settings**:
   - The `PInvokeStackImbalance` checkbox in Visual Studio Exception Settings may have been turned off on the previous computer.

---

## 3. Solutions

### Solution 1: Switch Visual Studio to 64-bit IIS Express (Recommended)

Running under 64-bit completely eliminates calling convention mismatches and aligns with modern 64-bit Windows environments.

1. In Visual Studio:
   - Navigate to: **Tools** → **Options** → **Projects and Solutions** → **Web Projects**.
   - Check: ☑ **Use the 64-bit version of IIS Express for web sites and projects**.
   - Click **OK**.
2. Ensure the **64-bit native DLLs** are in your `bin\` folder:
   - Copy `MFS100Dll.dll` and `iengine_ansi_iso.dll` from:
     `MantraAttendance\MantraAttendance\Lib\native\x64\`
   - Paste them directly into:
     `MantraAttendance\MantraAttendance\bin\`
   *(Or in `MantraAttendance.csproj`, change `<Mfs100Bitness>x86</Mfs100Bitness>` to `<Mfs100Bitness>x64</Mfs100Bitness>` and rebuild).*
3. Press <kbd>F5</kbd> to run and debug.

---

### Solution 2: Disable the `PInvokeStackImbalance` MDA (If Staying on 32-bit)

If you must keep 32-bit IIS Express:

1. In Visual Studio:
   - Open the Exception Settings window via **Debug** → **Windows** → **Exception Settings** (or press <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>E</kbd>).
2. In the search box, enter:
   `PInvokeStackImbalance`
3. Expand **Managed Debugging Assistants**.
4. **Uncheck** the box for:
   ☐ **PInvokeStackImbalance**
5. Close the window and continue debugging (<kbd>F5</kbd>).
   - The CLR will safely correct the stack pointer behind the scenes, and the scanner initialization will proceed normally.

---

### Solution 3: Run Without Debugging (Fastest Check)

- Press <kbd>Ctrl</kbd> + <kbd>F5</kbd> (or select **Debug** → **Start Without Debugging**).
- Because MDAs are disabled outside the debugger, your application will initialize and run normally without interruptions.

---

## 4. Summary Table of DLL Placement

| Architecture | IIS Express Executable Path | Native DLL Source Folder | Target Destination |
| :--- | :--- | :--- | :--- |
| **x64 (Recommended)** | `C:\Program Files\IIS Express\iisexpress.exe` | `Lib\native\x64\` | `bin\` |
| **x86** | `C:\Program Files (x86)\IIS Express\iisexpress.exe` | `Lib\native\x86\` | `bin\` |

> **Important**: Never mix architectures. If running 64-bit IIS Express, ensure the DLLs in `bin\` are from `x64`. If running 32-bit IIS Express, ensure the DLLs in `bin\` are from `x86`. Mixing them will cause a `BadImageFormatException`.
